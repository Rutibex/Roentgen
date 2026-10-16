using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Narrative;

public enum FactTruthState { Unknown, False, True, Disputed }
public enum FactScope { World, Region, Settlement, Faction, Actor, Quest }
public enum NarrativeValueKind { None, Boolean, Integer, FixedPermille, StableId, Token, Tick, Location, StableIdList }
public enum KnowledgeConfidence { Speculative = 200, Uncertain = 400, Plausible = 600, Confident = 800, Certain = 1000 }
public enum KnowledgeSecrecy { Public, FactionInternal, Restricted, Professional, Personal, Criminal, Classified, QuestSecret, Inferred, RumorOnly }
public enum KnowledgeSourceKind { WorldGeneration, Observation, WitnessReport, DiagnosticAnalysis, FactionCommunication, Rumor, Dialogue, Document, Inference, QuestConsequence, StrategicEvent }
public enum QuestStatus { Generated, Offered, Active, Completed, Failed, Suspended }
public enum ObjectiveStatus { Dormant, Active, Completed, Failed, Skipped }
public enum ObjectiveKind { Speak, Reach, DiscoverFact, AcquireItem, DeliverItem, ExamineEvidence, Diagnose, Repair, AlterMachine, ResolveCrime, Negotiate, ChangeService, ProtectActor, DefeatThreat, CauseSurrender, Escape, EscortShipment, WaitForEvent, SurviveUntil, ChooseOutcome }
public enum ObjectiveComposition { Single, All, Any, Ordered, MutuallyExclusive }
public enum QuestNodeKind { Stage, Objective, Choice, Gate, Consequence, Failure, Recovery, Completion, FollowUp }
public enum QuestRouteKind { Combat, Stealth, Technical, Scientific, Medical, Social, Economic, FactionLeverage, Exploration }
public enum RetryPolicy { Never, AfterSkillIncrease, AfterEvidence, AfterFactionLeverage, AfterEquipmentChange, AfterEvent, DistinctApproach }
public enum DialogueIntent { Greet, Identify, Ask, Explain, Accuse, Deny, Lie, Threaten, Bargain, Request, Offer, Warn, Confess, Reveal, Deflect, Surrender, Arrest, End }
public enum ConversationEndReason { None, PlayerEnded, SpeakerEnded, Interrupted, Combat, Invalidated }
public enum RumorStatus { Active, Confirmed, Disproved, Superseded, Corrected, Forgotten }
public enum RecoveryKind { ActorReplacement, AlternateInformant, EvidenceSubstitute, AlternateLocation, ItemReacquisition, CraftableSubstitute, ObjectiveDowngrade, AftermathRoute, FactionMediation, DelayedRetry }
public enum NarrativeConditionKind { Always, FactExists, FactEquals, ActorAlive, ActorKnowsFact, FactionKnowsFact, ItemExists, ActorHasItem, LocationReachable, ServiceOperating, RelationshipRange, ReputationRange, CrimeStatus, DisguiseBelieved, QuestState, ObjectiveState, SkillCapability, BehaviorTag, TimeBefore, TimeAfter, And, Or, Not }
public enum NarrativeEffectKind { AssertFact, RetractFact, GrantKnowledge, DiscloseFact, ActivateObjective, CompleteObjective, FailObjective, StartQuest, CompleteQuest, ChangeReputation, RecordCrime, TransferItem, ConsumeItem, UnlockLocation, ChangeServiceState, AlterTradeRoute, ReserveEncounter, ChangeRelationship, ApplyStatus, GrantExperience, GrantRecipe, ScheduleFollowUp }

public readonly record struct NarrativeValue : ICanonicalWritable, IComparable<NarrativeValue>
{
    private NarrativeValue(NarrativeValueKind kind, bool booleanValue, long integerValue, StableId stableIdValue, string? tokenValue, IReadOnlyList<StableId>? stableIds)
    {
        Kind = kind;
        BooleanValue = booleanValue;
        IntegerValue = integerValue;
        StableIdValue = stableIdValue;
        TokenValue = tokenValue;
        StableIds = stableIds ?? [];
    }

    public NarrativeValueKind Kind { get; }
    public bool BooleanValue { get; }
    public long IntegerValue { get; }
    public StableId StableIdValue { get; }
    public string? TokenValue { get; }
    public IReadOnlyList<StableId> StableIds { get; }

    public static NarrativeValue None => new(NarrativeValueKind.None, false, 0, StableId.Empty, null, null);
    public static NarrativeValue FromBoolean(bool value) => new(NarrativeValueKind.Boolean, value, 0, StableId.Empty, null, null);
    public static NarrativeValue FromInteger(long value) => new(NarrativeValueKind.Integer, false, value, StableId.Empty, null, null);
    public static NarrativeValue FromPermille(int value) => new(NarrativeValueKind.FixedPermille, false, Math.Clamp(value, -1000, 1000), StableId.Empty, null, null);
    public static NarrativeValue FromStableId(StableId value) => new(NarrativeValueKind.StableId, false, 0, value, null, null);
    public static NarrativeValue FromToken(string value) => new(NarrativeValueKind.Token, false, 0, StableId.Empty, value ?? throw new ArgumentNullException(nameof(value)), null);
    public static NarrativeValue FromTick(ulong value) => new(NarrativeValueKind.Tick, false, checked((long)value), StableId.Empty, null, null);
    public static NarrativeValue FromLocation(StableId value) => new(NarrativeValueKind.Location, false, 0, value, null, null);
    public static NarrativeValue FromStableIds(IEnumerable<StableId> values) => new(NarrativeValueKind.StableIdList, false, 0, StableId.Empty, null, values.Order().Distinct().ToArray());

    public int CompareTo(NarrativeValue other)
    {
        int kind = Kind.CompareTo(other.Kind);
        if (kind != 0) return kind;
        return Kind switch
        {
            NarrativeValueKind.Boolean => BooleanValue.CompareTo(other.BooleanValue),
            NarrativeValueKind.Integer or NarrativeValueKind.FixedPermille or NarrativeValueKind.Tick => IntegerValue.CompareTo(other.IntegerValue),
            NarrativeValueKind.StableId or NarrativeValueKind.Location => StableIdValue.CompareTo(other.StableIdValue),
            NarrativeValueKind.Token => string.Compare(TokenValue, other.TokenValue, StringComparison.Ordinal),
            NarrativeValueKind.StableIdList => CompareIds(StableIds, other.StableIds),
            _ => 0
        };
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)Kind);
        switch (Kind)
        {
            case NarrativeValueKind.None: break;
            case NarrativeValueKind.Boolean: writer.WriteBoolean(BooleanValue); break;
            case NarrativeValueKind.Integer:
            case NarrativeValueKind.FixedPermille:
            case NarrativeValueKind.Tick: writer.WriteInt64(IntegerValue); break;
            case NarrativeValueKind.StableId:
            case NarrativeValueKind.Location: writer.WriteStableId(StableIdValue); break;
            case NarrativeValueKind.Token: writer.WriteString(TokenValue); break;
            case NarrativeValueKind.StableIdList:
                writer.WriteCount(StableIds.Count);
                foreach (StableId id in StableIds) writer.WriteStableId(id);
                break;
            default: throw new InvalidDataException($"Unsupported narrative value kind {Kind}.");
        }
    }

    public static NarrativeValue ReadCanonical(CanonicalReader reader)
    {
        NarrativeValueKind kind = (NarrativeValueKind)reader.ReadInt32();
        return kind switch
        {
            NarrativeValueKind.None => None,
            NarrativeValueKind.Boolean => FromBoolean(reader.ReadBoolean()),
            NarrativeValueKind.Integer => FromInteger(reader.ReadInt64()),
            NarrativeValueKind.FixedPermille => FromPermille(checked((int)reader.ReadInt64())),
            NarrativeValueKind.StableId => FromStableId(reader.ReadStableId()),
            NarrativeValueKind.Token => FromToken(reader.ReadRequiredString()),
            NarrativeValueKind.Tick => FromTick(checked((ulong)reader.ReadInt64())),
            NarrativeValueKind.Location => FromLocation(reader.ReadStableId()),
            NarrativeValueKind.StableIdList => ReadIds(reader),
            _ => throw new InvalidDataException($"Unsupported narrative value kind {kind}.")
        };
    }

    public override string ToString() => Kind switch
    {
        NarrativeValueKind.None => "none",
        NarrativeValueKind.Boolean => BooleanValue ? "true" : "false",
        NarrativeValueKind.Integer => IntegerValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        NarrativeValueKind.FixedPermille => $"{IntegerValue}/1000",
        NarrativeValueKind.StableId or NarrativeValueKind.Location => StableIdValue.ToString(),
        NarrativeValueKind.Token => TokenValue ?? string.Empty,
        NarrativeValueKind.Tick => $"tick:{IntegerValue}",
        NarrativeValueKind.StableIdList => string.Join(',', StableIds),
        _ => string.Empty
    };

    private static NarrativeValue ReadIds(CanonicalReader reader)
    {
        int count = reader.ReadCount();
        var ids = new StableId[count];
        for (int index = 0; index < count; index++) ids[index] = reader.ReadStableId();
        return FromStableIds(ids);
    }

    private static int CompareIds(IReadOnlyList<StableId> left, IReadOnlyList<StableId> right)
    {
        int count = left.Count.CompareTo(right.Count);
        if (count != 0) return count;
        for (int index = 0; index < left.Count; index++)
        {
            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0) return comparison;
        }
        return 0;
    }
}

public sealed record FactRecord(
    StableId Id,
    string PredicateId,
    StableId SubjectId,
    NarrativeValue Object,
    FactTruthState Truth,
    FactScope Scope,
    StableId ProvenanceId,
    ulong EstablishedTick,
    ulong? ExpirationTick,
    ulong Revision = 0) : ICanonicalWritable
{
    public bool IsExpired(ulong tick) => ExpirationTick.HasValue && ExpirationTick.Value <= tick;
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteString(PredicateId); writer.WriteStableId(SubjectId); Object.WriteCanonical(writer);
        writer.WriteInt32((int)Truth); writer.WriteInt32((int)Scope); writer.WriteStableId(ProvenanceId); writer.WriteUInt64(EstablishedTick);
        writer.WriteBoolean(ExpirationTick.HasValue); if (ExpirationTick.HasValue) writer.WriteUInt64(ExpirationTick.Value); writer.WriteUInt64(Revision);
    }
    public static FactRecord ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId(); string predicate = reader.ReadRequiredString(); StableId subject = reader.ReadStableId(); NarrativeValue value = NarrativeValue.ReadCanonical(reader);
        FactTruthState truth = (FactTruthState)reader.ReadInt32(); FactScope scope = (FactScope)reader.ReadInt32(); StableId provenance = reader.ReadStableId(); ulong tick = reader.ReadUInt64();
        ulong? expiration = reader.ReadBoolean() ? reader.ReadUInt64() : null; return new(id, predicate, subject, value, truth, scope, provenance, tick, expiration, reader.ReadUInt64());
    }
}

public sealed record KnowledgeSource(StableId Id, KnowledgeSourceKind Kind, StableId SourceEntityId, string Description, ulong Tick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteInt32((int)Kind); writer.WriteStableId(SourceEntityId); writer.WriteString(Description); writer.WriteUInt64(Tick); }
    public static KnowledgeSource ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), (KnowledgeSourceKind)reader.ReadInt32(), reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadUInt64());
}

public sealed record KnowledgeClaim(
    StableId Id,
    StableId KnowerId,
    StableId FactId,
    FactTruthState BelievedTruth,
    int ConfidencePermille,
    KnowledgeSource Source,
    int SourceTrustPermille,
    ulong AcquiredTick,
    ulong LastConfirmedTick,
    KnowledgeSecrecy Secrecy,
    bool Contradicted,
    bool MayDisclose) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteStableId(KnowerId); writer.WriteStableId(FactId); writer.WriteInt32((int)BelievedTruth); writer.WriteInt32(ConfidencePermille);
        Source.WriteCanonical(writer); writer.WriteInt32(SourceTrustPermille); writer.WriteUInt64(AcquiredTick); writer.WriteUInt64(LastConfirmedTick); writer.WriteInt32((int)Secrecy); writer.WriteBoolean(Contradicted); writer.WriteBoolean(MayDisclose);
    }
    public static KnowledgeClaim ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (FactTruthState)reader.ReadInt32(), reader.ReadInt32(), KnowledgeSource.ReadCanonical(reader), reader.ReadInt32(), reader.ReadUInt64(), reader.ReadUInt64(), (KnowledgeSecrecy)reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean());
}

public sealed record SemanticEventRecord(StableId Id, string EventType, ulong Tick, StableId SourceDomainId, StableId LocationId, IReadOnlyList<StableId> Participants, IReadOnlyDictionary<string, NarrativeValue> Payload) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteString(EventType); writer.WriteUInt64(Tick); writer.WriteStableId(SourceDomainId); writer.WriteStableId(LocationId);
        writer.WriteCount(Participants.Count); foreach (StableId id in Participants.Order()) writer.WriteStableId(id);
        writer.WriteCount(Payload.Count); foreach ((string key, NarrativeValue value) in Payload.OrderBy(static item => item.Key, StringComparer.Ordinal)) { writer.WriteString(key); value.WriteCanonical(writer); }
    }
    public static SemanticEventRecord ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId(); string type = reader.ReadRequiredString(); ulong tick = reader.ReadUInt64(); StableId source = reader.ReadStableId(); StableId location = reader.ReadStableId();
        int participantCount = reader.ReadCount(); var participants = new StableId[participantCount]; for (int index = 0; index < participantCount; index++) participants[index] = reader.ReadStableId();
        int payloadCount = reader.ReadCount(); var payload = new SortedDictionary<string, NarrativeValue>(StringComparer.Ordinal); for (int index = 0; index < payloadCount; index++) payload.Add(reader.ReadRequiredString(), NarrativeValue.ReadCanonical(reader));
        return new(id, type, tick, source, location, participants, payload);
    }
}

public sealed record SkillCheckAttemptRecord(StableId Id, StableId QuestId, StableId ActorId, SkillId Skill, int Difficulty, RetryPolicy RetryPolicy, string ApproachId, int EffectiveSkill, SkillCheckOutcome Outcome, ulong Tick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(QuestId); writer.WriteStableId(ActorId); writer.WriteInt32((int)Skill); writer.WriteInt32(Difficulty); writer.WriteInt32((int)RetryPolicy); writer.WriteString(ApproachId); writer.WriteInt32(EffectiveSkill); writer.WriteInt32((int)Outcome); writer.WriteUInt64(Tick); }
    public static SkillCheckAttemptRecord ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (SkillId)reader.ReadInt32(), reader.ReadInt32(), (RetryPolicy)reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadInt32(), (SkillCheckOutcome)reader.ReadInt32(), reader.ReadUInt64());
}
