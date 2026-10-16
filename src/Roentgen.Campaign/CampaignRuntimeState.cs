using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Campaign;

public sealed record CampaignMilestoneRuntimeState(
    StableId MilestoneId,
    CampaignMilestoneStatus Status,
    ulong EligibleTick,
    ulong OfferedTick,
    ulong ResolvedTick,
    string ResolutionId,
    int RecoveryCount) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(MilestoneId);
        writer.WriteInt32((int)Status);
        writer.WriteUInt64(EligibleTick);
        writer.WriteUInt64(OfferedTick);
        writer.WriteUInt64(ResolvedTick);
        writer.WriteString(ResolutionId);
        writer.WriteInt32(RecoveryCount);
    }

    public static CampaignMilestoneRuntimeState ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), (CampaignMilestoneStatus)reader.ReadInt32(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadRequiredString(), reader.ReadInt32());
}

public sealed record RevelationRuntimeState(
    StableId RevelationId,
    RevelationStatus Status,
    IReadOnlySet<StableId> CollectedEvidenceIds,
    ulong ChangedTick,
    StableId DiscoveredByActorId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(RevelationId);
        writer.WriteInt32((int)Status);
        CampaignCanonical.WriteIds(writer, CollectedEvidenceIds);
        writer.WriteUInt64(ChangedTick);
        writer.WriteStableId(DiscoveredByActorId);
    }

    public static RevelationRuntimeState ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), (RevelationStatus)reader.ReadInt32(), new SortedSet<StableId>(CampaignCanonical.ReadIds(reader)), reader.ReadUInt64(), reader.ReadStableId());
}

public sealed record CampaignCommitmentRecord(
    StableId Id,
    StableId ChoiceId,
    string OptionId,
    StableId ResponsibleActorId,
    ulong Tick,
    ulong Day,
    IReadOnlyList<StableId> KnownFactIds,
    IReadOnlySet<string> AddedFlags,
    IReadOnlySet<string> RemovedFlags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteStableId(ChoiceId);
        writer.WriteString(OptionId);
        writer.WriteStableId(ResponsibleActorId);
        writer.WriteUInt64(Tick);
        writer.WriteUInt64(Day);
        CampaignCanonical.WriteIds(writer, KnownFactIds);
        CampaignCanonical.WriteStrings(writer, AddedFlags);
        CampaignCanonical.WriteStrings(writer, RemovedFlags);
    }

    public static CampaignCommitmentRecord ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadUInt64(), reader.ReadUInt64(), CampaignCanonical.ReadIds(reader), CampaignCanonical.ReadStringSet(reader), CampaignCanonical.ReadStringSet(reader));
}

public sealed record CriticalRoleRuntimeState(
    StableId AssignmentId,
    string RoleId,
    StableId CurrentEntityId,
    int FallbackIndex,
    bool Available,
    ulong ChangedTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(AssignmentId);
        writer.WriteString(RoleId);
        writer.WriteStableId(CurrentEntityId);
        writer.WriteInt32(FallbackIndex);
        writer.WriteBoolean(Available);
        writer.WriteUInt64(ChangedTick);
    }

    public static CriticalRoleRuntimeState ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadInt32(), reader.ReadBoolean(), reader.ReadUInt64());
}

public sealed record CriticalRoleReplacementRecord(
    StableId Id,
    string RoleId,
    StableId OriginalEntityId,
    StableId ReplacementEntityId,
    CriticalRoleFallbackKind FallbackKind,
    ulong Tick,
    string Reason) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(RoleId);
        writer.WriteStableId(OriginalEntityId);
        writer.WriteStableId(ReplacementEntityId);
        writer.WriteInt32((int)FallbackKind);
        writer.WriteUInt64(Tick);
        writer.WriteString(Reason);
    }

    public static CriticalRoleReplacementRecord ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadStableId(), (CriticalRoleFallbackKind)reader.ReadInt32(), reader.ReadUInt64(), reader.ReadRequiredString());
}

public sealed record CampaignEpilogueRecord(
    StableId Id,
    StableId RuleId,
    string SubjectKind,
    StableId SubjectId,
    string Text,
    IReadOnlyList<string> SupportingReasons,
    int Order) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteStableId(RuleId);
        writer.WriteString(SubjectKind);
        writer.WriteStableId(SubjectId);
        writer.WriteString(Text);
        CampaignCanonical.WriteStrings(writer, SupportingReasons);
        writer.WriteInt32(Order);
    }

    public static CampaignEpilogueRecord ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadRequiredString(), CampaignCanonical.ReadStrings(reader), reader.ReadInt32());
}

public sealed record CampaignEventRecord(
    StableId Id,
    CampaignEventKind Kind,
    ulong Tick,
    StableId SubjectId,
    StableId SecondaryId,
    string Description) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteInt32((int)Kind);
        writer.WriteUInt64(Tick);
        writer.WriteStableId(SubjectId);
        writer.WriteStableId(SecondaryId);
        writer.WriteString(Description);
    }

    public static CampaignEventRecord ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), (CampaignEventKind)reader.ReadInt32(), reader.ReadUInt64(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadRequiredString());
}

public sealed class CampaignWorldState : ICanonicalWritable
{
    public const int SchemaVersion = 17;
    private readonly SortedDictionary<StableId, CampaignMilestoneRuntimeState> milestones = [];
    private readonly SortedDictionary<StableId, RevelationRuntimeState> revelations = [];
    private readonly SortedDictionary<StableId, CampaignCommitmentRecord> commitments = [];
    private readonly SortedDictionary<string, CriticalRoleRuntimeState> criticalRoles = new(StringComparer.Ordinal);
    private readonly SortedDictionary<StableId, CriticalRoleReplacementRecord> replacements = [];
    private readonly SortedDictionary<StableId, CampaignEpilogueRecord> epilogues = [];
    private readonly SortedSet<string> flags = new(StringComparer.Ordinal);
    private readonly SortedSet<StableId> consumedEventIds = [];
    private readonly Queue<CampaignEventRecord> events = new();

    public CampaignWorldState(GeneratedCampaignPlan plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        foreach (GeneratedMilestonePlan milestone in plan.Milestones)
            milestones[milestone.Id] = new CampaignMilestoneRuntimeState(milestone.Id, CampaignMilestoneStatus.Planned, 0, 0, 0, string.Empty, 0);
        foreach (RevelationDefinition revelation in plan.Revelations)
            revelations[revelation.Id] = new RevelationRuntimeState(revelation.Id, RevelationStatus.Hidden, new SortedSet<StableId>(), 0, StableId.Empty);
        foreach (CriticalRoleAssignmentPlan assignment in plan.CriticalRoleAssignments)
            criticalRoles[assignment.RoleId] = new CriticalRoleRuntimeState(assignment.Id, assignment.RoleId, assignment.AssignedEntityId, -1, true, 0);
    }

    public GeneratedCampaignPlan Plan { get; }
    public IReadOnlyCollection<CampaignMilestoneRuntimeState> Milestones => milestones.Values;
    public IReadOnlyCollection<RevelationRuntimeState> Revelations => revelations.Values;
    public IReadOnlyCollection<CampaignCommitmentRecord> Commitments => commitments.Values;
    public IReadOnlyCollection<CriticalRoleRuntimeState> CriticalRoles => criticalRoles.Values;
    public IReadOnlyCollection<CriticalRoleReplacementRecord> Replacements => replacements.Values;
    public IReadOnlyCollection<CampaignEpilogueRecord> Epilogues => epilogues.Values;
    public IReadOnlySet<string> Flags => flags;
    public IReadOnlySet<StableId> ConsumedEventIds => consumedEventIds;
    public IReadOnlyCollection<CampaignEventRecord> Events => events.ToArray();
    public ulong Tick { get; private set; }
    public ulong Day { get; private set; }
    public ConvergenceForm? ConvergenceResolution { get; private set; }
    public StableId SelectedEndingId { get; private set; }
    public bool Completed { get; private set; }
    public ulong Revision { get; private set; }

    public CampaignMilestoneRuntimeState GetMilestone(StableId id) => milestones.TryGetValue(id, out CampaignMilestoneRuntimeState? value) ? value : throw new KeyNotFoundException($"Unknown milestone '{id}'.");
    public RevelationRuntimeState GetRevelation(StableId id) => revelations.TryGetValue(id, out RevelationRuntimeState? value) ? value : throw new KeyNotFoundException($"Unknown revelation '{id}'.");
    public CampaignCommitmentRecord? FindCommitment(StableId choiceId) => commitments.Values.FirstOrDefault(value => value.ChoiceId == choiceId);
    public CriticalRoleRuntimeState GetCriticalRole(string roleId) => criticalRoles.TryGetValue(roleId, out CriticalRoleRuntimeState? value) ? value : throw new KeyNotFoundException($"Unknown critical role '{roleId}'.");

    public void SetTime(ulong tick, ulong day) { Tick = tick; Day = day; Revision++; }
    public void SetMilestone(CampaignMilestoneRuntimeState value) { milestones[value.MilestoneId] = value; Revision++; }
    public void SetRevelation(RevelationRuntimeState value) { revelations[value.RevelationId] = value; Revision++; }
    public bool AddCommitment(CampaignCommitmentRecord value)
    {
        if (commitments.Values.Any(existing => existing.ChoiceId == value.ChoiceId)) return false;
        commitments[value.Id] = value;
        Revision++;
        return true;
    }
    public void SetCriticalRole(CriticalRoleRuntimeState value) { criticalRoles[value.RoleId] = value; Revision++; }
    public void AddReplacement(CriticalRoleReplacementRecord value) { replacements[value.Id] = value; Revision++; }
    public bool AddFlag(string value) { bool added = flags.Add(value); if (added) Revision++; return added; }
    public bool RemoveFlag(string value) { bool removed = flags.Remove(value); if (removed) Revision++; return removed; }
    public void SetConvergence(ConvergenceForm value) { ConvergenceResolution = value; Revision++; }
    public void SetEnding(StableId endingId)
    {
        SelectedEndingId = endingId;
        Completed = !endingId.IsEmpty;
        if (!endingId.IsEmpty) flags.Add($"ending:{endingId}");
        Revision++;
    }
    public void AddEpilogue(CampaignEpilogueRecord value) { epilogues[value.Id] = value; Revision++; }
    public bool ConsumeEvent(StableId id) { bool added = consumedEventIds.Add(id); if (added) Revision++; return added; }
    public void RecordEvent(CampaignEventRecord value)
    {
        events.Enqueue(value);
        while (events.Count > 512) _ = events.Dequeue();
        Revision++;
    }

    public byte[] ToCanonicalPayload()
    {
        var writer = new CanonicalWriter();
        WriteCanonical(writer);
        return writer.ToArray();
    }

    public StateChecksum ComputeChecksum()
    {
        var writer = new CanonicalWriter();
        WriteCanonical(writer);
        return writer.ComputeChecksum();
    }

    public void RestoreFrom(ReadOnlyMemory<byte> payload)
    {
        CampaignWorldState source = ReadCanonical(payload);
        if (source.Plan.Id != Plan.Id || source.Plan.Fingerprint != Plan.Fingerprint) throw new InvalidDataException("Campaign state belongs to a different generated plan.");
        milestones.Clear(); foreach ((StableId id, CampaignMilestoneRuntimeState value) in source.milestones) milestones.Add(id, value);
        revelations.Clear(); foreach ((StableId id, RevelationRuntimeState value) in source.revelations) revelations.Add(id, value);
        commitments.Clear(); foreach ((StableId id, CampaignCommitmentRecord value) in source.commitments) commitments.Add(id, value);
        criticalRoles.Clear(); foreach ((string id, CriticalRoleRuntimeState value) in source.criticalRoles) criticalRoles.Add(id, value);
        replacements.Clear(); foreach ((StableId id, CriticalRoleReplacementRecord value) in source.replacements) replacements.Add(id, value);
        epilogues.Clear(); foreach ((StableId id, CampaignEpilogueRecord value) in source.epilogues) epilogues.Add(id, value);
        flags.Clear(); foreach (string value in source.flags) flags.Add(value);
        consumedEventIds.Clear(); foreach (StableId value in source.consumedEventIds) consumedEventIds.Add(value);
        events.Clear(); foreach (CampaignEventRecord value in source.events) events.Enqueue(value);
        Tick = source.Tick; Day = source.Day; ConvergenceResolution = source.ConvergenceResolution; SelectedEndingId = source.SelectedEndingId; Completed = source.Completed; Revision = source.Revision;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32(SchemaVersion);
        Plan.WriteCanonical(writer);
        writer.WriteUInt64(Tick);
        writer.WriteUInt64(Day);
        writer.WriteCount(milestones.Count); foreach (CampaignMilestoneRuntimeState value in milestones.Values) value.WriteCanonical(writer);
        writer.WriteCount(revelations.Count); foreach (RevelationRuntimeState value in revelations.Values) value.WriteCanonical(writer);
        writer.WriteCount(commitments.Count); foreach (CampaignCommitmentRecord value in commitments.Values) value.WriteCanonical(writer);
        writer.WriteCount(criticalRoles.Count); foreach (CriticalRoleRuntimeState value in criticalRoles.Values) value.WriteCanonical(writer);
        writer.WriteCount(replacements.Count); foreach (CriticalRoleReplacementRecord value in replacements.Values) value.WriteCanonical(writer);
        writer.WriteCount(epilogues.Count); foreach (CampaignEpilogueRecord value in epilogues.Values) value.WriteCanonical(writer);
        CampaignCanonical.WriteStrings(writer, flags);
        CampaignCanonical.WriteIds(writer, consumedEventIds);
        writer.WriteCount(events.Count); foreach (CampaignEventRecord value in events) value.WriteCanonical(writer);
        writer.WriteBoolean(ConvergenceResolution.HasValue); if (ConvergenceResolution.HasValue) writer.WriteInt32((int)ConvergenceResolution.Value);
        writer.WriteStableId(SelectedEndingId);
        writer.WriteBoolean(Completed);
        writer.WriteUInt64(Revision);
    }

    public static CampaignWorldState ReadCanonical(ReadOnlyMemory<byte> payload)
    {
        var reader = new CanonicalReader(payload);
        CampaignWorldState result = ReadCanonical(reader);
        reader.EnsureComplete();
        return result;
    }

    public static CampaignWorldState ReadCanonical(CanonicalReader reader)
    {
        int schema = reader.ReadInt32();
        if (schema != SchemaVersion) throw new InvalidDataException($"Unsupported campaign state schema {schema}; expected {SchemaVersion}.");
        GeneratedCampaignPlan plan = GeneratedCampaignPlan.ReadCanonical(reader);
        var result = new CampaignWorldState(plan) { Tick = reader.ReadUInt64(), Day = reader.ReadUInt64() };
        result.milestones.Clear(); int milestoneCount = reader.ReadCount(); for (int index = 0; index < milestoneCount; index++) { CampaignMilestoneRuntimeState value = CampaignMilestoneRuntimeState.ReadCanonical(reader); result.milestones.Add(value.MilestoneId, value); }
        result.revelations.Clear(); int revelationCount = reader.ReadCount(); for (int index = 0; index < revelationCount; index++) { RevelationRuntimeState value = RevelationRuntimeState.ReadCanonical(reader); result.revelations.Add(value.RevelationId, value); }
        result.commitments.Clear(); int commitmentCount = reader.ReadCount(); for (int index = 0; index < commitmentCount; index++) { CampaignCommitmentRecord value = CampaignCommitmentRecord.ReadCanonical(reader); result.commitments.Add(value.Id, value); }
        result.criticalRoles.Clear(); int roleCount = reader.ReadCount(); for (int index = 0; index < roleCount; index++) { CriticalRoleRuntimeState value = CriticalRoleRuntimeState.ReadCanonical(reader); result.criticalRoles.Add(value.RoleId, value); }
        result.replacements.Clear(); int replacementCount = reader.ReadCount(); for (int index = 0; index < replacementCount; index++) { CriticalRoleReplacementRecord value = CriticalRoleReplacementRecord.ReadCanonical(reader); result.replacements.Add(value.Id, value); }
        result.epilogues.Clear(); int epilogueCount = reader.ReadCount(); for (int index = 0; index < epilogueCount; index++) { CampaignEpilogueRecord value = CampaignEpilogueRecord.ReadCanonical(reader); result.epilogues.Add(value.Id, value); }
        result.flags.Clear(); foreach (string value in CampaignCanonical.ReadStrings(reader)) result.flags.Add(value);
        result.consumedEventIds.Clear(); foreach (StableId value in CampaignCanonical.ReadIds(reader)) result.consumedEventIds.Add(value);
        result.events.Clear(); int eventCount = reader.ReadCount(); for (int index = 0; index < eventCount; index++) result.events.Enqueue(CampaignEventRecord.ReadCanonical(reader));
        result.ConvergenceResolution = reader.ReadBoolean() ? (ConvergenceForm)reader.ReadInt32() : null;
        result.SelectedEndingId = reader.ReadStableId();
        result.Completed = reader.ReadBoolean();
        result.Revision = reader.ReadUInt64();
        return result;
    }
}
