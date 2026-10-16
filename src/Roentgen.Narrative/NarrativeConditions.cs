using Roentgen.Contracts;
using Roentgen.Rpg;

namespace Roentgen.Narrative;

public interface INarrativeQueryContext
{
    NarrativeWorldState Narrative { get; }
    ulong Tick { get; }
    bool ActorAlive(StableId actorId);
    bool ItemExists(StableId itemId);
    bool ActorHasItem(StableId actorId, StableId itemId);
    bool LocationReachable(StableId locationId);
    bool ServiceOperating(StableId serviceId);
    int RelationshipValue(StableId sourceId, StableId targetId, string dimension);
    int ReputationValue(StableId subjectId, StableId factionId, string dimension);
    bool CrimeHasStatus(StableId crimeId, string status);
    bool DisguiseBelieved(StableId observerId, StableId disguiseId);
    int SkillCapability(StableId actorId, SkillId skill);
    bool HasBehaviorTag(StableId actorId, string tag);
}

public sealed class NarrativeConditionEvaluator
{
    public bool Evaluate(NarrativeConditionDefinition condition, GeneratedQuestPlan plan, INarrativeQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(condition); ArgumentNullException.ThrowIfNull(plan); ArgumentNullException.ThrowIfNull(context);
        StableId target = string.IsNullOrWhiteSpace(condition.SlotId) ? StableId.Empty : plan.ResolveSlot(condition.SlotId);
        return condition.Kind switch
        {
            NarrativeConditionKind.Always => true,
            NarrativeConditionKind.FactExists => context.Narrative.FactDatabase.Exists(condition.PredicateId, target, condition.ExpectedValue.Kind == NarrativeValueKind.None ? null : condition.ExpectedValue, FactTruthState.True, context.Tick),
            NarrativeConditionKind.FactEquals => context.Narrative.FactDatabase.Exists(condition.PredicateId, target, condition.ExpectedValue, FactTruthState.True, context.Tick),
            NarrativeConditionKind.ActorAlive => context.ActorAlive(target),
            NarrativeConditionKind.ActorKnowsFact => context.Narrative.Knows(target, ResolveFactId(condition, plan), condition.Minimum),
            NarrativeConditionKind.FactionKnowsFact => context.Narrative.Knows(target, ResolveFactId(condition, plan), condition.Minimum),
            NarrativeConditionKind.ItemExists => context.ItemExists(target),
            NarrativeConditionKind.ActorHasItem => context.ActorHasItem(target, condition.ExpectedValue.StableIdValue),
            NarrativeConditionKind.LocationReachable => context.LocationReachable(target),
            NarrativeConditionKind.ServiceOperating => context.ServiceOperating(target),
            NarrativeConditionKind.RelationshipRange => InRange(context.RelationshipValue(target, condition.ExpectedValue.StableIdValue, condition.PredicateId), condition.Minimum, condition.Maximum),
            NarrativeConditionKind.ReputationRange => InRange(context.ReputationValue(target, condition.ExpectedValue.StableIdValue, condition.PredicateId), condition.Minimum, condition.Maximum),
            NarrativeConditionKind.CrimeStatus => context.CrimeHasStatus(target, condition.ExpectedValue.TokenValue ?? string.Empty),
            NarrativeConditionKind.DisguiseBelieved => context.DisguiseBelieved(target, condition.ExpectedValue.StableIdValue),
            NarrativeConditionKind.QuestState => context.Narrative.TryGetQuest(target, out QuestInstanceState? quest) && string.Equals(quest.Status.ToString(), condition.ExpectedValue.TokenValue, StringComparison.OrdinalIgnoreCase),
            NarrativeConditionKind.ObjectiveState => EvaluateObjective(condition, plan, context),
            NarrativeConditionKind.SkillCapability => context.SkillCapability(target, (SkillId)condition.Minimum) >= condition.Maximum,
            NarrativeConditionKind.BehaviorTag => context.HasBehaviorTag(target, condition.ExpectedValue.TokenValue ?? string.Empty),
            NarrativeConditionKind.TimeBefore => context.Tick < checked((ulong)Math.Max(0, condition.Maximum)),
            NarrativeConditionKind.TimeAfter => context.Tick >= checked((ulong)Math.Max(0, condition.Minimum)),
            NarrativeConditionKind.And => condition.Children.All(child => Evaluate(child, plan, context)),
            NarrativeConditionKind.Or => condition.Children.Any(child => Evaluate(child, plan, context)),
            NarrativeConditionKind.Not => condition.Children.Count == 1 && !Evaluate(condition.Children[0], plan, context),
            _ => false
        };
    }

    private static StableId ResolveFactId(NarrativeConditionDefinition condition, GeneratedQuestPlan plan)
    {
        if (condition.ExpectedValue.Kind == NarrativeValueKind.StableId) return condition.ExpectedValue.StableIdValue;
        return plan.ResolveSlot(condition.PredicateId);
    }

    private static bool EvaluateObjective(NarrativeConditionDefinition condition, GeneratedQuestPlan plan, INarrativeQueryContext context)
    {
        if (!context.Narrative.TryGetQuest(plan.Id, out QuestInstanceState? quest) || quest is null) return false;
        return quest.Objectives.TryGetValue(condition.PredicateId, out ObjectiveRuntimeState? state) && string.Equals(state.Status.ToString(), condition.ExpectedValue.TokenValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InRange(int value, int minimum, int maximum) => value >= minimum && (maximum == 0 || value <= maximum);
}

public sealed class BasicNarrativeQueryContext : INarrativeQueryContext
{
    private readonly Func<StableId, bool> actorAlive;
    private readonly Func<StableId, bool> itemExists;
    private readonly Func<StableId, StableId, bool> actorHasItem;
    private readonly Func<StableId, bool> locationReachable;
    private readonly Func<StableId, bool> serviceOperating;
    private readonly Func<StableId, StableId, string, int> relationship;
    private readonly Func<StableId, StableId, string, int> reputation;
    private readonly Func<StableId, string, bool> crimeStatus;
    private readonly Func<StableId, StableId, bool> disguiseBelieved;
    private readonly Func<StableId, SkillId, int> skill;
    private readonly Func<StableId, string, bool> behaviorTag;

    public BasicNarrativeQueryContext(NarrativeWorldState narrative, ulong tick,
        Func<StableId, bool>? actorAlive = null, Func<StableId, bool>? itemExists = null,
        Func<StableId, StableId, bool>? actorHasItem = null, Func<StableId, bool>? locationReachable = null,
        Func<StableId, bool>? serviceOperating = null, Func<StableId, StableId, string, int>? relationship = null,
        Func<StableId, StableId, string, int>? reputation = null, Func<StableId, string, bool>? crimeStatus = null,
        Func<StableId, StableId, bool>? disguiseBelieved = null, Func<StableId, SkillId, int>? skill = null,
        Func<StableId, string, bool>? behaviorTag = null)
    {
        Narrative = narrative; Tick = tick; this.actorAlive = actorAlive ?? (_ => true); this.itemExists = itemExists ?? (_ => true); this.actorHasItem = actorHasItem ?? ((_, _) => false); this.locationReachable = locationReachable ?? (_ => true); this.serviceOperating = serviceOperating ?? (_ => true); this.relationship = relationship ?? ((_, _, _) => 0); this.reputation = reputation ?? ((_, _, _) => 0); this.crimeStatus = crimeStatus ?? ((_, _) => false); this.disguiseBelieved = disguiseBelieved ?? ((_, _) => false); this.skill = skill ?? ((_, _) => 0); this.behaviorTag = behaviorTag ?? ((_, _) => false);
    }
    public NarrativeWorldState Narrative { get; }
    public ulong Tick { get; }
    public bool ActorAlive(StableId actorId) => actorAlive(actorId);
    public bool ItemExists(StableId itemId) => itemExists(itemId);
    public bool ActorHasItem(StableId actorId, StableId itemId) => actorHasItem(actorId, itemId);
    public bool LocationReachable(StableId locationId) => locationReachable(locationId);
    public bool ServiceOperating(StableId serviceId) => serviceOperating(serviceId);
    public int RelationshipValue(StableId sourceId, StableId targetId, string dimension) => relationship(sourceId, targetId, dimension);
    public int ReputationValue(StableId subjectId, StableId factionId, string dimension) => reputation(subjectId, factionId, dimension);
    public bool CrimeHasStatus(StableId crimeId, string status) => crimeStatus(crimeId, status);
    public bool DisguiseBelieved(StableId observerId, StableId disguiseId) => disguiseBelieved(observerId, disguiseId);
    public int SkillCapability(StableId actorId, SkillId skillId) => skill(actorId, skillId);
    public bool HasBehaviorTag(StableId actorId, string tag) => behaviorTag(actorId, tag);
}
