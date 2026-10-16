using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Simulation;

public enum CommandOriginPriority
{
    Engine = 0,
    Replay = 100,
    Network = 200,
    Player = 300,
    Development = 900
}

public interface ISimulationCommand
{
    string TypeId { get; }
    int SchemaVersion { get; }
    void WritePayload(CanonicalWriter writer);
}

public sealed record CommandEnvelope(
    StableId CommandId,
    StableId IssuerId,
    ulong TargetTick,
    CommandOriginPriority OriginPriority,
    ulong Sequence,
    string TypeId,
    int SchemaVersion,
    byte[] Payload,
    StateChecksum PayloadChecksum);

public static class CommandEnvelopeFactory
{
    public static CommandEnvelope Create(
        StableId issuerId,
        ulong targetTick,
        CommandOriginPriority originPriority,
        ulong sequence,
        ISimulationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.TypeId);
        if (command.SchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Command schema version must be positive.");
        }

        var payloadWriter = new CanonicalWriter();
        command.WritePayload(payloadWriter);
        byte[] payload = payloadWriter.ToArray();
        StateChecksum payloadChecksum = payloadWriter.ComputeChecksum();

        StableId commandId = ComputeId(
            issuerId,
            targetTick,
            originPriority,
            sequence,
            command.TypeId,
            command.SchemaVersion,
            payloadChecksum);

        return new CommandEnvelope(
            commandId,
            issuerId,
            targetTick,
            originPriority,
            sequence,
            command.TypeId,
            command.SchemaVersion,
            payload,
            payloadChecksum);
    }
    public static bool IsValid(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.TypeId) || command.SchemaVersion <= 0)
        {
            return false;
        }

        StateChecksum actualPayloadChecksum = StateChecksum.FromBytes(System.Security.Cryptography.SHA256.HashData(command.Payload));
        if (actualPayloadChecksum != command.PayloadChecksum)
        {
            return false;
        }

        StableId expectedId = ComputeId(
            command.IssuerId,
            command.TargetTick,
            command.OriginPriority,
            command.Sequence,
            command.TypeId,
            command.SchemaVersion,
            command.PayloadChecksum);
        return expectedId == command.CommandId;
    }

    private static StableId ComputeId(
        StableId issuerId,
        ulong targetTick,
        CommandOriginPriority originPriority,
        ulong sequence,
        string typeId,
        int schemaVersion,
        StateChecksum payloadChecksum)
    {
        var idWriter = new CanonicalWriter();
        idWriter.WriteString("roentgen.command");
        idWriter.WriteStableId(issuerId);
        idWriter.WriteUInt64(targetTick);
        idWriter.WriteInt32((int)originPriority);
        idWriter.WriteUInt64(sequence);
        idWriter.WriteString(typeId);
        idWriter.WriteInt32(schemaVersion);
        idWriter.WriteChecksum(payloadChecksum);
        StateChecksum idHash = idWriter.ComputeChecksum();
        return new StableId(idHash.A, idHash.B);
    }

}

public interface ICommandHandler
{
    string HandlerId { get; }
    string CommandTypeId { get; }
    int MinimumSchemaVersion { get; }
    int MaximumSchemaVersion { get; }
    void Handle(CommandEnvelope command, SimulationTickContext context);
}
