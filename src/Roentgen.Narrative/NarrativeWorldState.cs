using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Narrative;

public readonly record struct KnowledgeLedgerKey(StableId KnowerId, StableId ClaimId) : IComparable<KnowledgeLedgerKey>
{
    public int CompareTo(KnowledgeLedgerKey other) { int knower = KnowerId.CompareTo(other.KnowerId); return knower != 0 ? knower : ClaimId.CompareTo(other.ClaimId); }
}

public sealed class NarrativeWorldState : ICanonicalWritable
{
    public const int SchemaVersion = 16;
    private readonly FactDatabase factDatabase = new();
    private readonly SortedDictionary<KnowledgeLedgerKey, KnowledgeClaim> knowledgeClaims = [];
    private readonly SortedDictionary<StableId, GeneratedQuestPlan> generatedPlans = [];
    private readonly SortedDictionary<StableId, QuestInstanceState> quests = [];
    private readonly SortedDictionary<StableId, ConversationSessionState> conversations = [];
    private readonly SortedDictionary<StableId, RumorRecord> rumors = [];
    private readonly SortedDictionary<StableId, NarrativeConsequenceRecord> consequences = [];
    private readonly SortedDictionary<StableId, RecoveryRecord> recoveries = [];
    private readonly SortedDictionary<StableId, SkillCheckAttemptRecord> skillAttempts = [];
    private readonly SortedSet<StableId> consumedEventIds = [];
    private readonly Queue<SemanticEventRecord> eventHistory = new();

    public IReadOnlyCollection<FactRecord> Facts => factDatabase.Facts;
    public IReadOnlyCollection<KnowledgeClaim> KnowledgeClaims => knowledgeClaims.Values;
    public IReadOnlyCollection<GeneratedQuestPlan> GeneratedPlans => generatedPlans.Values;
    public IReadOnlyCollection<QuestInstanceState> Quests => quests.Values;
    public IReadOnlyCollection<ConversationSessionState> Conversations => conversations.Values;
    public IReadOnlyCollection<RumorRecord> Rumors => rumors.Values;
    public IReadOnlyCollection<NarrativeConsequenceRecord> Consequences => consequences.Values;
    public IReadOnlyCollection<RecoveryRecord> Recoveries => recoveries.Values;
    public IReadOnlyCollection<SkillCheckAttemptRecord> SkillAttempts => skillAttempts.Values;
    public IReadOnlySet<StableId> ConsumedEventIds => consumedEventIds;
    public IReadOnlyCollection<SemanticEventRecord> EventHistory => eventHistory.ToArray();
    public ulong Tick { get; private set; }
    public ulong Day { get; private set; }
    public ulong Revision { get; private set; }

    public FactDatabase FactDatabase => factDatabase;
    public void SetTime(ulong tick, ulong day) { Tick = tick; Day = day; Revision++; }
    public void UpsertFact(FactRecord fact) { factDatabase.Upsert(fact); Revision++; }
    public bool RemoveFact(StableId id) { bool result = factDatabase.Remove(id); if (result) Revision++; return result; }
    public void AddKnowledge(KnowledgeClaim claim) { knowledgeClaims[new KnowledgeLedgerKey(claim.KnowerId, claim.Id)] = claim; Revision++; }
    public IEnumerable<KnowledgeClaim> GetKnowledge(StableId knowerId) => knowledgeClaims.Where(pair => pair.Key.KnowerId == knowerId).Select(static pair => pair.Value);
    public bool Knows(StableId knowerId, StableId factId, int minimumConfidencePermille = 1) => GetKnowledge(knowerId).Any(claim => claim.FactId == factId && !claim.Contradicted && claim.ConfidencePermille >= minimumConfidencePermille);
    public void AddPlan(GeneratedQuestPlan plan) { generatedPlans[plan.Id] = plan; Revision++; }
    public void AddQuest(QuestInstanceState quest) { quests[quest.Id] = quest; generatedPlans[quest.Plan.Id] = quest.Plan; Revision++; }
    public QuestInstanceState GetQuest(StableId id) => quests.TryGetValue(id, out QuestInstanceState? value) ? value : throw new KeyNotFoundException($"Unknown quest '{id}'.");
    public bool TryGetQuest(StableId id, out QuestInstanceState? value) => quests.TryGetValue(id, out value);
    public void AddConversation(ConversationSessionState session) { conversations[session.Id] = session; Revision++; }
    public ConversationSessionState GetConversation(StableId id) => conversations.TryGetValue(id, out ConversationSessionState? value) ? value : throw new KeyNotFoundException($"Unknown conversation '{id}'.");
    public void AddRumor(RumorRecord rumor) { rumors[rumor.Id] = rumor; Revision++; }
    public void AddConsequence(NarrativeConsequenceRecord consequence) { consequences[consequence.Id] = consequence; Revision++; }
    public void AddRecovery(RecoveryRecord recovery) { recoveries[recovery.Id] = recovery; Revision++; }
    public bool TryAddSkillAttempt(SkillCheckAttemptRecord attempt) { bool added = skillAttempts.TryAdd(attempt.Id, attempt); if (added) Revision++; return added; }
    public SkillCheckAttemptRecord? FindSkillAttempt(StableId questId, StableId actorId, string approachId) => skillAttempts.Values.FirstOrDefault(attempt => attempt.QuestId == questId && attempt.ActorId == actorId && string.Equals(attempt.ApproachId, approachId, StringComparison.Ordinal));
    public bool ConsumeEvent(SemanticEventRecord value)
    {
        if (!consumedEventIds.Add(value.Id)) return false;
        eventHistory.Enqueue(value); while (eventHistory.Count > 512) _ = eventHistory.Dequeue(); Revision++; return true;
    }
    public void Touch() => Revision++;

    public byte[] ToCanonicalPayload() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ToArray(); }
    public StateChecksum ComputeChecksum() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ComputeChecksum(); }
    public void RestoreFrom(ReadOnlyMemory<byte> payload)
    {
        NarrativeWorldState source = ReadCanonical(payload); factDatabase.Clear(); foreach (FactRecord fact in source.Facts) factDatabase.Upsert(fact); knowledgeClaims.Clear(); foreach ((KnowledgeLedgerKey key, KnowledgeClaim value) in source.knowledgeClaims) knowledgeClaims.Add(key, value); generatedPlans.Clear(); foreach ((StableId id, GeneratedQuestPlan value) in source.generatedPlans) generatedPlans.Add(id, value); quests.Clear(); foreach ((StableId id, QuestInstanceState value) in source.quests) quests.Add(id, value); conversations.Clear(); foreach ((StableId id, ConversationSessionState value) in source.conversations) conversations.Add(id, value); rumors.Clear(); foreach ((StableId id, RumorRecord value) in source.rumors) rumors.Add(id, value); consequences.Clear(); foreach ((StableId id, NarrativeConsequenceRecord value) in source.consequences) consequences.Add(id, value); recoveries.Clear(); foreach ((StableId id, RecoveryRecord value) in source.recoveries) recoveries.Add(id, value); skillAttempts.Clear(); foreach ((StableId id, SkillCheckAttemptRecord value) in source.skillAttempts) skillAttempts.Add(id, value); consumedEventIds.Clear(); foreach (StableId id in source.consumedEventIds) consumedEventIds.Add(id); eventHistory.Clear(); foreach (SemanticEventRecord value in source.eventHistory) eventHistory.Enqueue(value); Tick = source.Tick; Day = source.Day; Revision = source.Revision;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32(SchemaVersion); writer.WriteUInt64(Tick); writer.WriteUInt64(Day); writer.WriteCount(Facts.Count); foreach (FactRecord fact in Facts.OrderBy(static value => value.Id)) fact.WriteCanonical(writer); writer.WriteCount(knowledgeClaims.Count); foreach (KnowledgeClaim claim in knowledgeClaims.Values) claim.WriteCanonical(writer); writer.WriteCount(generatedPlans.Count); foreach (GeneratedQuestPlan plan in generatedPlans.Values) plan.WriteCanonical(writer); writer.WriteCount(quests.Count); foreach (QuestInstanceState quest in quests.Values) quest.WriteCanonical(writer); writer.WriteCount(conversations.Count); foreach (ConversationSessionState conversation in conversations.Values) conversation.WriteCanonical(writer); writer.WriteCount(rumors.Count); foreach (RumorRecord rumor in rumors.Values) rumor.WriteCanonical(writer); writer.WriteCount(consequences.Count); foreach (NarrativeConsequenceRecord consequence in consequences.Values) consequence.WriteCanonical(writer); writer.WriteCount(recoveries.Count); foreach (RecoveryRecord recovery in recoveries.Values) recovery.WriteCanonical(writer); writer.WriteCount(skillAttempts.Count); foreach (SkillCheckAttemptRecord attempt in skillAttempts.Values) attempt.WriteCanonical(writer); writer.WriteCount(consumedEventIds.Count); foreach (StableId id in consumedEventIds) writer.WriteStableId(id); writer.WriteCount(eventHistory.Count); foreach (SemanticEventRecord value in eventHistory) value.WriteCanonical(writer); writer.WriteUInt64(Revision);
    }

    public static NarrativeWorldState ReadCanonical(ReadOnlyMemory<byte> payload) { var reader = new CanonicalReader(payload); NarrativeWorldState result = ReadCanonical(reader); reader.EnsureComplete(); return result; }
    public static NarrativeWorldState ReadCanonical(CanonicalReader reader)
    {
        int schema = reader.ReadInt32(); if (schema != SchemaVersion) throw new InvalidDataException($"Unsupported narrative schema {schema}; expected {SchemaVersion}."); var result = new NarrativeWorldState { Tick = reader.ReadUInt64(), Day = reader.ReadUInt64() }; int factCount = reader.ReadCount(); for (int i = 0; i < factCount; i++) result.factDatabase.Upsert(FactRecord.ReadCanonical(reader)); int claimCount = reader.ReadCount(); for (int i = 0; i < claimCount; i++) { KnowledgeClaim claim = KnowledgeClaim.ReadCanonical(reader); result.knowledgeClaims.Add(new KnowledgeLedgerKey(claim.KnowerId, claim.Id), claim); } int planCount = reader.ReadCount(); for (int i = 0; i < planCount; i++) { GeneratedQuestPlan plan = GeneratedQuestPlan.ReadCanonical(reader); result.generatedPlans.Add(plan.Id, plan); } int questCount = reader.ReadCount(); for (int i = 0; i < questCount; i++) { QuestInstanceState quest = QuestInstanceState.ReadCanonical(reader); result.quests.Add(quest.Id, quest); } int conversationCount = reader.ReadCount(); for (int i = 0; i < conversationCount; i++) { ConversationSessionState conversation = ConversationSessionState.ReadCanonical(reader); result.conversations.Add(conversation.Id, conversation); } int rumorCount = reader.ReadCount(); for (int i = 0; i < rumorCount; i++) { RumorRecord rumor = RumorRecord.ReadCanonical(reader); result.rumors.Add(rumor.Id, rumor); } int consequenceCount = reader.ReadCount(); for (int i = 0; i < consequenceCount; i++) { NarrativeConsequenceRecord consequence = NarrativeConsequenceRecord.ReadCanonical(reader); result.consequences.Add(consequence.Id, consequence); } int recoveryCount = reader.ReadCount(); for (int i = 0; i < recoveryCount; i++) { RecoveryRecord recovery = RecoveryRecord.ReadCanonical(reader); result.recoveries.Add(recovery.Id, recovery); } int attemptCount = reader.ReadCount(); for (int i = 0; i < attemptCount; i++) { SkillCheckAttemptRecord attempt = SkillCheckAttemptRecord.ReadCanonical(reader); result.skillAttempts.Add(attempt.Id, attempt); } int consumedCount = reader.ReadCount(); for (int i = 0; i < consumedCount; i++) result.consumedEventIds.Add(reader.ReadStableId()); int historyCount = reader.ReadCount(); for (int i = 0; i < historyCount; i++) result.eventHistory.Enqueue(SemanticEventRecord.ReadCanonical(reader)); result.Revision = reader.ReadUInt64(); return result;
    }
}
