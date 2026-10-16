using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.Persistence;

public static class SimulationSnapshotSerializer
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RGSNAP01");

    public static void Write(Stream stream, SimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!stream.CanWrite)
        {
            throw new ArgumentException("Snapshot stream must be writable.", nameof(stream));
        }

        var writer = new CanonicalWriter();
        WriteCanonical(writer, snapshot);
        byte[] payload = writer.ToArray();
        byte[] checksum = SHA256.HashData(payload);

        stream.Write(Magic);
        Span<byte> length = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(length, payload.LongLength);
        stream.Write(length);
        stream.Write(payload);
        stream.Write(checksum);
    }

    public static SimulationSnapshot Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Snapshot stream must be readable.", nameof(stream));
        }

        Span<byte> magic = stackalloc byte[Magic.Length];
        ReadExactly(stream, magic);
        if (!magic.SequenceEqual(Magic))
        {
            throw new InvalidDataException("Snapshot magic header is invalid.");
        }

        Span<byte> lengthBytes = stackalloc byte[sizeof(long)];
        ReadExactly(stream, lengthBytes);
        long length = BinaryPrimitives.ReadInt64BigEndian(lengthBytes);
        if (length < 0 || length > 512L * 1024L * 1024L)
        {
            throw new InvalidDataException($"Snapshot payload length {length} is outside the supported range.");
        }

        byte[] payload = new byte[checked((int)length)];
        ReadExactly(stream, payload);
        Span<byte> declaredChecksum = stackalloc byte[StateChecksum.ByteLength];
        ReadExactly(stream, declaredChecksum);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Snapshot contains trailing data.");
        }

        Span<byte> actualChecksum = stackalloc byte[StateChecksum.ByteLength];
        SHA256.HashData(payload, actualChecksum);
        if (!actualChecksum.SequenceEqual(declaredChecksum))
        {
            throw new InvalidDataException("Snapshot payload checksum is invalid.");
        }

        return ReadCanonical(new CanonicalReader(payload));
    }

    private static void WriteCanonical(CanonicalWriter writer, SimulationSnapshot snapshot)
    {
        writer.WriteString("roentgen.simulation-snapshot");
        writer.WriteInt32(snapshot.FormatVersion);
        writer.WriteUInt64(snapshot.CampaignSeed.Value);
        writer.WriteInt32(snapshot.TicksPerSecond);
        writer.WriteUInt64(snapshot.CompletedTick);
        writer.WriteDouble(snapshot.TimeScale);
        writer.WriteUInt64(snapshot.NextCommandSequence);
        writer.WriteUInt64(snapshot.NextEventSequence);

        writer.WriteUInt64(snapshot.EntityRegistry.MutationVersion);
        writer.WriteCount(snapshot.EntityRegistry.Entities.Count);
        foreach (EntitySnapshot entity in snapshot.EntityRegistry.Entities.OrderBy(item => item.Id))
        {
            writer.WriteStableId(entity.Id);
            writer.WriteString(entity.ArchetypeId);
            writer.WriteUInt64(entity.CreatedTick);
            writer.WriteCount(entity.Tags.Count);
            foreach (string tag in entity.Tags.OrderBy(item => item, StringComparer.Ordinal))
            {
                writer.WriteString(tag);
            }

            writer.WriteCount(entity.Components.Count);
            foreach (ComponentSnapshot component in entity.Components.OrderBy(item => item.TypeId, StringComparer.Ordinal))
            {
                writer.WriteString(component.TypeId);
                writer.WriteInt32(component.SchemaVersion);
                writer.WriteBytes(component.Payload);
                writer.WriteChecksum(component.PayloadChecksum);
            }
        }

        writer.WriteUInt64(snapshot.StateStore.MutationVersion);
        writer.WriteCount(snapshot.StateStore.Values.Count);
        foreach (SimulationStateValueSnapshot value in snapshot.StateStore.Values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WriteString(value.Key);
            writer.WriteInt32(value.SchemaVersion);
            writer.WriteBytes(value.Payload);
            writer.WriteChecksum(value.PayloadChecksum);
        }

        CommandEnvelope[] pendingCommands = snapshot.PendingCommands
            .OrderBy(command => command, CommandEnvelopeCanonicalComparer.Instance)
            .ToArray();
        writer.WriteCount(pendingCommands.Length);
        foreach (CommandEnvelope command in pendingCommands)
        {
            writer.WriteStableId(command.CommandId);
            writer.WriteStableId(command.IssuerId);
            writer.WriteUInt64(command.TargetTick);
            writer.WriteInt32((int)command.OriginPriority);
            writer.WriteUInt64(command.Sequence);
            writer.WriteString(command.TypeId);
            writer.WriteInt32(command.SchemaVersion);
            writer.WriteBytes(command.Payload);
            writer.WriteChecksum(command.PayloadChecksum);
        }

        writer.WriteChecksum(snapshot.StateChecksum);
    }

    private static SimulationSnapshot ReadCanonical(CanonicalReader reader)
    {
        string marker = reader.ReadRequiredString();
        if (!string.Equals(marker, "roentgen.simulation-snapshot", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Snapshot canonical marker is invalid.");
        }

        int formatVersion = reader.ReadInt32();
        if (formatVersion != SimulationSnapshot.CurrentFormatVersion)
        {
            throw new NotSupportedException($"Simulation snapshot format {formatVersion} is not supported.");
        }

        var campaignSeed = new CampaignSeed(reader.ReadUInt64());
        int ticksPerSecond = reader.ReadInt32();
        if (ticksPerSecond <= 0)
        {
            throw new InvalidDataException("Snapshot tick rate must be positive.");
        }

        ulong completedTick = reader.ReadUInt64();
        double timeScale = reader.ReadDouble();
        if (!double.IsFinite(timeScale) || timeScale < 0.0)
        {
            throw new InvalidDataException("Snapshot time scale must be finite and non-negative.");
        }

        ulong nextCommandSequence = reader.ReadUInt64();
        ulong nextEventSequence = reader.ReadUInt64();

        ulong entityMutationVersion = reader.ReadUInt64();
        int entityCount = reader.ReadCount();
        var entities = new List<EntitySnapshot>(entityCount);
        StableId previousEntityId = default;
        for (int index = 0; index < entityCount; index++)
        {
            StableId id = reader.ReadStableId();
            if (index > 0 && id.CompareTo(previousEntityId) <= 0)
            {
                throw new InvalidDataException("Snapshot entities are not in strict stable-ID order.");
            }

            previousEntityId = id;
            string archetypeId = reader.ReadRequiredString();
            ulong createdTick = reader.ReadUInt64();
            int tagCount = reader.ReadCount();
            var tags = new List<string>(tagCount);
            string? previousTag = null;
            for (int tagIndex = 0; tagIndex < tagCount; tagIndex++)
            {
                string tag = reader.ReadRequiredString();
                if (previousTag is not null && string.CompareOrdinal(tag, previousTag) <= 0)
                {
                    throw new InvalidDataException("Snapshot entity tags are not in strict ordinal order.");
                }

                previousTag = tag;
                tags.Add(tag);
            }

            int componentCount = reader.ReadCount();
            var components = new List<ComponentSnapshot>(componentCount);
            string? previousComponent = null;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                string typeId = reader.ReadRequiredString();
                if (previousComponent is not null && string.CompareOrdinal(typeId, previousComponent) <= 0)
                {
                    throw new InvalidDataException("Snapshot components are not in strict ordinal order.");
                }

                previousComponent = typeId;
                int schemaVersion = reader.ReadInt32();
                byte[] payload = reader.ReadBytes();
                StateChecksum checksum = reader.ReadChecksum();
                _ = EntityComponentState.Restore(typeId, schemaVersion, payload, checksum);
                components.Add(new ComponentSnapshot(typeId, schemaVersion, payload, checksum));
            }

            entities.Add(new EntitySnapshot(id, archetypeId, createdTick, tags, components));
        }

        ulong stateMutationVersion = reader.ReadUInt64();
        int valueCount = reader.ReadCount();
        var values = new List<SimulationStateValueSnapshot>(valueCount);
        string? previousKey = null;
        for (int index = 0; index < valueCount; index++)
        {
            string key = reader.ReadRequiredString();
            if (previousKey is not null && string.CompareOrdinal(key, previousKey) <= 0)
            {
                throw new InvalidDataException("Snapshot state keys are not in strict ordinal order.");
            }

            previousKey = key;
            int schemaVersion = reader.ReadInt32();
            byte[] payload = reader.ReadBytes();
            StateChecksum checksum = reader.ReadChecksum();
            _ = SimulationStateValue.Restore(key, schemaVersion, payload, checksum);
            values.Add(new SimulationStateValueSnapshot(key, schemaVersion, payload, checksum));
        }

        int commandCount = reader.ReadCount();
        var commands = new List<CommandEnvelope>(commandCount);
        CommandEnvelope? previousCommand = null;
        for (int index = 0; index < commandCount; index++)
        {
            var command = new CommandEnvelope(
                reader.ReadStableId(),
                reader.ReadStableId(),
                reader.ReadUInt64(),
                (CommandOriginPriority)reader.ReadInt32(),
                reader.ReadUInt64(),
                reader.ReadRequiredString(),
                reader.ReadInt32(),
                reader.ReadBytes(),
                reader.ReadChecksum());
            if (!CommandEnvelopeFactory.IsValid(command))
            {
                throw new InvalidDataException($"Snapshot command '{command.CommandId}' is invalid.");
            }

            if (command.TargetTick <= completedTick)
            {
                throw new InvalidDataException($"Snapshot command '{command.CommandId}' does not target a future tick.");
            }

            if (previousCommand is not null && CommandEnvelopeCanonicalComparer.Instance.Compare(previousCommand, command) >= 0)
            {
                throw new InvalidDataException("Snapshot commands are not in strict canonical queue order.");
            }

            previousCommand = command;
            commands.Add(command);
        }

        if (commands.Count > 0 && nextCommandSequence <= commands.Max(command => command.Sequence))
        {
            throw new InvalidDataException("Snapshot next command sequence would collide with a pending command.");
        }

        StateChecksum stateChecksum = reader.ReadChecksum();
        reader.EnsureComplete();
        return new SimulationSnapshot(
            formatVersion,
            campaignSeed,
            ticksPerSecond,
            completedTick,
            timeScale,
            nextCommandSequence,
            nextEventSequence,
            new EntityRegistrySnapshot(entityMutationVersion, entities),
            new SimulationStateStoreSnapshot(stateMutationVersion, values),
            commands,
            stateChecksum);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            int read = stream.Read(destination);
            if (read == 0)
            {
                throw new EndOfStreamException("Snapshot ended unexpectedly.");
            }

            destination = destination[read..];
        }
    }
}
