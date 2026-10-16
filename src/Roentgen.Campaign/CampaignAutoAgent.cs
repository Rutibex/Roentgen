using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Narrative;
using Roentgen.Rpg;

namespace Roentgen.Campaign;

public sealed record CampaignAutoCompletionResult(
    string BuildId,
    bool Completed,
    StableId EndingId,
    int Iterations,
    IReadOnlyList<string> Trace,
    IReadOnlyList<string> Errors,
    StateChecksum FinalChecksum);

public sealed class CampaignAutoAgent
{
    public CampaignAutoCompletionResult Complete(
        GeneratedCampaignPlan plan,
        SocietyWorldState society,
        AbstractQuestBuild build,
        StableId playerId,
        int maximumIterations = 256)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(society);
        ArgumentNullException.ThrowIfNull(build);
        var campaign = new CampaignWorldState(plan);
        var narrative = new NarrativeWorldState();
        var director = new CampaignDirector();
        var questRuntime = new QuestRuntimeEngine();
        var solver = new AbstractQuestSolver();
        var router = new NoOpNarrativeExternalEffectRouter();
        var trace = new List<string>();
        var errors = new List<string>();
        ulong tick = 1;
        ulong day = 0;
        _ = director.Initialize(campaign, narrative, society, playerId, tick, day);

        for (int iteration = 0; iteration < maximumIterations && !campaign.Completed; iteration++)
        {
            tick += 60;
            day = (ulong)(iteration / 3);
            campaign.SetTime(tick, day);
            narrative.SetTime(tick, day);
            GeneratedMilestonePlan? activePlan = plan.Milestones.OrderBy(static value => value.Sequence).FirstOrDefault(value => campaign.GetMilestone(value.Id).Status is CampaignMilestoneStatus.Offered or CampaignMilestoneStatus.Active or CampaignMilestoneStatus.AwaitingDecision);
            if (activePlan is null)
            {
                CampaignUpdateResult update = director.Update(campaign, narrative, society, playerId, tick, day);
                errors.AddRange(update.Errors);
                trace.AddRange(update.OfferedMilestoneIds.Select(id => $"offered:{id}"));
                continue;
            }

            CampaignMilestoneRuntimeState milestoneState = campaign.GetMilestone(activePlan.Id);
            if (milestoneState.Status == CampaignMilestoneStatus.AwaitingDecision)
            {
                IrreversibleChoiceDefinition choice = plan.GetChoice(activePlan.ChoiceId);
                CampaignChoiceOption option = SelectChoice(choice, build);
                _ = director.CommitChoice(campaign, society, choice.Id, option.Id, playerId, narrative.Facts.Select(static value => value.Id), tick, day);
                trace.Add($"choice:{choice.Id}:{option.Id}");
                _ = director.Update(campaign, narrative, society, playerId, tick, day);
                continue;
            }

            if (!narrative.TryGetQuest(activePlan.QuestPlan.Id, out QuestInstanceState? quest) || quest is null)
            {
                errors.Add($"missing-quest:{activePlan.QuestPlan.Id}");
                break;
            }

            if (quest.Status != QuestStatus.Completed)
            {
                AbstractQuestSolveResult solve = solver.Solve(activePlan.QuestPlan, build);
                if (!solve.Solved)
                {
                    errors.Add($"unsolved:{activePlan.Kernel.Id}:{solve.FailureReason}");
                    break;
                }
                BasicNarrativeQueryContext query = CreateQuery(narrative, tick, playerId, build);
                SatisfyQuestConditions(narrative, quest, activePlan.QuestPlan, tick);
                QuestUpdateResult result = questRuntime.Reconcile(narrative, quest, query, router, playerId);
                if (result.Errors.Count > 0) errors.AddRange(result.Errors.Select(value => $"quest:{activePlan.Kernel.Id}:{value}"));
                if (result.QuestCompleted) quest.CompleteRoute(solve.RouteId);
                trace.Add($"quest:{activePlan.Kernel.Id}:{solve.RouteId}:{result.QuestCompleted}");
            }

            CampaignUpdateResult directorResult = director.Update(campaign, narrative, society, playerId, tick, day);
            errors.AddRange(directorResult.Errors);
            if (activePlan.Kernel.Role == CampaignMilestoneRole.FinalConvergence && quest.Status == QuestStatus.Completed && !campaign.ConvergenceResolution.HasValue)
            {
                ConvergenceForm form = SelectConvergence(plan.Convergence, build);
                try
                {
                    director.ResolveConvergence(campaign, form, playerId, tick);
                    trace.Add($"convergence:{form}");
                }
                catch (InvalidOperationException exception)
                {
                    errors.Add($"convergence:{exception.Message}");
                    break;
                }
            }
            _ = director.Update(campaign, narrative, society, playerId, tick, day);
        }

        CampaignValidationReport validation = new CampaignStateValidator().Validate(campaign);
        errors.AddRange(validation.Issues.Where(static value => value.IsCritical).Select(static value => $"validation:{value.Code}:{value.Message}"));
        return new CampaignAutoCompletionResult(build.Id, campaign.Completed && validation.IsValid, campaign.SelectedEndingId, trace.Count, trace, errors, campaign.ComputeChecksum());
    }

    public IReadOnlyList<CampaignAutoCompletionResult> CompleteRegressionBuilds(
        GeneratedCampaignPlan plan,
        Func<SocietyWorldState> societyFactory,
        StableId playerId,
        int maximumIterations = 256) =>
        AbstractQuestSolver.CreateRegressionBuilds().Select(build => Complete(plan, societyFactory(), build, playerId, maximumIterations)).ToArray();

    private static BasicNarrativeQueryContext CreateQuery(NarrativeWorldState narrative, ulong tick, StableId playerId, AbstractQuestBuild build) => new(
        narrative,
        tick,
        actorAlive: _ => true,
        itemExists: _ => true,
        actorHasItem: (_, _) => true,
        locationReachable: _ => true,
        serviceOperating: _ => true,
        skill: (actor, skill) => actor == playerId && build.Skills.TryGetValue(skill, out int value) ? value : 50,
        behaviorTag: (_, _) => true);

    private static void SatisfyQuestConditions(NarrativeWorldState narrative, QuestInstanceState quest, GeneratedQuestPlan plan, ulong tick)
    {
        foreach (QuestObjectiveDefinition objective in plan.Kernel.Objectives.Where(static value => !value.Optional).OrderBy(static value => value.Id, StringComparer.Ordinal))
            SatisfyCondition(narrative, quest, plan, objective.CompletionCondition, tick);
    }

    private static void SatisfyCondition(NarrativeWorldState narrative, QuestInstanceState quest, GeneratedQuestPlan plan, NarrativeConditionDefinition condition, ulong tick)
    {
        StableId subject = string.IsNullOrWhiteSpace(condition.SlotId) ? plan.Id : plan.ResolveSlot(condition.SlotId);
        switch (condition.Kind)
        {
            case NarrativeConditionKind.Always:
                break;
            case NarrativeConditionKind.FactExists:
            case NarrativeConditionKind.FactEquals:
                NarrativeValue value = condition.ExpectedValue.Kind == NarrativeValueKind.None ? NarrativeValue.FromBoolean(true) : condition.ExpectedValue;
                StableId factId = StableIdFactory.CreateChild(plan.Id, "auto-agent-fact", $"{condition.Id}:{condition.PredicateId}:{subject}");
                narrative.UpsertFact(new FactRecord(factId, condition.PredicateId, subject, value, FactTruthState.True, FactScope.Quest, plan.Id, tick, null));
                break;
            case NarrativeConditionKind.And:
                foreach (NarrativeConditionDefinition child in condition.Children) SatisfyCondition(narrative, quest, plan, child, tick);
                break;
            case NarrativeConditionKind.Or:
                if (condition.Children.Count > 0) SatisfyCondition(narrative, quest, plan, condition.Children[0], tick);
                break;
            case NarrativeConditionKind.ObjectiveState:
                if (quest.Objectives.ContainsKey(condition.PredicateId)) quest.CompleteObjective(condition.PredicateId, tick);
                break;
        }
    }

    private static CampaignChoiceOption SelectChoice(IrreversibleChoiceDefinition choice, AbstractQuestBuild build)
    {
        if (build.AllowedRouteKinds.Contains(QuestRouteKind.Social) && choice.Options.Any(static value => value.Id.Contains("shared", StringComparison.Ordinal)))
            return choice.Options.First(static value => value.Id.Contains("shared", StringComparison.Ordinal));
        if (build.AllowedRouteKinds.Contains(QuestRouteKind.Medical) && choice.Options.Any(static value => value.Id.Contains("public", StringComparison.Ordinal)))
            return choice.Options.First(static value => value.Id.Contains("public", StringComparison.Ordinal));
        int index = build.Id switch
        {
            "combat" => 0,
            "stealth-security" => 1,
            "engineering-science" => 2,
            "medicine-fieldcraft" => 0,
            "influence-commerce" => 1,
            _ => 2
        };
        return choice.Options.OrderBy(static value => value.Id, StringComparer.Ordinal).ElementAt(index % choice.Options.Count);
    }

    private static ConvergenceForm SelectConvergence(ConvergenceDefinition convergence, AbstractQuestBuild build)
    {
        ConvergenceForm[] preferences = build.Id switch
        {
            "combat" => [ConvergenceForm.SiegeOrAssault, ConvergenceForm.Destruction],
            "stealth-security" => [ConvergenceForm.SystemShutdown, ConvergenceForm.Containment],
            "engineering-science" => [ConvergenceForm.SystemRepair, ConvergenceForm.TransferOfControl],
            "medicine-fieldcraft" => [ConvergenceForm.ControlledTransformation, ConvergenceForm.Evacuation],
            "influence-commerce" => [ConvergenceForm.MultiFactionNegotiation, ConvergenceForm.TransferOfControl],
            _ => [ConvergenceForm.MultiFactionNegotiation, ConvergenceForm.SystemRepair, ConvergenceForm.Containment]
        };
        return preferences.FirstOrDefault(convergence.EligibleForms.Contains, convergence.EligibleForms[0]);
    }
}
