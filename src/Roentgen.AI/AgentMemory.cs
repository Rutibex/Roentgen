using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.AI;

public enum AwarenessLevel
{
    Unaware = 0,
    Suspicious = 1,
    Investigating = 2,
    Alerted = 3,
    Confirmed = 4
}

public enum AgentSimulationTier
{
    Full = 0,
    Reduced = 1,
    Strategic = 2
}

public enum AgentDisposition
{
    Calm = 0,
    Guarded = 1,
    Aggressive = 2,
    Fearful = 3,
    Surrendered = 4,
    Fleeing = 5
}

public sealed class AgentAwarenessMemory : ICanonicalWritable
{
    public StableId ConfirmedTargetId { get; private set; }
    public FixedVector3 LastKnownPosition { get; private set; }
    public FixedVector3 LastHeardPosition { get; private set; }
    public int ConfidencePermille { get; private set; }
    public int SuspicionPermille { get; private set; }
    public ulong LastEvidenceTick { get; private set; }
    public ulong LastDecayTick { get; private set; }
    public AwarenessLevel Level { get; private set; }
    public ulong Revision { get; private set; }

    public void Confirm(StableId targetId, FixedVector3 position, int confidencePermille, ulong tick)
    {
        ConfirmedTargetId = targetId;
        LastKnownPosition = position;
        ConfidencePermille = Math.Clamp(confidencePermille, 0, 1000);
        SuspicionPermille = Math.Max(SuspicionPermille, ConfidencePermille);
        LastEvidenceTick = tick;
        LastDecayTick = tick;
        Level = ConfidencePermille >= 750 ? AwarenessLevel.Confirmed : AwarenessLevel.Alerted;
        Revision++;
    }

    public void Hear(FixedVector3 position, int intensityPermille, ulong tick)
    {
        LastHeardPosition = position;
        SuspicionPermille = Math.Clamp(Math.Max(SuspicionPermille, intensityPermille), 0, 1000);
        LastEvidenceTick = tick;
        LastDecayTick = tick;
        if (Level < AwarenessLevel.Investigating) Level = intensityPermille >= 650 ? AwarenessLevel.Investigating : AwarenessLevel.Suspicious;
        Revision++;
    }

    public void Share(AgentAwarenessMemory source, int transferPermille, ulong tick)
    {
        int confidence = source.ConfidencePermille * Math.Clamp(transferPermille, 0, 1000) / 1000;
        if (!source.ConfirmedTargetId.IsEmpty && confidence > ConfidencePermille)
            Confirm(source.ConfirmedTargetId, source.LastKnownPosition, confidence, tick);
        else if (source.SuspicionPermille > SuspicionPermille)
            Hear(source.LastHeardPosition, source.SuspicionPermille * transferPermille / 1000, tick);
    }

    public void CopyFrom(AgentAwarenessMemory source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ConfirmedTargetId = source.ConfirmedTargetId;
        LastKnownPosition = source.LastKnownPosition;
        LastHeardPosition = source.LastHeardPosition;
        ConfidencePermille = source.ConfidencePermille;
        SuspicionPermille = source.SuspicionPermille;
        LastEvidenceTick = source.LastEvidenceTick;
        LastDecayTick = source.LastDecayTick;
        Level = source.Level;
        Revision++;
    }

    public void Decay(ulong tick, int decayPerSecondPermille = 24)
    {
        if (tick <= LastDecayTick) return;
        ulong elapsedSeconds = (tick - LastDecayTick) / 60;
        if (elapsedSeconds == 0) return;
        int decay = checked((int)Math.Min(1000UL, elapsedSeconds * (ulong)Math.Max(0, decayPerSecondPermille)));
        ConfidencePermille = Math.Max(0, ConfidencePermille - decay);
        SuspicionPermille = Math.Max(0, SuspicionPermille - decay / 2);
        Level = ConfidencePermille >= 750 ? AwarenessLevel.Confirmed : ConfidencePermille >= 300 ? AwarenessLevel.Alerted : SuspicionPermille >= 350 ? AwarenessLevel.Investigating : SuspicionPermille > 0 ? AwarenessLevel.Suspicious : AwarenessLevel.Unaware;
        if (Level == AwarenessLevel.Unaware) ConfirmedTargetId = StableId.Empty;
        LastDecayTick = checked(LastDecayTick + elapsedSeconds * 60UL);
        Revision++;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(ConfirmedTargetId);
        WriteVector(writer, LastKnownPosition);
        WriteVector(writer, LastHeardPosition);
        writer.WriteInt32(ConfidencePermille);
        writer.WriteInt32(SuspicionPermille);
        writer.WriteUInt64(LastEvidenceTick);
        writer.WriteUInt64(LastDecayTick);
        writer.WriteInt32((int)Level);
        writer.WriteUInt64(Revision);
    }

    private static void WriteVector(CanonicalWriter writer, FixedVector3 value)
    {
        writer.WriteInt64(value.XMillimeters); writer.WriteInt64(value.YMillimeters); writer.WriteInt64(value.ZMillimeters);
    }
}

public sealed class AgentBrainState : ICanonicalWritable
{
    public AgentBrainState(StableId agentId, string roleId, StableId factionId)
    {
        if (agentId.IsEmpty) throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        if (factionId.IsEmpty) throw new ArgumentException("Faction ID cannot be empty.", nameof(factionId));
        AgentId = agentId; RoleId = roleId; FactionId = factionId;
    }

    public AgentBrainState(StableId agentId, string roleId, string factionId)
        : this(agentId, roleId, StableIdFactory.Create("faction.legacy", factionId))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factionId);
    }

    public StableId AgentId { get; }
    public string RoleId { get; }
    public StableId FactionId { get; }
    public AgentAwarenessMemory Awareness { get; } = new();
    public AgentSimulationTier Tier { get; set; } = AgentSimulationTier.Full;
    public AgentDisposition Disposition { get; set; } = AgentDisposition.Calm;
    public string CurrentActionId { get; set; } = "idle";
    public ulong ActionCommittedUntilTick { get; set; }
    public ulong LastActionChangeTick { get; set; }
    public int HungerPermille { get; set; }
    public int FatiguePermille { get; set; }
    public int FearPermille { get; set; }
    public int DutyPermille { get; set; } = 700;
    public bool IsSurrendered { get; set; }
    public bool HasRaisedAlarm { get; set; }
    public StableId GroupId { get; set; }
    public StableId CurrentDestinationId { get; set; }
    public FixedVector3 CurrentDestination { get; set; }
    public ulong Revision { get; set; }

    public void CommitAction(string actionId, ulong tick, int minimumTicks)
    {
        CurrentActionId = actionId;
        LastActionChangeTick = tick;
        ActionCommittedUntilTick = checked(tick + (ulong)Math.Max(0, minimumTicks));
        Revision++;
    }

    public void CopyDynamicStateFrom(AgentBrainState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Awareness.CopyFrom(source.Awareness);
        Tier = source.Tier;
        Disposition = source.Disposition;
        CurrentActionId = source.CurrentActionId;
        ActionCommittedUntilTick = source.ActionCommittedUntilTick;
        LastActionChangeTick = source.LastActionChangeTick;
        HungerPermille = source.HungerPermille;
        FatiguePermille = source.FatiguePermille;
        FearPermille = source.FearPermille;
        DutyPermille = source.DutyPermille;
        IsSurrendered = source.IsSurrendered;
        HasRaisedAlarm = source.HasRaisedAlarm;
        GroupId = source.GroupId;
        CurrentDestinationId = source.CurrentDestinationId;
        CurrentDestination = source.CurrentDestination;
        Revision++;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(AgentId); writer.WriteString(RoleId); writer.WriteStableId(FactionId); Awareness.WriteCanonical(writer);
        writer.WriteInt32((int)Tier); writer.WriteInt32((int)Disposition); writer.WriteString(CurrentActionId);
        writer.WriteUInt64(ActionCommittedUntilTick); writer.WriteUInt64(LastActionChangeTick); writer.WriteInt32(HungerPermille);
        writer.WriteInt32(FatiguePermille); writer.WriteInt32(FearPermille); writer.WriteInt32(DutyPermille); writer.WriteBoolean(IsSurrendered);
        writer.WriteBoolean(HasRaisedAlarm); writer.WriteStableId(GroupId); writer.WriteStableId(CurrentDestinationId);
        writer.WriteInt64(CurrentDestination.XMillimeters); writer.WriteInt64(CurrentDestination.YMillimeters); writer.WriteInt64(CurrentDestination.ZMillimeters);
        writer.WriteUInt64(Revision);
    }
}

public sealed class AgentBrainRegistry : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, AgentBrainState> agents = [];
    public IReadOnlyCollection<AgentBrainState> Agents => agents.Values;
    public ulong Revision { get; private set; }
    public void Add(AgentBrainState agent) { if (!agents.TryAdd(agent.AgentId, agent)) throw new InvalidOperationException($"Agent '{agent.AgentId}' already exists."); Revision++; }
    public AgentBrainState Get(StableId id) => agents.TryGetValue(id, out AgentBrainState? value) ? value : throw new KeyNotFoundException($"Unknown AI agent '{id}'.");
    public bool TryGet(StableId id, out AgentBrainState? value) => agents.TryGetValue(id, out value);
    public bool Remove(StableId id) { bool changed = agents.Remove(id); if (changed) Revision++; return changed; }
    public StateChecksum ComputeChecksum() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ComputeChecksum(); }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteUInt64(Revision); writer.WriteCount(agents.Count); foreach (AgentBrainState agent in agents.Values) agent.WriteCanonical(writer); }
}
