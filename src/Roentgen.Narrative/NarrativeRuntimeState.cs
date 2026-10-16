using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Narrative;

public sealed record ObjectiveRuntimeState(string ObjectiveId, ObjectiveStatus Status, bool Revealed, ulong ChangedTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(ObjectiveId); writer.WriteInt32((int)Status); writer.WriteBoolean(Revealed); writer.WriteUInt64(ChangedTick); }
    public static ObjectiveRuntimeState ReadCanonical(CanonicalReader reader) => new(reader.ReadRequiredString(), (ObjectiveStatus)reader.ReadInt32(), reader.ReadBoolean(), reader.ReadUInt64());
}

public sealed class QuestInstanceState : ICanonicalWritable
{
    private readonly SortedDictionary<string, ObjectiveRuntimeState> objectives = new(StringComparer.Ordinal);
    private readonly SortedSet<string> activeNodeIds = new(StringComparer.Ordinal);
    private readonly SortedSet<string> completedRouteIds = new(StringComparer.Ordinal);
    private readonly SortedSet<StableId> appliedConsequenceIds = [];

    public QuestInstanceState(GeneratedQuestPlan plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        foreach (QuestObjectiveDefinition objective in plan.Kernel.Objectives) objectives[objective.Id] = new ObjectiveRuntimeState(objective.Id, ObjectiveStatus.Dormant, !objective.Hidden, 0);
    }

    public GeneratedQuestPlan Plan { get; }
    public StableId Id => Plan.Id;
    public QuestStatus Status { get; private set; } = QuestStatus.Generated;
    public IReadOnlyDictionary<string, ObjectiveRuntimeState> Objectives => objectives;
    public IReadOnlySet<string> ActiveNodeIds => activeNodeIds;
    public IReadOnlySet<string> CompletedRouteIds => completedRouteIds;
    public IReadOnlySet<StableId> AppliedConsequenceIds => appliedConsequenceIds;
    public ulong StartedTick { get; private set; }
    public ulong CompletedTick { get; private set; }
    public ulong Revision { get; private set; }

    public void Offer(ulong tick) { if (Status == QuestStatus.Generated) { Status = QuestStatus.Offered; StartedTick = tick; Revision++; } }
    public void Start(ulong tick)
    {
        if (Status is QuestStatus.Completed or QuestStatus.Failed) return;
        Status = QuestStatus.Active; if (StartedTick == 0) StartedTick = tick;
        string? first = Plan.Kernel.Nodes.OrderBy(static node => node.Id, StringComparer.Ordinal).FirstOrDefault(static node => node.Kind == QuestNodeKind.Stage)?.Id;
        if (first is not null) activeNodeIds.Add(first);
        foreach (QuestObjectiveDefinition objective in Plan.Kernel.Objectives.Where(static objective => !objective.Hidden).Take(1)) ActivateObjective(objective.Id, tick);
        Revision++;
    }
    public void ActivateNode(string id) { if (activeNodeIds.Add(id)) Revision++; }
    public void DeactivateNode(string id) { if (activeNodeIds.Remove(id)) Revision++; }
    public void ActivateObjective(string id, ulong tick) { if (!objectives.TryGetValue(id, out ObjectiveRuntimeState? current)) throw new KeyNotFoundException($"Unknown objective '{id}'."); if (current.Status == ObjectiveStatus.Dormant) { objectives[id] = current with { Status = ObjectiveStatus.Active, Revealed = true, ChangedTick = tick }; Revision++; } }
    public void RevealObjective(string id, ulong tick) { if (!objectives.TryGetValue(id, out ObjectiveRuntimeState? current)) return; objectives[id] = current with { Revealed = true, ChangedTick = tick }; Revision++; }
    public void CompleteObjective(string id, ulong tick) { if (!objectives.TryGetValue(id, out ObjectiveRuntimeState? current)) throw new KeyNotFoundException($"Unknown objective '{id}'."); if (current.Status != ObjectiveStatus.Completed) { objectives[id] = current with { Status = ObjectiveStatus.Completed, Revealed = true, ChangedTick = tick }; Revision++; } }
    public void FailObjective(string id, ulong tick) { if (!objectives.TryGetValue(id, out ObjectiveRuntimeState? current)) throw new KeyNotFoundException($"Unknown objective '{id}'."); if (current.Status != ObjectiveStatus.Failed) { objectives[id] = current with { Status = ObjectiveStatus.Failed, Revealed = true, ChangedTick = tick }; Revision++; } }
    public void CompleteRoute(string id) { if (completedRouteIds.Add(id)) Revision++; }
    public bool MarkConsequenceApplied(StableId id) { bool changed = appliedConsequenceIds.Add(id); if (changed) Revision++; return changed; }
    public void Complete(ulong tick) { Status = QuestStatus.Completed; CompletedTick = tick; activeNodeIds.Clear(); Revision++; }
    public void Fail(ulong tick) { Status = QuestStatus.Failed; CompletedTick = tick; activeNodeIds.Clear(); Revision++; }
    public void Suspend() { if (Status == QuestStatus.Active) { Status = QuestStatus.Suspended; Revision++; } }

    public void WriteCanonical(CanonicalWriter writer)
    {
        Plan.WriteCanonical(writer); writer.WriteInt32((int)Status); writer.WriteCount(objectives.Count); foreach (ObjectiveRuntimeState value in objectives.Values) value.WriteCanonical(writer); writer.WriteCount(activeNodeIds.Count); foreach (string value in activeNodeIds) writer.WriteString(value); writer.WriteCount(completedRouteIds.Count); foreach (string value in completedRouteIds) writer.WriteString(value); writer.WriteCount(appliedConsequenceIds.Count); foreach (StableId value in appliedConsequenceIds) writer.WriteStableId(value); writer.WriteUInt64(StartedTick); writer.WriteUInt64(CompletedTick); writer.WriteUInt64(Revision);
    }
    public static QuestInstanceState ReadCanonical(CanonicalReader reader)
    {
        var result = new QuestInstanceState(GeneratedQuestPlan.ReadCanonical(reader)); result.Status = (QuestStatus)reader.ReadInt32(); result.objectives.Clear(); int objectiveCount = reader.ReadCount(); for (int i = 0; i < objectiveCount; i++) { ObjectiveRuntimeState value = ObjectiveRuntimeState.ReadCanonical(reader); result.objectives.Add(value.ObjectiveId, value); } result.activeNodeIds.Clear(); int nodeCount = reader.ReadCount(); for (int i = 0; i < nodeCount; i++) result.activeNodeIds.Add(reader.ReadRequiredString()); result.completedRouteIds.Clear(); int routeCount = reader.ReadCount(); for (int i = 0; i < routeCount; i++) result.completedRouteIds.Add(reader.ReadRequiredString()); result.appliedConsequenceIds.Clear(); int consequenceCount = reader.ReadCount(); for (int i = 0; i < consequenceCount; i++) result.appliedConsequenceIds.Add(reader.ReadStableId()); result.StartedTick = reader.ReadUInt64(); result.CompletedTick = reader.ReadUInt64(); result.Revision = reader.ReadUInt64(); return result;
    }
}

public sealed record DialogueRenderedChoice(string Id, string Text, bool Enabled, string DisabledReason, bool HasSkillCheck, int Difficulty, string? SkillLabel = null);
public sealed record DialogueRenderedNode(StableId SessionId, string SpeakerName, string Text, IReadOnlyList<DialogueRenderedChoice> Choices, bool EndsConversation);

public sealed class ConversationSessionState : ICanonicalWritable
{
    private readonly SortedSet<string> revealedChoiceIds = new(StringComparer.Ordinal);
    private readonly SortedSet<string> selectedChoiceIds = new(StringComparer.Ordinal);
    private readonly SortedSet<StableId> disclosedFactIds = [];
    private readonly Queue<string> recentUtteranceIds = new();

    public ConversationSessionState(StableId id, StableId questId, IReadOnlyList<StableId> participants, string currentNodeId, ulong visitCount)
    {
        Id = id; QuestId = questId; Participants = participants.Order().Distinct().ToArray(); CurrentNodeId = currentNodeId; VisitCount = visitCount;
    }

    public StableId Id { get; }
    public StableId QuestId { get; }
    public IReadOnlyList<StableId> Participants { get; }
    public string CurrentNodeId { get; private set; }
    public ulong VisitCount { get; private set; }
    public IReadOnlySet<string> RevealedChoiceIds => revealedChoiceIds;
    public IReadOnlySet<string> SelectedChoiceIds => selectedChoiceIds;
    public IReadOnlySet<StableId> DisclosedFactIds => disclosedFactIds;
    public IReadOnlyCollection<string> RecentUtteranceIds => recentUtteranceIds.ToArray();
    public ConversationEndReason EndReason { get; private set; }
    public ulong Revision { get; private set; }

    public void MoveTo(string nodeId) { CurrentNodeId = nodeId; VisitCount++; AddUtterance(nodeId); Revision++; }
    public void RevealChoice(string id) { if (revealedChoiceIds.Add(id)) Revision++; }
    public void SelectChoice(string id) { if (selectedChoiceIds.Add(id)) Revision++; }
    public void DiscloseFact(StableId id) { if (disclosedFactIds.Add(id)) Revision++; }
    public void End(ConversationEndReason reason) { EndReason = reason; Revision++; }
    private void AddUtterance(string id) { recentUtteranceIds.Enqueue(id); while (recentUtteranceIds.Count > 16) _ = recentUtteranceIds.Dequeue(); }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteStableId(QuestId); writer.WriteCount(Participants.Count); foreach (StableId id in Participants) writer.WriteStableId(id); writer.WriteString(CurrentNodeId); writer.WriteUInt64(VisitCount); writer.WriteCount(revealedChoiceIds.Count); foreach (string id in revealedChoiceIds) writer.WriteString(id); writer.WriteCount(selectedChoiceIds.Count); foreach (string id in selectedChoiceIds) writer.WriteString(id); writer.WriteCount(disclosedFactIds.Count); foreach (StableId id in disclosedFactIds) writer.WriteStableId(id); writer.WriteCount(recentUtteranceIds.Count); foreach (string id in recentUtteranceIds) writer.WriteString(id); writer.WriteInt32((int)EndReason); writer.WriteUInt64(Revision);
    }
    public static ConversationSessionState ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId(); StableId quest = reader.ReadStableId(); int participantCount = reader.ReadCount(); var participants = new StableId[participantCount]; for (int i = 0; i < participantCount; i++) participants[i] = reader.ReadStableId(); var result = new ConversationSessionState(id, quest, participants, reader.ReadRequiredString(), reader.ReadUInt64()); int revealCount = reader.ReadCount(); for (int i = 0; i < revealCount; i++) result.revealedChoiceIds.Add(reader.ReadRequiredString()); int selectedCount = reader.ReadCount(); for (int i = 0; i < selectedCount; i++) result.selectedChoiceIds.Add(reader.ReadRequiredString()); int factCount = reader.ReadCount(); for (int i = 0; i < factCount; i++) result.disclosedFactIds.Add(reader.ReadStableId()); int utteranceCount = reader.ReadCount(); for (int i = 0; i < utteranceCount; i++) result.recentUtteranceIds.Enqueue(reader.ReadRequiredString()); result.EndReason = (ConversationEndReason)reader.ReadInt32(); result.Revision = reader.ReadUInt64(); return result;
    }
}

public sealed record RumorRecord(StableId Id, StableId FactId, FactTruthState ClaimedTruth, StableId OriginatorId, IReadOnlyList<StableId> TransmissionChain, int ConfidencePermille, int DistortionPermille, ulong CreatedDay, ulong ExpirationDay, RumorStatus Status, string RenderTemplateId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(FactId); writer.WriteInt32((int)ClaimedTruth); writer.WriteStableId(OriginatorId); writer.WriteCount(TransmissionChain.Count); foreach (StableId id in TransmissionChain) writer.WriteStableId(id); writer.WriteInt32(ConfidencePermille); writer.WriteInt32(DistortionPermille); writer.WriteUInt64(CreatedDay); writer.WriteUInt64(ExpirationDay); writer.WriteInt32((int)Status); writer.WriteString(RenderTemplateId); }
    public static RumorRecord ReadCanonical(CanonicalReader reader) { StableId id = reader.ReadStableId(); StableId fact = reader.ReadStableId(); FactTruthState truth = (FactTruthState)reader.ReadInt32(); StableId origin = reader.ReadStableId(); int count = reader.ReadCount(); var chain = new StableId[count]; for (int i = 0; i < count; i++) chain[i] = reader.ReadStableId(); return new(id, fact, truth, origin, chain, reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt64(), reader.ReadUInt64(), (RumorStatus)reader.ReadInt32(), reader.ReadRequiredString()); }
}

public sealed record NarrativeConsequenceRecord(StableId Id, StableId QuestId, string EffectId, StableId ResponsibleActorId, IReadOnlyList<StableId> AffectedIds, ulong AppliedTick, bool Reversible, string FollowUpHookId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(QuestId); writer.WriteString(EffectId); writer.WriteStableId(ResponsibleActorId); writer.WriteCount(AffectedIds.Count); foreach (StableId id in AffectedIds.Order()) writer.WriteStableId(id); writer.WriteUInt64(AppliedTick); writer.WriteBoolean(Reversible); writer.WriteString(FollowUpHookId); }
    public static NarrativeConsequenceRecord ReadCanonical(CanonicalReader reader) { StableId id = reader.ReadStableId(); StableId quest = reader.ReadStableId(); string effect = reader.ReadRequiredString(); StableId actor = reader.ReadStableId(); int count = reader.ReadCount(); var affected = new StableId[count]; for (int i = 0; i < count; i++) affected[i] = reader.ReadStableId(); return new(id, quest, effect, actor, affected, reader.ReadUInt64(), reader.ReadBoolean(), reader.ReadRequiredString()); }
}

public sealed record RecoveryRecord(StableId Id, StableId QuestId, string RecoveryId, RecoveryKind Kind, StableId ReplacedEntityId, StableId ReplacementEntityId, ulong AppliedTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(QuestId); writer.WriteString(RecoveryId); writer.WriteInt32((int)Kind); writer.WriteStableId(ReplacedEntityId); writer.WriteStableId(ReplacementEntityId); writer.WriteUInt64(AppliedTick); }
    public static RecoveryRecord ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadRequiredString(), (RecoveryKind)reader.ReadInt32(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadUInt64());
}
