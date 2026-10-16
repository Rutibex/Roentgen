using Roentgen.Contracts;
using Roentgen.Narrative;
using Roentgen.Core;

namespace Roentgen.Simulation;

public sealed class Phase16NarrativeRuntime
{
    private readonly QuestRuntimeEngine questEngine = new();
    private readonly DialogueEngine dialogueEngine = new();
    private readonly NarrativeRepairEngine repairEngine = new();
    private readonly INarrativeExternalEffectRouter externalRouter;

    public Phase16NarrativeRuntime(NarrativeWorldState world, StableId playerId, INarrativeExternalEffectRouter? externalRouter = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world)); PlayerId = playerId; this.externalRouter = externalRouter ?? new NoOpNarrativeExternalEffectRouter();
    }

    public NarrativeWorldState World { get; private set; }
    public StableId PlayerId { get; }
    public IReadOnlyList<QuestUpdateResult> LastQuestUpdates { get; private set; } = [];
    public DialogueRenderedNode? CurrentDialogue { get; private set; }
    public StableId CurrentConversationId { get; private set; }

    public void Update(ulong tick, ulong day, INarrativeQueryContext query)
    {
        World.SetTime(tick, day);
        LastQuestUpdates = World.Quests.Where(static quest => quest.Status is QuestStatus.Generated or QuestStatus.Offered or QuestStatus.Active).OrderBy(static quest => quest.Id).Select(quest => questEngine.Reconcile(World, quest, query, externalRouter, PlayerId)).ToArray();
    }

    public IReadOnlyList<QuestUpdateResult> Ingest(SemanticEventRecord semanticEvent, INarrativeQueryContext query)
    {
        LastQuestUpdates = questEngine.IngestEvent(World, semanticEvent, query, externalRouter, PlayerId);
        return LastQuestUpdates;
    }

    public DialogueRenderedNode StartConversation(StableId questId, IReadOnlyList<StableId> participants, INarrativeQueryContext query, IDialoguePresentationContext presentation)
    {
        QuestInstanceState quest = World.GetQuest(questId);
        ulong visits = checked((ulong)World.Conversations.Count(session => session.QuestId == questId));
        ConversationSessionState session = dialogueEngine.StartConversation(World, quest, participants, visits);
        CurrentConversationId = session.Id;
        CurrentDialogue = dialogueEngine.Render(session, quest, query, presentation);
        return CurrentDialogue;
    }

    public SkillCheckAttemptRecord? ChooseDialogue(string choiceId, INarrativeQueryContext query, IDialoguePresentationContext presentation)
    {
        if (CurrentConversationId.IsEmpty) throw new InvalidOperationException("No conversation is active.");
        ConversationSessionState session = World.GetConversation(CurrentConversationId);
        QuestInstanceState quest = World.GetQuest(session.QuestId);
        SkillCheckAttemptRecord? result = dialogueEngine.Choose(World, session, quest, choiceId, PlayerId, query, externalRouter, presentation);
        CurrentDialogue = session.EndReason == ConversationEndReason.None ? dialogueEngine.Render(session, quest, query, presentation) : null;
        if (CurrentDialogue is null) CurrentConversationId = StableId.Empty;
        return result;
    }

    public void CancelConversation()
    {
        CurrentDialogue = null;
        CurrentConversationId = StableId.Empty;
    }

    public void DebugDiscoverEvidence(StableId questId, ulong tick)
    {
        QuestInstanceState quest = World.GetQuest(questId); StableId target = quest.Plan.ResolveSlot("EVIDENCE"); StableId factId = StableIdFactory.CreateChild(quest.Id, "debug-fact", "evidence"); World.UpsertFact(new FactRecord(factId, "quest.evidence.discovered", target, NarrativeValue.FromBoolean(true), FactTruthState.True, FactScope.Quest, factId, tick, null));
    }

    public void DebugResolveProblem(StableId questId, ulong tick)
    {
        QuestInstanceState quest = World.GetQuest(questId); StableId target = quest.Plan.ResolveSlot("PRIMARY_LOCATION"); StableId factId = StableIdFactory.CreateChild(quest.Id, "debug-fact", "resolved"); World.UpsertFact(new FactRecord(factId, "quest.problem.resolved", target, NarrativeValue.FromBoolean(true), FactTruthState.True, FactScope.Quest, factId, tick, null));
    }

    public IReadOnlyList<RecoveryRecord> Repair(Func<StableId, bool> actorAlive, Func<StableId, StableId?> replacementResolver, ulong tick) => repairEngine.Repair(World, actorAlive, replacementResolver, tick);
    public byte[] Save() => World.ToCanonicalPayload();
    public void Load(ReadOnlyMemory<byte> payload) { World.RestoreFrom(payload); CurrentDialogue = null; CurrentConversationId = StableId.Empty; }
}
