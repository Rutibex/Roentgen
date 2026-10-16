using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Narrative;
using Roentgen.World;

namespace Roentgen.Campaign;

public sealed record HistoricalFactDefinition(
    string PredicateId,
    StableId SubjectId,
    NarrativeValue Object,
    FactTruthState Truth,
    FactScope Scope) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(PredicateId);
        writer.WriteStableId(SubjectId);
        Object.WriteCanonical(writer);
        writer.WriteInt32((int)Truth);
        writer.WriteInt32((int)Scope);
    }

    public static HistoricalFactDefinition ReadCanonical(CanonicalReader reader) => new(
        reader.ReadRequiredString(),
        reader.ReadStableId(),
        NarrativeValue.ReadCanonical(reader),
        (FactTruthState)reader.ReadInt32(),
        (FactScope)reader.ReadInt32());
}

public sealed record HistoricalEventRecord(
    StableId Id,
    HistoricalEventKind Kind,
    RegionFamily Region,
    ulong Day,
    StableId LocationId,
    IReadOnlyList<StableId> ParticipantIds,
    IReadOnlyList<StableId> CauseEventIds,
    string Summary,
    int PublicVisibilityPermille,
    IReadOnlyList<StableId> EvidenceIds,
    IReadOnlySet<string> Tags,
    IReadOnlyList<HistoricalFactDefinition> DerivedFacts) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteInt32((int)Kind);
        writer.WriteInt32((int)Region);
        writer.WriteUInt64(Day);
        writer.WriteStableId(LocationId);
        CampaignCanonical.WriteIds(writer, ParticipantIds);
        CampaignCanonical.WriteIds(writer, CauseEventIds);
        writer.WriteString(Summary);
        writer.WriteInt32(PublicVisibilityPermille);
        CampaignCanonical.WriteIds(writer, EvidenceIds);
        CampaignCanonical.WriteStrings(writer, Tags);
        writer.WriteCount(DerivedFacts.Count);
        foreach (HistoricalFactDefinition fact in DerivedFacts.OrderBy(static value => value.PredicateId, StringComparer.Ordinal).ThenBy(static value => value.SubjectId)) fact.WriteCanonical(writer);
    }

    public static HistoricalEventRecord ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        HistoricalEventKind kind = (HistoricalEventKind)reader.ReadInt32();
        RegionFamily region = (RegionFamily)reader.ReadInt32();
        ulong day = reader.ReadUInt64();
        StableId location = reader.ReadStableId();
        StableId[] participants = CampaignCanonical.ReadIds(reader);
        StableId[] causes = CampaignCanonical.ReadIds(reader);
        string summary = reader.ReadRequiredString();
        int visibility = reader.ReadInt32();
        StableId[] evidence = CampaignCanonical.ReadIds(reader);
        IReadOnlySet<string> tags = CampaignCanonical.ReadStringSet(reader);
        int factCount = reader.ReadCount();
        var facts = new HistoricalFactDefinition[factCount];
        for (int index = 0; index < factCount; index++) facts[index] = HistoricalFactDefinition.ReadCanonical(reader);
        return new HistoricalEventRecord(id, kind, region, day, location, participants, causes, summary, visibility, evidence, tags, facts);
    }
}

public sealed record RegionalHistoryRecord(
    StableId Id,
    RegionFamily Region,
    string PreCollapseIdentity,
    string PresentIdentity,
    StableId RepresentativeLocationId,
    IReadOnlyList<StableId> EventIds,
    IReadOnlySet<string> CurrentPressures,
    IReadOnlySet<string> HiddenTruthTags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteInt32((int)Region);
        writer.WriteString(PreCollapseIdentity);
        writer.WriteString(PresentIdentity);
        writer.WriteStableId(RepresentativeLocationId);
        CampaignCanonical.WriteIds(writer, EventIds);
        CampaignCanonical.WriteStrings(writer, CurrentPressures);
        CampaignCanonical.WriteStrings(writer, HiddenTruthTags);
    }

    public static RegionalHistoryRecord ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(),
        (RegionFamily)reader.ReadInt32(),
        reader.ReadRequiredString(),
        reader.ReadRequiredString(),
        reader.ReadStableId(),
        CampaignCanonical.ReadIds(reader),
        CampaignCanonical.ReadStringSet(reader),
        CampaignCanonical.ReadStringSet(reader));
}

public sealed record RegionalHistoryPlan(
    StableId Id,
    CampaignSeed CampaignSeed,
    IReadOnlyList<RegionalHistoryRecord> Regions,
    IReadOnlyList<HistoricalEventRecord> Events,
    IReadOnlyList<string> ValidationHistory,
    IReadOnlyList<string> RepairHistory,
    int GeneratorVersion) : ICanonicalWritable
{
    public HistoricalEventRecord GetEvent(StableId id) => Events.FirstOrDefault(value => value.Id == id) ?? throw new KeyNotFoundException($"Unknown historical event '{id}'.");

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteUInt64(CampaignSeed.Value);
        writer.WriteCount(Regions.Count);
        foreach (RegionalHistoryRecord region in Regions.OrderBy(static value => value.Region)) region.WriteCanonical(writer);
        writer.WriteCount(Events.Count);
        foreach (HistoricalEventRecord historicalEvent in Events.OrderBy(static value => value.Day).ThenBy(static value => value.Id)) historicalEvent.WriteCanonical(writer);
        CampaignCanonical.WriteStrings(writer, ValidationHistory);
        CampaignCanonical.WriteStrings(writer, RepairHistory);
        writer.WriteInt32(GeneratorVersion);
    }

    public static RegionalHistoryPlan ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        var seed = new CampaignSeed(reader.ReadUInt64());
        int regionCount = reader.ReadCount();
        var regions = new RegionalHistoryRecord[regionCount];
        for (int index = 0; index < regionCount; index++) regions[index] = RegionalHistoryRecord.ReadCanonical(reader);
        int eventCount = reader.ReadCount();
        var events = new HistoricalEventRecord[eventCount];
        for (int index = 0; index < eventCount; index++) events[index] = HistoricalEventRecord.ReadCanonical(reader);
        return new RegionalHistoryPlan(id, seed, regions, events, CampaignCanonical.ReadStrings(reader), CampaignCanonical.ReadStrings(reader), reader.ReadInt32());
    }
}

public sealed record CampaignPremiseKernel(
    string Id,
    CampaignPremiseKind Kind,
    string DisplayName,
    IReadOnlySet<HistoricalEventKind> RequiredEventKinds,
    IReadOnlySet<string> PreferredTags,
    int BasePriority,
    int MinimumFactionCount,
    int MinimumRegionCount) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteInt32((int)Kind);
        writer.WriteString(DisplayName);
        writer.WriteCount(RequiredEventKinds.Count);
        foreach (HistoricalEventKind value in RequiredEventKinds.Order()) writer.WriteInt32((int)value);
        CampaignCanonical.WriteStrings(writer, PreferredTags);
        writer.WriteInt32(BasePriority);
        writer.WriteInt32(MinimumFactionCount);
        writer.WriteInt32(MinimumRegionCount);
    }

    public static CampaignPremiseKernel ReadCanonical(CanonicalReader reader)
    {
        string id = reader.ReadRequiredString();
        CampaignPremiseKind kind = (CampaignPremiseKind)reader.ReadInt32();
        string name = reader.ReadRequiredString();
        int count = reader.ReadCount();
        var kinds = new SortedSet<HistoricalEventKind>();
        for (int index = 0; index < count; index++) kinds.Add((HistoricalEventKind)reader.ReadInt32());
        return new CampaignPremiseKernel(id, kind, name, kinds, CampaignCanonical.ReadStringSet(reader), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }
}

public sealed record CampaignPremiseSelection(
    StableId Id,
    CampaignPremiseKernel Kernel,
    RegionFamily PrimaryRegion,
    StableId PrimaryFactionId,
    StableId SecondaryFactionId,
    StableId PrimaryLocationId,
    StableId HiddenCauseEventId,
    int Score,
    IReadOnlyList<string> Reasons) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        Kernel.WriteCanonical(writer);
        writer.WriteInt32((int)PrimaryRegion);
        writer.WriteStableId(PrimaryFactionId);
        writer.WriteStableId(SecondaryFactionId);
        writer.WriteStableId(PrimaryLocationId);
        writer.WriteStableId(HiddenCauseEventId);
        writer.WriteInt32(Score);
        CampaignCanonical.WriteStrings(writer, Reasons);
    }

    public static CampaignPremiseSelection ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(),
        CampaignPremiseKernel.ReadCanonical(reader),
        (RegionFamily)reader.ReadInt32(),
        reader.ReadStableId(),
        reader.ReadStableId(),
        reader.ReadStableId(),
        reader.ReadStableId(),
        reader.ReadInt32(),
        CampaignCanonical.ReadStrings(reader));
}

public sealed record RevelationDefinition(
    StableId Id,
    string DisplayName,
    IReadOnlyList<StableId> HiddenFactIds,
    IReadOnlyList<StableId> EvidenceIds,
    int RequiredEvidenceCount,
    IReadOnlyList<StableId> UnlockMilestoneIds,
    IReadOnlySet<string> Tags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(DisplayName);
        CampaignCanonical.WriteIds(writer, HiddenFactIds);
        CampaignCanonical.WriteIds(writer, EvidenceIds);
        writer.WriteInt32(RequiredEvidenceCount);
        CampaignCanonical.WriteIds(writer, UnlockMilestoneIds);
        CampaignCanonical.WriteStrings(writer, Tags);
    }

    public static RevelationDefinition ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), reader.ReadRequiredString(), CampaignCanonical.ReadIds(reader), CampaignCanonical.ReadIds(reader), reader.ReadInt32(), CampaignCanonical.ReadIds(reader), CampaignCanonical.ReadStringSet(reader));
}

public sealed record CampaignChoiceOption(
    string Id,
    string DisplayName,
    StableId FavoredFactionId,
    StableId OpposedFactionId,
    IReadOnlySet<string> AddFlags,
    IReadOnlySet<string> RemoveFlags,
    int TrustDelta,
    int FearDelta,
    int RespectDelta,
    string ConsequenceSummary) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteString(DisplayName);
        writer.WriteStableId(FavoredFactionId);
        writer.WriteStableId(OpposedFactionId);
        CampaignCanonical.WriteStrings(writer, AddFlags);
        CampaignCanonical.WriteStrings(writer, RemoveFlags);
        writer.WriteInt32(TrustDelta);
        writer.WriteInt32(FearDelta);
        writer.WriteInt32(RespectDelta);
        writer.WriteString(ConsequenceSummary);
    }

    public static CampaignChoiceOption ReadCanonical(CanonicalReader reader) => new(
        reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadStableId(), CampaignCanonical.ReadStringSet(reader), CampaignCanonical.ReadStringSet(reader), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadRequiredString());
}

public sealed record IrreversibleChoiceDefinition(
    StableId Id,
    string DisplayName,
    StableId MilestoneId,
    IReadOnlyList<CampaignChoiceOption> Options,
    bool LocksOnSelection,
    IReadOnlySet<string> Tags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(DisplayName);
        writer.WriteStableId(MilestoneId);
        writer.WriteCount(Options.Count);
        foreach (CampaignChoiceOption option in Options.OrderBy(static value => value.Id, StringComparer.Ordinal)) option.WriteCanonical(writer);
        writer.WriteBoolean(LocksOnSelection);
        CampaignCanonical.WriteStrings(writer, Tags);
    }

    public static IrreversibleChoiceDefinition ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        string display = reader.ReadRequiredString();
        StableId milestone = reader.ReadStableId();
        int count = reader.ReadCount();
        var options = new CampaignChoiceOption[count];
        for (int index = 0; index < count; index++) options[index] = CampaignChoiceOption.ReadCanonical(reader);
        return new IrreversibleChoiceDefinition(id, display, milestone, options, reader.ReadBoolean(), CampaignCanonical.ReadStringSet(reader));
    }
}

public sealed record CriticalRoleDefinition(
    string Id,
    string DisplayName,
    IReadOnlySet<string> RequiredTags,
    IReadOnlyList<CriticalRoleFallbackKind> FallbackOrder,
    bool RequiredForMainCampaign) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteString(DisplayName);
        CampaignCanonical.WriteStrings(writer, RequiredTags);
        writer.WriteCount(FallbackOrder.Count);
        foreach (CriticalRoleFallbackKind value in FallbackOrder) writer.WriteInt32((int)value);
        writer.WriteBoolean(RequiredForMainCampaign);
    }

    public static CriticalRoleDefinition ReadCanonical(CanonicalReader reader)
    {
        string id = reader.ReadRequiredString();
        string display = reader.ReadRequiredString();
        IReadOnlySet<string> tags = CampaignCanonical.ReadStringSet(reader);
        int count = reader.ReadCount();
        var order = new CriticalRoleFallbackKind[count];
        for (int index = 0; index < count; index++) order[index] = (CriticalRoleFallbackKind)reader.ReadInt32();
        return new CriticalRoleDefinition(id, display, tags, order, reader.ReadBoolean());
    }
}

public sealed record CriticalRoleAssignmentPlan(
    StableId Id,
    string RoleId,
    StableId AssignedEntityId,
    StableId PreferredFactionId,
    IReadOnlyList<StableId> NamedReplacementIds,
    StableId EvidenceFallbackId,
    StableId MachineFallbackId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(RoleId);
        writer.WriteStableId(AssignedEntityId);
        writer.WriteStableId(PreferredFactionId);
        CampaignCanonical.WriteIds(writer, NamedReplacementIds);
        writer.WriteStableId(EvidenceFallbackId);
        writer.WriteStableId(MachineFallbackId);
    }

    public static CriticalRoleAssignmentPlan ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadStableId(), CampaignCanonical.ReadIds(reader), reader.ReadStableId(), reader.ReadStableId());
}

public sealed record MilestoneKernel(
    string Id,
    CampaignMilestoneRole Role,
    string DisplayName,
    IReadOnlyList<string> PreferredQuestKernelIds,
    IReadOnlySet<string> RequiredPremiseTags,
    bool RequiresDecision,
    bool RequiresRevelation,
    bool RequiresConvergenceResolution) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteInt32((int)Role);
        writer.WriteString(DisplayName);
        CampaignCanonical.WriteStrings(writer, PreferredQuestKernelIds);
        CampaignCanonical.WriteStrings(writer, RequiredPremiseTags);
        writer.WriteBoolean(RequiresDecision);
        writer.WriteBoolean(RequiresRevelation);
        writer.WriteBoolean(RequiresConvergenceResolution);
    }

    public static MilestoneKernel ReadCanonical(CanonicalReader reader) => new(
        reader.ReadRequiredString(), (CampaignMilestoneRole)reader.ReadInt32(), reader.ReadRequiredString(), CampaignCanonical.ReadStrings(reader), CampaignCanonical.ReadStringSet(reader), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean());
}

public sealed record GeneratedMilestonePlan(
    StableId Id,
    MilestoneKernel Kernel,
    int Sequence,
    RegionFamily Region,
    StableId PrimaryFactionId,
    StableId SecondaryFactionId,
    StableId LocationId,
    GeneratedQuestPlan QuestPlan,
    IReadOnlyList<StableId> PredecessorIds,
    IReadOnlyList<StableId> NextIds,
    StableId RevelationId,
    StableId ChoiceId,
    IReadOnlyList<string> CriticalRoleIds,
    ulong EarliestDay,
    ulong DeadlineDay,
    string FailurePolicy) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        Kernel.WriteCanonical(writer);
        writer.WriteInt32(Sequence);
        writer.WriteInt32((int)Region);
        writer.WriteStableId(PrimaryFactionId);
        writer.WriteStableId(SecondaryFactionId);
        writer.WriteStableId(LocationId);
        QuestPlan.WriteCanonical(writer);
        CampaignCanonical.WriteIds(writer, PredecessorIds);
        CampaignCanonical.WriteIds(writer, NextIds);
        writer.WriteStableId(RevelationId);
        writer.WriteStableId(ChoiceId);
        CampaignCanonical.WriteStrings(writer, CriticalRoleIds);
        writer.WriteUInt64(EarliestDay);
        writer.WriteUInt64(DeadlineDay);
        writer.WriteString(FailurePolicy);
    }

    public static GeneratedMilestonePlan ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), MilestoneKernel.ReadCanonical(reader), reader.ReadInt32(), (RegionFamily)reader.ReadInt32(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), GeneratedQuestPlan.ReadCanonical(reader), CampaignCanonical.ReadIds(reader), CampaignCanonical.ReadIds(reader), reader.ReadStableId(), reader.ReadStableId(), CampaignCanonical.ReadStrings(reader), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadRequiredString());
}

public sealed record ConvergenceDefinition(
    StableId Id,
    StableId MilestoneId,
    IReadOnlyList<ConvergenceForm> EligibleForms,
    IReadOnlyList<StableId> ParticipatingFactionIds,
    StableId CentralLocationId,
    IReadOnlySet<string> RequiredFlags,
    IReadOnlySet<string> ForbiddenFlags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteStableId(MilestoneId);
        writer.WriteCount(EligibleForms.Count);
        foreach (ConvergenceForm form in EligibleForms) writer.WriteInt32((int)form);
        CampaignCanonical.WriteIds(writer, ParticipatingFactionIds);
        writer.WriteStableId(CentralLocationId);
        CampaignCanonical.WriteStrings(writer, RequiredFlags);
        CampaignCanonical.WriteStrings(writer, ForbiddenFlags);
    }

    public static ConvergenceDefinition ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        StableId milestone = reader.ReadStableId();
        int count = reader.ReadCount();
        var forms = new ConvergenceForm[count];
        for (int index = 0; index < count; index++) forms[index] = (ConvergenceForm)reader.ReadInt32();
        return new ConvergenceDefinition(id, milestone, forms, CampaignCanonical.ReadIds(reader), reader.ReadStableId(), CampaignCanonical.ReadStringSet(reader), CampaignCanonical.ReadStringSet(reader));
    }
}

public sealed record EndingRequirement(
    EndingRequirementKind Kind,
    StableId SubjectId,
    string Token,
    int Minimum) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)Kind);
        writer.WriteStableId(SubjectId);
        writer.WriteString(Token);
        writer.WriteInt32(Minimum);
    }

    public static EndingRequirement ReadCanonical(CanonicalReader reader) => new((EndingRequirementKind)reader.ReadInt32(), reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadInt32());
}

public sealed record EndingFamilyDefinition(
    StableId Id,
    string DisplayName,
    EndingTone Tone,
    int Priority,
    IReadOnlyList<EndingRequirement> Requirements,
    IReadOnlySet<string> AddEpilogueFlags,
    string SummaryTemplate) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(DisplayName);
        writer.WriteInt32((int)Tone);
        writer.WriteInt32(Priority);
        writer.WriteCount(Requirements.Count);
        foreach (EndingRequirement requirement in Requirements) requirement.WriteCanonical(writer);
        CampaignCanonical.WriteStrings(writer, AddEpilogueFlags);
        writer.WriteString(SummaryTemplate);
    }

    public static EndingFamilyDefinition ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        string name = reader.ReadRequiredString();
        EndingTone tone = (EndingTone)reader.ReadInt32();
        int priority = reader.ReadInt32();
        int count = reader.ReadCount();
        var requirements = new EndingRequirement[count];
        for (int index = 0; index < count; index++) requirements[index] = EndingRequirement.ReadCanonical(reader);
        return new EndingFamilyDefinition(id, name, tone, priority, requirements, CampaignCanonical.ReadStringSet(reader), reader.ReadRequiredString());
    }
}

public sealed record EpilogueRule(
    StableId Id,
    string SubjectKind,
    StableId SubjectId,
    int Priority,
    IReadOnlyList<EndingRequirement> Requirements,
    string TextTemplate,
    IReadOnlySet<string> Tags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteString(SubjectKind);
        writer.WriteStableId(SubjectId);
        writer.WriteInt32(Priority);
        writer.WriteCount(Requirements.Count);
        foreach (EndingRequirement requirement in Requirements) requirement.WriteCanonical(writer);
        writer.WriteString(TextTemplate);
        CampaignCanonical.WriteStrings(writer, Tags);
    }

    public static EpilogueRule ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        string kind = reader.ReadRequiredString();
        StableId subject = reader.ReadStableId();
        int priority = reader.ReadInt32();
        int count = reader.ReadCount();
        var requirements = new EndingRequirement[count];
        for (int index = 0; index < count; index++) requirements[index] = EndingRequirement.ReadCanonical(reader);
        return new EpilogueRule(id, kind, subject, priority, requirements, reader.ReadRequiredString(), CampaignCanonical.ReadStringSet(reader));
    }
}

public sealed record GeneratedCampaignPlan(
    StableId Id,
    CampaignSeed CampaignSeed,
    RegionalHistoryPlan History,
    CampaignPremiseSelection Premise,
    IReadOnlyList<GeneratedMilestonePlan> Milestones,
    IReadOnlyList<RevelationDefinition> Revelations,
    IReadOnlyList<IrreversibleChoiceDefinition> Choices,
    ConvergenceDefinition Convergence,
    IReadOnlyList<EndingFamilyDefinition> EndingFamilies,
    IReadOnlyList<EpilogueRule> EpilogueRules,
    IReadOnlyList<CriticalRoleDefinition> CriticalRoles,
    IReadOnlyList<CriticalRoleAssignmentPlan> CriticalRoleAssignments,
    IReadOnlyList<string> ValidationHistory,
    IReadOnlyList<string> RepairHistory,
    int GeneratorVersion,
    StateChecksum Fingerprint) : ICanonicalWritable
{
    public GeneratedMilestonePlan GetMilestone(StableId id) => Milestones.FirstOrDefault(value => value.Id == id) ?? throw new KeyNotFoundException($"Unknown campaign milestone '{id}'.");
    public IrreversibleChoiceDefinition GetChoice(StableId id) => Choices.FirstOrDefault(value => value.Id == id) ?? throw new KeyNotFoundException($"Unknown campaign choice '{id}'.");
    public RevelationDefinition GetRevelation(StableId id) => Revelations.FirstOrDefault(value => value.Id == id) ?? throw new KeyNotFoundException($"Unknown revelation '{id}'.");

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteUInt64(CampaignSeed.Value);
        History.WriteCanonical(writer);
        Premise.WriteCanonical(writer);
        writer.WriteCount(Milestones.Count);
        foreach (GeneratedMilestonePlan milestone in Milestones.OrderBy(static value => value.Sequence)) milestone.WriteCanonical(writer);
        writer.WriteCount(Revelations.Count);
        foreach (RevelationDefinition revelation in Revelations.OrderBy(static value => value.Id)) revelation.WriteCanonical(writer);
        writer.WriteCount(Choices.Count);
        foreach (IrreversibleChoiceDefinition choice in Choices.OrderBy(static value => value.Id)) choice.WriteCanonical(writer);
        Convergence.WriteCanonical(writer);
        writer.WriteCount(EndingFamilies.Count);
        foreach (EndingFamilyDefinition ending in EndingFamilies.OrderByDescending(static value => value.Priority).ThenBy(static value => value.Id)) ending.WriteCanonical(writer);
        writer.WriteCount(EpilogueRules.Count);
        foreach (EpilogueRule rule in EpilogueRules.OrderBy(static value => value.SubjectKind, StringComparer.Ordinal).ThenByDescending(static value => value.Priority).ThenBy(static value => value.Id)) rule.WriteCanonical(writer);
        writer.WriteCount(CriticalRoles.Count);
        foreach (CriticalRoleDefinition role in CriticalRoles.OrderBy(static value => value.Id, StringComparer.Ordinal)) role.WriteCanonical(writer);
        writer.WriteCount(CriticalRoleAssignments.Count);
        foreach (CriticalRoleAssignmentPlan assignment in CriticalRoleAssignments.OrderBy(static value => value.RoleId, StringComparer.Ordinal)) assignment.WriteCanonical(writer);
        CampaignCanonical.WriteStrings(writer, ValidationHistory);
        CampaignCanonical.WriteStrings(writer, RepairHistory);
        writer.WriteInt32(GeneratorVersion);
        writer.WriteChecksum(Fingerprint);
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

    public static GeneratedCampaignPlan ReadCanonical(ReadOnlyMemory<byte> payload)
    {
        var reader = new CanonicalReader(payload);
        GeneratedCampaignPlan result = ReadCanonical(reader);
        reader.EnsureComplete();
        return result;
    }

    public static GeneratedCampaignPlan ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId();
        var seed = new CampaignSeed(reader.ReadUInt64());
        RegionalHistoryPlan history = RegionalHistoryPlan.ReadCanonical(reader);
        CampaignPremiseSelection premise = CampaignPremiseSelection.ReadCanonical(reader);
        int milestoneCount = reader.ReadCount();
        var milestones = new GeneratedMilestonePlan[milestoneCount];
        for (int index = 0; index < milestoneCount; index++) milestones[index] = GeneratedMilestonePlan.ReadCanonical(reader);
        int revelationCount = reader.ReadCount();
        var revelations = new RevelationDefinition[revelationCount];
        for (int index = 0; index < revelationCount; index++) revelations[index] = RevelationDefinition.ReadCanonical(reader);
        int choiceCount = reader.ReadCount();
        var choices = new IrreversibleChoiceDefinition[choiceCount];
        for (int index = 0; index < choiceCount; index++) choices[index] = IrreversibleChoiceDefinition.ReadCanonical(reader);
        ConvergenceDefinition convergence = ConvergenceDefinition.ReadCanonical(reader);
        int endingCount = reader.ReadCount();
        var endings = new EndingFamilyDefinition[endingCount];
        for (int index = 0; index < endingCount; index++) endings[index] = EndingFamilyDefinition.ReadCanonical(reader);
        int epilogueCount = reader.ReadCount();
        var epilogues = new EpilogueRule[epilogueCount];
        for (int index = 0; index < epilogueCount; index++) epilogues[index] = EpilogueRule.ReadCanonical(reader);
        int roleCount = reader.ReadCount();
        var roles = new CriticalRoleDefinition[roleCount];
        for (int index = 0; index < roleCount; index++) roles[index] = CriticalRoleDefinition.ReadCanonical(reader);
        int assignmentCount = reader.ReadCount();
        var assignments = new CriticalRoleAssignmentPlan[assignmentCount];
        for (int index = 0; index < assignmentCount; index++) assignments[index] = CriticalRoleAssignmentPlan.ReadCanonical(reader);
        return new GeneratedCampaignPlan(id, seed, history, premise, milestones, revelations, choices, convergence, endings, epilogues, roles, assignments, CampaignCanonical.ReadStrings(reader), CampaignCanonical.ReadStrings(reader), reader.ReadInt32(), reader.ReadChecksum());
    }
}

public sealed record CampaignGenerationRequest(
    CampaignSeed CampaignSeed,
    StableId PlayerId,
    SocietyWorldState Society,
    QuestBindingContext QuestBindings,
    IReadOnlyList<QuestKernelDefinition> QuestKernels,
    RegionalTopology? Topology = null,
    IReadOnlyList<StableId>? CompanionIds = null);

internal static class CampaignCanonical
{
    public static void WriteIds(CanonicalWriter writer, IEnumerable<StableId> values)
    {
        StableId[] ordered = values.Order().Distinct().ToArray();
        writer.WriteCount(ordered.Length);
        foreach (StableId value in ordered) writer.WriteStableId(value);
    }

    public static StableId[] ReadIds(CanonicalReader reader)
    {
        int count = reader.ReadCount();
        var result = new StableId[count];
        for (int index = 0; index < count; index++) result[index] = reader.ReadStableId();
        return result;
    }

    public static void WriteStrings(CanonicalWriter writer, IEnumerable<string> values)
    {
        string[] ordered = values.Where(static value => !string.IsNullOrWhiteSpace(value)).Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        writer.WriteCount(ordered.Length);
        foreach (string value in ordered) writer.WriteString(value);
    }

    public static string[] ReadStrings(CanonicalReader reader)
    {
        int count = reader.ReadCount();
        var result = new string[count];
        for (int index = 0; index < count; index++) result[index] = reader.ReadRequiredString();
        return result;
    }

    public static IReadOnlySet<string> ReadStringSet(CanonicalReader reader) => new SortedSet<string>(ReadStrings(reader), StringComparer.Ordinal);
}
