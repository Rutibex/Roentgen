using Roentgen.Contracts;
using Roentgen.Factions;
using Roentgen.Narrative;
using Roentgen.World;

namespace Roentgen.Campaign;

public sealed record CampaignValidationIssue(string Code, string Message, bool IsCritical, StableId SubjectId);
public sealed record CampaignValidationReport(bool IsValid, IReadOnlyList<CampaignValidationIssue> Issues);

public sealed class CampaignPlanValidator
{
    public CampaignValidationReport Validate(GeneratedCampaignPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var issues = new List<CampaignValidationIssue>();
        if (plan.Id.IsEmpty) Add("campaign.id.empty", "Campaign ID cannot be empty.", true, plan.Id);
        if (plan.Fingerprint.IsZero) Add("campaign.fingerprint.empty", "Campaign fingerprint cannot be zero.", true, plan.Id);
        if (plan.History.Regions.Select(static value => value.Region).Distinct().Count() != Enum.GetValues<RegionFamily>().Length)
            Add("history.region-coverage", "Regional history does not cover all six region families.", true, plan.History.Id);
        foreach (RegionalHistoryRecord region in plan.History.Regions)
        {
            if (region.EventIds.Count < 4) Add("history.region.too-shallow", $"Region '{region.Region}' has fewer than four causal events.", true, region.Id);
            if (region.RepresentativeLocationId.IsEmpty) Add("history.region.location", $"Region '{region.Region}' lacks a representative location.", true, region.Id);
        }
        foreach (HistoricalEventRecord historicalEvent in plan.History.Events)
        {
            foreach (StableId cause in historicalEvent.CauseEventIds)
                if (!plan.History.Events.Any(value => value.Id == cause)) Add("history.cause.missing", $"Event '{historicalEvent.Id}' references missing cause '{cause}'.", true, historicalEvent.Id);
            if (historicalEvent.DerivedFacts.Count == 0) Add("history.fact.empty", $"Event '{historicalEvent.Id}' projects no facts.", false, historicalEvent.Id);
        }

        if (plan.Milestones.Count != 12) Add("campaign.milestone.count", $"Expected 12 major milestones, found {plan.Milestones.Count}.", true, plan.Id);
        int[] sequences = plan.Milestones.Select(static value => value.Sequence).Order().ToArray();
        if (!sequences.SequenceEqual(Enumerable.Range(0, plan.Milestones.Count))) Add("campaign.milestone.sequence", "Milestone sequence is not contiguous from zero.", true, plan.Id);
        foreach (GeneratedMilestonePlan milestone in plan.Milestones)
        {
            if (milestone.QuestPlan.Id.IsEmpty) Add("campaign.milestone.quest", $"Milestone '{milestone.Kernel.Id}' has no quest plan.", true, milestone.Id);
            foreach (StableId predecessor in milestone.PredecessorIds)
                if (!plan.Milestones.Any(value => value.Id == predecessor)) Add("campaign.predecessor.missing", $"Milestone '{milestone.Id}' references missing predecessor '{predecessor}'.", true, milestone.Id);
            foreach (StableId next in milestone.NextIds)
                if (!plan.Milestones.Any(value => value.Id == next)) Add("campaign.next.missing", $"Milestone '{milestone.Id}' references missing successor '{next}'.", true, milestone.Id);
            if (milestone.DeadlineDay <= milestone.EarliestDay) Add("campaign.deadline.invalid", $"Milestone '{milestone.Id}' deadline does not follow its earliest day.", true, milestone.Id);
        }
        if (HasCycle(plan.Milestones)) Add("campaign.graph.cycle", "Campaign milestone graph contains a cycle.", true, plan.Id);
        if (plan.Milestones.Select(static value => value.QuestPlan.Id).Distinct().Count() != plan.Milestones.Count)
            Add("campaign.quest-id.duplicate", "Campaign milestones do not have unique generated quest IDs.", true, plan.Id);

        if (plan.Revelations.Count != 3) Add("campaign.revelation.count", $"Expected three major revelations, found {plan.Revelations.Count}.", true, plan.Id);
        foreach (RevelationDefinition revelation in plan.Revelations)
        {
            if (revelation.HiddenFactIds.Count == 0) Add("campaign.revelation.fact", $"Revelation '{revelation.Id}' has no hidden facts.", true, revelation.Id);
            if (revelation.RequiredEvidenceCount < 1 || revelation.RequiredEvidenceCount > revelation.EvidenceIds.Count) Add("campaign.revelation.threshold", $"Revelation '{revelation.Id}' has an invalid evidence threshold.", true, revelation.Id);
        }
        if (plan.Choices.Count != 2) Add("campaign.choice.count", $"Expected two irreversible choices, found {plan.Choices.Count}.", true, plan.Id);
        foreach (IrreversibleChoiceDefinition choice in plan.Choices)
        {
            if (choice.Options.Count < 2) Add("campaign.choice.options", $"Choice '{choice.Id}' has fewer than two options.", true, choice.Id);
            if (!plan.Milestones.Any(value => value.Id == choice.MilestoneId && value.Kernel.RequiresDecision)) Add("campaign.choice.milestone", $"Choice '{choice.Id}' is not bound to a decision milestone.", true, choice.Id);
        }
        if (plan.EndingFamilies.Count < 4) Add("campaign.ending.count", $"Expected at least four ending families, found {plan.EndingFamilies.Count}.", true, plan.Id);
        if (plan.Convergence.EligibleForms.Count < 3) Add("campaign.convergence.forms", "Final convergence exposes fewer than three resolution forms.", true, plan.Convergence.Id);
        if (!plan.EpilogueRules.Any(static value => string.Equals(value.SubjectKind, "Player", StringComparison.Ordinal)))
            Add("campaign.epilogue.player", "Campaign has no player epilogue rule.", true, plan.Id);
        foreach (RegionalHistoryRecord region in plan.History.Regions)
            if (!plan.EpilogueRules.Any(value => string.Equals(value.SubjectKind, "Region", StringComparison.Ordinal) && value.SubjectId == region.Id))
                Add("campaign.epilogue.region", $"Region '{region.Region}' has no epilogue rule.", true, region.Id);
        if (plan.EpilogueRules.Select(static value => value.Id).Distinct().Count() != plan.EpilogueRules.Count)
            Add("campaign.epilogue.duplicate-id", "Campaign epilogue rules contain duplicate IDs.", true, plan.Id);
        foreach (EpilogueRule rule in plan.EpilogueRules)
            if (rule.SubjectId.IsEmpty) Add("campaign.epilogue.empty-subject", $"Epilogue rule '{rule.Id}' has an empty subject.", true, rule.Id);
        if (plan.CriticalRoles.Count != plan.CriticalRoleAssignments.Count) Add("campaign.critical-role.assignment-count", "Every critical role must have one initial assignment.", true, plan.Id);
        foreach (CriticalRoleDefinition role in plan.CriticalRoles.Where(static value => value.RequiredForMainCampaign))
        {
            CriticalRoleAssignmentPlan? assignment = plan.CriticalRoleAssignments.FirstOrDefault(value => string.Equals(value.RoleId, role.Id, StringComparison.Ordinal));
            if (assignment is null || assignment.AssignedEntityId.IsEmpty) Add("campaign.critical-role.unassigned", $"Required role '{role.Id}' is unassigned.", true, plan.Id);
            if (role.FallbackOrder.Count == 0) Add("campaign.critical-role.no-fallback", $"Required role '{role.Id}' has no fallback ladder.", true, plan.Id);
        }
        return new CampaignValidationReport(issues.All(static value => !value.IsCritical), issues);

        void Add(string code, string message, bool critical, StableId subject) => issues.Add(new CampaignValidationIssue(code, message, critical, subject));
    }

    private static bool HasCycle(IReadOnlyList<GeneratedMilestonePlan> milestones)
    {
        var graph = milestones.ToDictionary(static value => value.Id, static value => value.NextIds);
        var visiting = new HashSet<StableId>();
        var visited = new HashSet<StableId>();
        return graph.Keys.Any(Visit);

        bool Visit(StableId id)
        {
            if (visited.Contains(id)) return false;
            if (!visiting.Add(id)) return true;
            if (graph.TryGetValue(id, out IReadOnlyList<StableId>? next))
                foreach (StableId child in next)
                    if (Visit(child)) return true;
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
    }
}

public sealed class CampaignStateValidator
{
    public CampaignValidationReport Validate(CampaignWorldState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var issues = new List<CampaignValidationIssue>(new CampaignPlanValidator().Validate(state.Plan).Issues);
        foreach (CampaignMilestoneRuntimeState milestone in state.Milestones)
            if (!state.Plan.Milestones.Any(value => value.Id == milestone.MilestoneId)) issues.Add(new CampaignValidationIssue("campaign.state.milestone-missing", $"Runtime milestone '{milestone.MilestoneId}' is absent from the plan.", true, milestone.MilestoneId));
        foreach (CampaignCommitmentRecord commitment in state.Commitments)
        {
            IrreversibleChoiceDefinition? choice = state.Plan.Choices.FirstOrDefault(value => value.Id == commitment.ChoiceId);
            if (choice is null || !choice.Options.Any(value => string.Equals(value.Id, commitment.OptionId, StringComparison.Ordinal)))
                issues.Add(new CampaignValidationIssue("campaign.state.commitment-invalid", $"Commitment '{commitment.Id}' references an invalid choice option.", true, commitment.Id));
        }
        if (state.Commitments.GroupBy(static value => value.ChoiceId).Any(static group => group.Count() > 1))
            issues.Add(new CampaignValidationIssue("campaign.state.commitment-duplicate", "An irreversible choice has multiple commitments.", true, state.Plan.Id));
        if (state.Completed && state.SelectedEndingId.IsEmpty)
            issues.Add(new CampaignValidationIssue("campaign.state.ending-missing", "Completed campaign has no selected ending.", true, state.Plan.Id));
        if (state.Completed && state.Epilogues.Count == 0)
            issues.Add(new CampaignValidationIssue("campaign.state.epilogue-missing", "Completed campaign has no epilogue records.", true, state.Plan.Id));
        return new CampaignValidationReport(issues.All(static value => !value.IsCritical), issues);
    }
}

public sealed class CampaignRepairEngine
{
    public IReadOnlyList<StableId> RepairFailedMilestones(CampaignWorldState campaign, ulong tick)
    {
        var repaired = new List<StableId>();
        foreach (GeneratedMilestonePlan plan in campaign.Plan.Milestones.OrderBy(static value => value.Sequence))
        {
            CampaignMilestoneRuntimeState state = campaign.GetMilestone(plan.Id);
            if (state.Status != CampaignMilestoneStatus.Failed || !string.Equals(plan.FailurePolicy, "recovery-or-aftermath", StringComparison.Ordinal)) continue;
            campaign.SetMilestone(state with { Status = CampaignMilestoneStatus.Recovered, ResolvedTick = tick, ResolutionId = "aftermath-recovery", RecoveryCount = state.RecoveryCount + 1 });
            campaign.AddFlag($"milestone:{plan.Id}:recovered");
            repaired.Add(plan.Id);
        }
        return repaired;
    }
}
