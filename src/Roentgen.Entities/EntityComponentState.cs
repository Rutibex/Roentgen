using System.Security.Cryptography;
using Roentgen.Core;

namespace Roentgen.Entities;

public sealed class EntityComponentState : ICanonicalWritable
{
    private readonly byte[] _payload;

    private EntityComponentState(string typeId, int schemaVersion, byte[] payload, StateChecksum payloadChecksum)
    {
        TypeId = typeId;
        SchemaVersion = schemaVersion;
        _payload = payload;
        PayloadChecksum = payloadChecksum;
    }

    public string TypeId { get; }
    public int SchemaVersion { get; }
    public ReadOnlyMemory<byte> Payload => _payload;
    public StateChecksum PayloadChecksum { get; }

    public static EntityComponentState Create(string typeId, int schemaVersion, Action<CanonicalWriter> writePayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(writePayload);
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        var writer = new CanonicalWriter();
        writePayload(writer);
        return FromPayload(typeId, schemaVersion, writer.ToArray());
    }

    public static EntityComponentState FromPayload(string typeId, int schemaVersion, ReadOnlySpan<byte> payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        byte[] copy = payload.ToArray();
        StateChecksum checksum = StateChecksum.FromBytes(SHA256.HashData(copy));
        return new EntityComponentState(typeId, schemaVersion, copy, checksum);
    }

    public static EntityComponentState Restore(
        string typeId,
        int schemaVersion,
        ReadOnlySpan<byte> payload,
        StateChecksum expectedChecksum)
    {
        EntityComponentState component = FromPayload(typeId, schemaVersion, payload);
        if (component.PayloadChecksum != expectedChecksum)
        {
            throw new InvalidDataException($"Component '{typeId}' payload checksum is invalid.");
        }

        return component;
    }

    public CanonicalReader OpenReader() => new(_payload);

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString(TypeId);
        writer.WriteInt32(SchemaVersion);
        writer.WriteBytes(_payload);
        writer.WriteChecksum(PayloadChecksum);
    }
}
