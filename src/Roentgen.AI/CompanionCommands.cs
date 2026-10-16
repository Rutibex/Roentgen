using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.AI;

public enum CompanionCommandKind
{
    Follow = 0,
    Stay = 1,
    MoveTo = 2,
    Regroup = 3,
    AttackTarget = 4,
    AvoidCombat = 5,
    PreferRanged = 6,
    PreferMelee = 7,
    TakeCover = 8,
    HealSelf = 9,
    HealPlayer = 10,
    Interact = 11,
    WaitUntil = 12
}

public enum CompanionCommandStatus
{
    Queued = 0,
    Accepted = 1,
    Rejected = 2,
    Active = 3,
    Completed = 4,
    Cancelled = 5
}

public sealed record CompanionCommand(
    StableId CommandId,
    StableId CompanionId,
    StableId IssuerId,
    CompanionCommandKind Kind,
    StableId TargetId,
    FixedVector3 TargetPosition,
    ulong IssuedTick,
    ulong ExpiresTick,
    string Reason) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(CommandId); writer.WriteStableId(CompanionId); writer.WriteStableId(IssuerId); writer.WriteInt32((int)Kind);
        writer.WriteStableId(TargetId); writer.WriteInt64(TargetPosition.XMillimeters); writer.WriteInt64(TargetPosition.YMillimeters); writer.WriteInt64(TargetPosition.ZMillimeters);
        writer.WriteUInt64(IssuedTick); writer.WriteUInt64(ExpiresTick); writer.WriteString(Reason);
    }
}

public sealed record CompanionCommandResult(StableId CommandId, CompanionCommandStatus Status, string Message, ulong Tick);

public sealed class CompanionCommandService
{
    private readonly SortedDictionary<StableId, CompanionCommand> active = [];
    private readonly List<CompanionCommandResult> history = [];
    public IReadOnlyDictionary<StableId, CompanionCommand> ActiveCommands => active;
    public IReadOnlyList<CompanionCommandResult> History => history;

    public CompanionCommandResult Issue(CompanionCommand command, AgentBrainRegistry brains, ulong tick)
    {
        if (command.CommandId.IsEmpty || command.CompanionId.IsEmpty || command.IssuerId.IsEmpty) return Record(command.CommandId, CompanionCommandStatus.Rejected, "command identity is incomplete", tick);
        if (!brains.TryGet(command.CompanionId, out AgentBrainState? brain) || brain is null) return Record(command.CommandId, CompanionCommandStatus.Rejected, "companion is unavailable", tick);
        if (brain.IsSurrendered) return Record(command.CommandId, CompanionCommandStatus.Rejected, "companion cannot act while surrendered", tick);
        active[command.CompanionId] = command;
        brain.CurrentDestinationId = command.TargetId; brain.CurrentDestination = command.TargetPosition; brain.Revision++;
        return Record(command.CommandId, CompanionCommandStatus.Accepted, "command accepted", tick);
    }

    public bool TryGet(StableId companionId, out CompanionCommand? command) => active.TryGetValue(companionId, out command);

    public CompanionCommandResult Complete(StableId companionId, ulong tick, string message = "command completed")
    {
        if (!active.Remove(companionId, out CompanionCommand? command)) return Record(StableId.Empty, CompanionCommandStatus.Rejected, "no active companion command", tick);
        return Record(command.CommandId, CompanionCommandStatus.Completed, message, tick);
    }

    public CompanionCommandResult Fail(StableId companionId, ulong tick, string message)
    {
        if (!active.Remove(companionId, out CompanionCommand? command)) return Record(StableId.Empty, CompanionCommandStatus.Rejected, "no active companion command", tick);
        return Record(command.CommandId, CompanionCommandStatus.Rejected, message, tick);
    }

    public void Expire(ulong tick)
    {
        foreach ((StableId companionId, CompanionCommand command) in active.Where(value => value.Value.ExpiresTick > 0 && value.Value.ExpiresTick <= tick).ToArray())
        {
            active.Remove(companionId);
            Record(command.CommandId, CompanionCommandStatus.Cancelled, "command expired", tick);
        }
    }

    private CompanionCommandResult Record(StableId commandId, CompanionCommandStatus status, string message, ulong tick)
    {
        var result = new CompanionCommandResult(commandId, status, message, tick); history.Add(result); if (history.Count > 128) history.RemoveRange(0, history.Count - 128); return result;
    }
}
