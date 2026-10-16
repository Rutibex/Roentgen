using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Narrative;

public sealed record QuestUpdateResult(StableId QuestId, IReadOnlyList<string> ActivatedObjectives, IReadOnlyList<string> CompletedObjectives, bool QuestCompleted, IReadOnlyList<string> Errors);

public sealed class QuestRuntimeEngine
{
    private readonly NarrativeConditionEvaluator conditionEvaluator = new();
    private readonly NarrativeEffectExecutor effectExecutor = new();

    public QuestUpdateResult Reconcile(NarrativeWorldState world, QuestInstanceState quest, INarrativeQueryContext context, INarrativeExternalEffectRouter externalRouter, StableId responsibleActorId)
    {
        var activated = new List<string>(); var completed = new List<string>(); var errors = new List<string>();
        if (quest.Status is QuestStatus.Generated or QuestStatus.Offered) quest.Start(context.Tick);
        foreach (QuestObjectiveDefinition objective in quest.Plan.Kernel.Objectives.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            ObjectiveRuntimeState runtime = quest.Objectives[objective.Id];
            if (runtime.Status == ObjectiveStatus.Dormant && (!objective.Hidden || IsObjectiveRelevant(objective, quest))) { quest.ActivateObjective(objective.Id, context.Tick); activated.Add(objective.Id); runtime = quest.Objectives[objective.Id]; }
            if (runtime.Status != ObjectiveStatus.Active) continue;
            if (conditionEvaluator.Evaluate(objective.FailureCondition, quest.Plan, context) && objective.FailureCondition.Kind != NarrativeConditionKind.Always) { quest.FailObjective(objective.Id, context.Tick); continue; }
            if (!conditionEvaluator.Evaluate(objective.CompletionCondition, quest.Plan, context)) continue;
            NarrativeEffectBatchResult result = effectExecutor.Apply(world, quest, responsibleActorId, objective.CompletionEffects, externalRouter, context.Tick);
            if (!result.Success) { errors.AddRange(result.Errors); continue; }
            quest.CompleteObjective(objective.Id, context.Tick); completed.Add(objective.Id);
        }
        bool requiredComplete = quest.Plan.Kernel.Objectives.Where(static objective => !objective.Optional).All(objective => quest.Objectives[objective.Id].Status == ObjectiveStatus.Completed);
        if (requiredComplete && quest.Status == QuestStatus.Active)
        {
            NarrativeEffectBatchResult consequenceResult = effectExecutor.Apply(world, quest, responsibleActorId, quest.Plan.Kernel.Consequences, externalRouter, context.Tick);
            if (consequenceResult.Success) quest.Complete(context.Tick); else errors.AddRange(consequenceResult.Errors);
        }
        world.Touch();
        return new QuestUpdateResult(quest.Id, activated, completed, quest.Status == QuestStatus.Completed, errors);
    }

    public IReadOnlyList<QuestUpdateResult> IngestEvent(NarrativeWorldState world, SemanticEventRecord semanticEvent, INarrativeQueryContext context, INarrativeExternalEffectRouter externalRouter, StableId responsibleActorId)
    {
        if (!world.ConsumeEvent(semanticEvent)) return [];
        ProjectSemanticFacts(world, semanticEvent);
        return world.Quests.Where(static quest => quest.Status is QuestStatus.Active or QuestStatus.Offered or QuestStatus.Generated).OrderBy(static quest => quest.Id).Select(quest => Reconcile(world, quest, context, externalRouter, responsibleActorId)).ToArray();
    }

    public SkillCheckAttemptRecord ResolveSkillCheck(NarrativeWorldState world, QuestInstanceState quest, StableId actorId, string approachId, SkillId skill, int difficulty, RetryPolicy retryPolicy, int effectiveSkill, bool supportsCostlySuccess, ulong tick)
    {
        SkillCheckAttemptRecord? previous = world.FindSkillAttempt(quest.Id, actorId, approachId);
        if (previous is not null && retryPolicy == RetryPolicy.Never) return previous;
        int margin = effectiveSkill - difficulty;
        SkillCheckOutcome outcome = margin switch { >= 20 => SkillCheckOutcome.ExceptionalSuccess, >= 0 => SkillCheckOutcome.Success, >= -10 when supportsCostlySuccess => SkillCheckOutcome.CostlySuccess, _ => SkillCheckOutcome.Failure };
        string key = retryPolicy == RetryPolicy.DistinctApproach ? $"{approachId}:{world.SkillAttempts.Count(attempt => attempt.QuestId == quest.Id && attempt.ActorId == actorId && attempt.ApproachId.StartsWith(approachId, StringComparison.Ordinal))}" : approachId;
        StableId id = StableIdFactory.CreateChild(quest.Id, "skill-attempt", $"{actorId}:{key}");
        var attempt = new SkillCheckAttemptRecord(id, quest.Id, actorId, skill, difficulty, retryPolicy, key, effectiveSkill, outcome, tick);
        world.TryAddSkillAttempt(attempt);
        return attempt;
    }

    private static bool IsObjectiveRelevant(QuestObjectiveDefinition objective, QuestInstanceState quest) => quest.Plan.Kernel.Routes.Any(route => route.RequiredObjectiveIds.Contains(objective.Id, StringComparer.Ordinal));

    private static void ProjectSemanticFacts(NarrativeWorldState world, SemanticEventRecord semanticEvent)
    {
        foreach ((string key, NarrativeValue value) in semanticEvent.Payload.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            StableId subject = semanticEvent.Participants.FirstOrDefault();
            StableId factId = StableIdFactory.CreateChild(semanticEvent.Id, "event-fact", key);
            world.UpsertFact(new FactRecord(factId, $"event.{semanticEvent.EventType}.{key}", subject, value, FactTruthState.True, FactScope.World, semanticEvent.Id, semanticEvent.Tick, null));
        }
        world.UpsertFact(new FactRecord(StableIdFactory.CreateChild(semanticEvent.Id, "event-fact", "occurred"), $"event.{semanticEvent.EventType}.occurred", semanticEvent.LocationId, NarrativeValue.FromBoolean(true), FactTruthState.True, FactScope.World, semanticEvent.Id, semanticEvent.Tick, null));
    }
}
