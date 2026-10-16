using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Narrative;
using FactionReputationDimension = Roentgen.Factions.ReputationDimension;

namespace Roentgen.Campaign;

public sealed record CampaignUpdateResult(
    IReadOnlyList<StableId> EligibleMilestoneIds,
    IReadOnlyList<StableId> OfferedMilestoneIds,
    IReadOnlyList<StableId> ResolvedMilestoneIds,
    IReadOnlyList<StableId> ProvenRevelationIds,
    bool CampaignCompleted,
    IReadOnlyList<string> Errors);

public sealed record CriticalRoleRepairContext(
    Func<StableId, bool> EntityAvailable,
    Func<StableId, string, StableId?>? QualifiedFactionMemberResolver = null,
    Func<StableId, string, StableId?>? FactionOfficeResolver = null);

public sealed class CampaignDirector
{
    public CampaignUpdateResult Initialize(
        CampaignWorldState campaign,
        NarrativeWorldState narrative,
        SocietyWorldState society,
        StableId playerId,
        ulong tick,
        ulong day)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(society);
        campaign.SetTime(tick, day);
        _ = new RegionalHistoryProjector().Project(campaign.Plan.History, narrative, society.Factions.Select(static value => value.Id), tick);
        campaign.AddFlag("campaign-started");
        return Update(campaign, narrative, society, playerId, tick, day);
    }

    public CampaignUpdateResult Update(
        CampaignWorldState campaign,
        NarrativeWorldState narrative,
        SocietyWorldState society,
        StableId playerId,
        ulong tick,
        ulong day)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(society);
        var eligible = new List<StableId>();
        var offered = new List<StableId>();
        var resolved = new List<StableId>();
        var proven = new List<StableId>();
        var errors = new List<string>();
        campaign.SetTime(tick, day);

        foreach (GeneratedMilestonePlan plan in campaign.Plan.Milestones.OrderBy(static value => value.Sequence))
        {
            CampaignMilestoneRuntimeState state = campaign.GetMilestone(plan.Id);
            if (state.Status is CampaignMilestoneStatus.Resolved or CampaignMilestoneStatus.Recovered or CampaignMilestoneStatus.LockedOut or CampaignMilestoneStatus.Superseded) continue;

            if (state.Status is CampaignMilestoneStatus.Offered or CampaignMilestoneStatus.Active)
            {
                if (narrative.TryGetQuest(plan.QuestPlan.Id, out QuestInstanceState? quest) && quest is not null)
                {
                    if (quest.Status == QuestStatus.Active && state.Status == CampaignMilestoneStatus.Offered)
                    {
                        state = state with { Status = CampaignMilestoneStatus.Active };
                        campaign.SetMilestone(state);
                    }
                    if (quest.Status == QuestStatus.Completed)
                    {
                        if (plan.Kernel.RequiresDecision && state.Status != CampaignMilestoneStatus.AwaitingDecision)
                        {
                            campaign.SetMilestone(state with { Status = CampaignMilestoneStatus.AwaitingDecision });
                            continue;
                        }
                        if (plan.Kernel.RequiresConvergenceResolution && !campaign.ConvergenceResolution.HasValue)
                        {
                            _ = ProveMilestoneRevelation(campaign, plan, playerId, tick, proven);
                            continue;
                        }
                        ResolveMilestone(campaign, plan, "quest-completed", tick, resolved);
                        _ = ProveMilestoneRevelation(campaign, plan, playerId, tick, proven);
                        if (plan.Kernel.Role == CampaignMilestoneRole.Ending)
                        {
                            SelectEndingAndGenerateEpilogue(campaign, society, playerId, tick, resolved, errors);
                        }
                    }
                    else if (quest.Status == QuestStatus.Failed && day >= plan.DeadlineDay)
                    {
                        campaign.SetMilestone(state with { Status = CampaignMilestoneStatus.Failed, ResolvedTick = tick, ResolutionId = "quest-failed-after-deadline" });
                        Record(campaign, CampaignEventKind.MilestoneFailed, plan.Id, quest.Id, tick, $"Milestone '{plan.Kernel.Id}' failed after its recovery window.");
                        errors.Add($"milestone.failed:{plan.Kernel.Id}");
                    }
                }
            }
        }

        bool hasLiveMilestone = campaign.Milestones.Any(static value => value.Status is CampaignMilestoneStatus.Offered or CampaignMilestoneStatus.Active or CampaignMilestoneStatus.AwaitingDecision);
        if (!hasLiveMilestone && !campaign.Completed)
        {
            GeneratedMilestonePlan? next = campaign.Plan.Milestones
                .OrderBy(static value => value.Sequence)
                .FirstOrDefault(plan => campaign.GetMilestone(plan.Id).Status == CampaignMilestoneStatus.Planned && day >= plan.EarliestDay && plan.PredecessorIds.All(id => campaign.GetMilestone(id).Status is CampaignMilestoneStatus.Resolved or CampaignMilestoneStatus.Recovered));
            if (next is not null)
            {
                CampaignMilestoneRuntimeState state = campaign.GetMilestone(next.Id) with { Status = CampaignMilestoneStatus.Eligible, EligibleTick = tick };
                campaign.SetMilestone(state);
                eligible.Add(next.Id);
                Record(campaign, CampaignEventKind.MilestoneEligible, next.Id, StableId.Empty, tick, $"Milestone '{next.Kernel.Id}' became eligible.");
                OfferMilestone(campaign, narrative, next, tick);
                offered.Add(next.Id);
            }
        }

        return new CampaignUpdateResult(eligible, offered, resolved, proven, campaign.Completed, errors);
    }

    public CampaignCommitmentRecord CommitChoice(
        CampaignWorldState campaign,
        SocietyWorldState society,
        StableId choiceId,
        string optionId,
        StableId responsibleActorId,
        IEnumerable<StableId> knownFactIds,
        ulong tick,
        ulong day)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(society);
        IrreversibleChoiceDefinition choice = campaign.Plan.GetChoice(choiceId);
        CampaignMilestoneRuntimeState milestone = campaign.GetMilestone(choice.MilestoneId);
        if (milestone.Status != CampaignMilestoneStatus.AwaitingDecision) throw new InvalidOperationException($"Choice '{choiceId}' is not currently awaiting a decision.");
        if (campaign.FindCommitment(choiceId) is not null) throw new InvalidOperationException($"Choice '{choiceId}' is already committed.");
        CampaignChoiceOption option = choice.Options.FirstOrDefault(value => string.Equals(value.Id, optionId, StringComparison.Ordinal)) ?? throw new KeyNotFoundException($"Unknown choice option '{optionId}'.");
        StableId commitmentId = StableIdFactory.CreateChild(choiceId, "commitment", option.Id);
        var record = new CampaignCommitmentRecord(commitmentId, choiceId, option.Id, responsibleActorId, tick, day, knownFactIds.Order().Distinct().ToArray(), option.AddFlags, option.RemoveFlags);
        if (!campaign.AddCommitment(record)) throw new InvalidOperationException($"Choice '{choiceId}' could not be committed.");
        foreach (string flag in option.RemoveFlags) campaign.RemoveFlag(flag);
        foreach (string flag in option.AddFlags) campaign.AddFlag(flag);
        campaign.AddFlag(choice == campaign.Plan.Choices[0] ? "first-choice-recorded" : "second-choice-recorded");
        ApplyFactionConsequences(society, responsibleActorId, option);
        campaign.SetMilestone(milestone with { Status = CampaignMilestoneStatus.Resolved, ResolvedTick = tick, ResolutionId = $"choice:{option.Id}" });
        Record(campaign, CampaignEventKind.CommitmentRecorded, choiceId, commitmentId, tick, option.ConsequenceSummary);
        Record(campaign, CampaignEventKind.MilestoneResolved, choice.MilestoneId, commitmentId, tick, $"Milestone resolved through irreversible choice '{option.Id}'.");
        return record;
    }

    public RevelationRuntimeState CollectEvidence(CampaignWorldState campaign, StableId revelationId, StableId evidenceId, StableId actorId, ulong tick)
    {
        RevelationDefinition definition = campaign.Plan.GetRevelation(revelationId);
        if (!definition.EvidenceIds.Contains(evidenceId)) throw new InvalidOperationException($"Evidence '{evidenceId}' does not support revelation '{revelationId}'.");
        RevelationRuntimeState current = campaign.GetRevelation(revelationId);
        var evidence = new SortedSet<StableId>(current.CollectedEvidenceIds) { evidenceId };
        RevelationStatus status = evidence.Count >= definition.RequiredEvidenceCount ? RevelationStatus.Proven : RevelationStatus.Suspected;
        var updated = current with { Status = status, CollectedEvidenceIds = evidence, ChangedTick = tick, DiscoveredByActorId = actorId };
        campaign.SetRevelation(updated);
        if (status == RevelationStatus.Proven)
        {
            campaign.AddFlag($"revelation:{revelationId}:proven");
            if (revelationId == campaign.Plan.Revelations[^1].Id) campaign.AddFlag("final-revelation-proven");
        }
        Record(campaign, CampaignEventKind.RevelationChanged, revelationId, evidenceId, tick, $"Revelation '{definition.DisplayName}' is now {status}.");
        return updated;
    }

    public void ResolveConvergence(CampaignWorldState campaign, ConvergenceForm form, StableId responsibleActorId, ulong tick)
    {
        ConvergenceDefinition definition = campaign.Plan.Convergence;
        if (!definition.EligibleForms.Contains(form)) throw new InvalidOperationException($"Convergence form '{form}' is not eligible for this campaign.");
        CampaignMilestoneRuntimeState milestone = campaign.GetMilestone(definition.MilestoneId);
        if (milestone.Status is not (CampaignMilestoneStatus.Active or CampaignMilestoneStatus.Offered)) throw new InvalidOperationException("The final convergence is not active.");
        if (!definition.RequiredFlags.All(campaign.Flags.Contains)) throw new InvalidOperationException("The final convergence is missing required campaign commitments or revelations.");
        if (definition.ForbiddenFlags.Any(campaign.Flags.Contains)) throw new InvalidOperationException("The final convergence is blocked by a campaign failure flag.");
        campaign.SetConvergence(form);
        ApplyConvergenceFlags(campaign, form);
        campaign.SetMilestone(milestone with { Status = CampaignMilestoneStatus.Resolved, ResolvedTick = tick, ResolutionId = $"convergence:{form}" });
        Record(campaign, CampaignEventKind.ConvergenceResolved, definition.Id, responsibleActorId, tick, $"Final convergence resolved as {form}.");
        Record(campaign, CampaignEventKind.MilestoneResolved, definition.MilestoneId, definition.Id, tick, "Final convergence milestone resolved.");
    }

    public IReadOnlyList<CriticalRoleReplacementRecord> RepairCriticalRoles(CampaignWorldState campaign, CriticalRoleRepairContext context, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(context);
        var applied = new List<CriticalRoleReplacementRecord>();
        foreach (CriticalRoleRuntimeState runtime in campaign.CriticalRoles.OrderBy(static value => value.RoleId, StringComparer.Ordinal))
        {
            if (runtime.Available && context.EntityAvailable(runtime.CurrentEntityId)) continue;
            CriticalRoleAssignmentPlan assignment = campaign.Plan.CriticalRoleAssignments.Single(value => string.Equals(value.RoleId, runtime.RoleId, StringComparison.Ordinal));
            CriticalRoleDefinition definition = campaign.Plan.CriticalRoles.Single(value => string.Equals(value.Id, runtime.RoleId, StringComparison.Ordinal));
            (StableId replacement, int fallbackIndex, CriticalRoleFallbackKind kind) = ResolveReplacement(runtime, assignment, definition, context, campaign.Plan.Id);
            if (replacement.IsEmpty) continue;
            StableId recordId = StableIdFactory.CreateChild(assignment.Id, "critical-role-replacement", $"{runtime.CurrentEntityId}:{replacement}:{fallbackIndex}");
            if (campaign.Replacements.Any(value => value.Id == recordId)) continue;
            var record = new CriticalRoleReplacementRecord(recordId, runtime.RoleId, runtime.CurrentEntityId, replacement, kind, tick, "Required campaign role became unavailable; deterministic replacement ladder applied.");
            campaign.SetCriticalRole(runtime with { CurrentEntityId = replacement, FallbackIndex = fallbackIndex, Available = true, ChangedTick = tick });
            campaign.AddReplacement(record);
            Record(campaign, CampaignEventKind.CriticalRoleReplaced, assignment.Id, replacement, tick, $"Critical role '{runtime.RoleId}' replaced using {kind}.");
            applied.Add(record);
        }
        return applied;
    }

    private static void OfferMilestone(CampaignWorldState campaign, NarrativeWorldState narrative, GeneratedMilestonePlan plan, ulong tick)
    {
        if (!narrative.TryGetQuest(plan.QuestPlan.Id, out QuestInstanceState? quest) || quest is null)
        {
            quest = new QuestInstanceState(plan.QuestPlan);
            narrative.AddQuest(quest);
        }
        quest.Offer(tick);
        CampaignMilestoneRuntimeState state = campaign.GetMilestone(plan.Id);
        campaign.SetMilestone(state with { Status = CampaignMilestoneStatus.Offered, OfferedTick = tick });
        Record(campaign, CampaignEventKind.MilestoneOffered, plan.Id, plan.QuestPlan.Id, tick, $"Milestone '{plan.Kernel.Id}' offered quest '{plan.QuestPlan.Kernel.Id}'.");
    }

    private static void ResolveMilestone(CampaignWorldState campaign, GeneratedMilestonePlan plan, string resolutionId, ulong tick, ICollection<StableId> resolved)
    {
        CampaignMilestoneRuntimeState state = campaign.GetMilestone(plan.Id);
        if (state.Status == CampaignMilestoneStatus.Resolved) return;
        campaign.SetMilestone(state with { Status = CampaignMilestoneStatus.Resolved, ResolvedTick = tick, ResolutionId = resolutionId });
        Record(campaign, CampaignEventKind.MilestoneResolved, plan.Id, plan.QuestPlan.Id, tick, $"Milestone '{plan.Kernel.Id}' resolved.");
        resolved.Add(plan.Id);
    }

    private static bool ProveMilestoneRevelation(CampaignWorldState campaign, GeneratedMilestonePlan milestone, StableId actorId, ulong tick, ICollection<StableId> proven)
    {
        if (milestone.RevelationId.IsEmpty) return false;
        RevelationDefinition definition = campaign.Plan.GetRevelation(milestone.RevelationId);
        RevelationRuntimeState current = campaign.GetRevelation(definition.Id);
        if (current.Status == RevelationStatus.Proven) return false;
        var evidence = new SortedSet<StableId>(definition.EvidenceIds);
        campaign.SetRevelation(current with { Status = RevelationStatus.Proven, CollectedEvidenceIds = evidence, ChangedTick = tick, DiscoveredByActorId = actorId });
        campaign.AddFlag($"revelation:{definition.Id}:proven");
        if (definition.Id == campaign.Plan.Revelations[^1].Id) campaign.AddFlag("final-revelation-proven");
        Record(campaign, CampaignEventKind.RevelationChanged, definition.Id, milestone.Id, tick, $"Revelation '{definition.DisplayName}' proven by completing milestone '{milestone.Kernel.Id}'.");
        proven.Add(definition.Id);
        return true;
    }

    private static void SelectEndingAndGenerateEpilogue(CampaignWorldState campaign, SocietyWorldState society, StableId playerId, ulong tick, ICollection<StableId> resolved, ICollection<string> errors)
    {
        if (campaign.Completed) return;
        var evaluator = new CampaignRequirementEvaluator();
        EndingFamilyDefinition? ending = campaign.Plan.EndingFamilies
            .Where(value => value.Requirements.All(requirement => evaluator.Evaluate(requirement, campaign, society, playerId)))
            .OrderByDescending(static value => value.Priority)
            .ThenBy(static value => value.Id)
            .FirstOrDefault();
        if (ending is null)
        {
            errors.Add("ending.no-eligible-family");
            return;
        }
        campaign.SetEnding(ending.Id);
        foreach (string flag in ending.AddEpilogueFlags) campaign.AddFlag(flag);
        Record(campaign, CampaignEventKind.EndingSelected, ending.Id, playerId, tick, $"Ending selected: {ending.DisplayName}.");
        GenerateEpilogue(campaign, society, playerId, evaluator, tick);
        GeneratedMilestonePlan endingMilestone = campaign.Plan.Milestones.Single(static value => value.Kernel.Role == CampaignMilestoneRole.Ending);
        CampaignMilestoneRuntimeState state = campaign.GetMilestone(endingMilestone.Id);
        if (state.Status != CampaignMilestoneStatus.Resolved)
        {
            campaign.SetMilestone(state with { Status = CampaignMilestoneStatus.Resolved, ResolvedTick = tick, ResolutionId = $"ending:{ending.Id}" });
            resolved.Add(endingMilestone.Id);
        }
    }

    private static void GenerateEpilogue(CampaignWorldState campaign, SocietyWorldState society, StableId playerId, CampaignRequirementEvaluator evaluator, ulong tick)
    {
        int order = 0;
        foreach (EpilogueRule rule in campaign.Plan.EpilogueRules
                     .Where(value => value.Requirements.All(requirement => evaluator.Evaluate(requirement, campaign, society, playerId)))
                     .GroupBy(static value => (value.SubjectKind, value.SubjectId))
                     .Select(static group => group.OrderByDescending(static value => value.Priority).ThenBy(static value => value.Id).First())
                     .OrderByDescending(static value => value.Priority)
                     .ThenBy(static value => value.SubjectKind, StringComparer.Ordinal)
                     .ThenBy(static value => value.SubjectId))
        {
            StableId id = StableIdFactory.CreateChild(campaign.Plan.Id, "epilogue", $"{order}:{rule.Id}");
            string[] reasons = rule.Requirements.Select(static value => $"{value.Kind}:{value.Token}:{value.SubjectId}").ToArray();
            campaign.AddEpilogue(new CampaignEpilogueRecord(id, rule.Id, rule.SubjectKind, rule.SubjectId, rule.TextTemplate, reasons, order++));
            Record(campaign, CampaignEventKind.EpilogueGenerated, id, rule.SubjectId, tick, rule.TextTemplate);
        }
    }

    private static void ApplyFactionConsequences(SocietyWorldState society, StableId actorId, CampaignChoiceOption option)
    {
        if (!option.FavoredFactionId.IsEmpty)
        {
            FactionReputationState favored = society.GetOrCreateReputation(actorId, option.FavoredFactionId);
            favored.Adjust(FactionReputationDimension.Trust, option.TrustDelta);
            favored.Adjust(FactionReputationDimension.Fear, Math.Max(0, option.FearDelta));
            favored.Adjust(FactionReputationDimension.Respect, option.RespectDelta);
        }
        if (!option.OpposedFactionId.IsEmpty)
        {
            FactionReputationState opposed = society.GetOrCreateReputation(actorId, option.OpposedFactionId);
            opposed.Adjust(FactionReputationDimension.Trust, -Math.Abs(option.TrustDelta / 2));
            opposed.Adjust(FactionReputationDimension.Fear, Math.Max(0, option.FearDelta));
            opposed.Adjust(FactionReputationDimension.Respect, option.RespectDelta / 3);
        }
        society.Touch();
    }

    private static void ApplyConvergenceFlags(CampaignWorldState campaign, ConvergenceForm form)
    {
        campaign.AddFlag($"convergence:{form}");
        switch (form)
        {
            case ConvergenceForm.SystemRepair: campaign.AddFlag("system-repaired"); break;
            case ConvergenceForm.MultiFactionNegotiation: campaign.AddFlag("shared-control"); break;
            case ConvergenceForm.TransferOfControl: campaign.AddFlag("single-faction-control"); break;
            case ConvergenceForm.DeliberateExposure: campaign.AddFlag("truth-disclosed"); break;
            case ConvergenceForm.SystemShutdown: campaign.AddFlag("network-silent"); break;
            case ConvergenceForm.Destruction: campaign.AddFlag("network-destroyed"); break;
            case ConvergenceForm.Evacuation: campaign.AddFlag("population-relocated"); break;
            case ConvergenceForm.Containment: campaign.AddFlag("contained-network"); break;
            case ConvergenceForm.ControlledTransformation: campaign.AddFlag("transformation-accepted"); break;
            case ConvergenceForm.SiegeOrAssault: campaign.AddFlag("victory-by-force"); break;
        }
    }

    private static (StableId Replacement, int FallbackIndex, CriticalRoleFallbackKind Kind) ResolveReplacement(
        CriticalRoleRuntimeState runtime,
        CriticalRoleAssignmentPlan assignment,
        CriticalRoleDefinition definition,
        CriticalRoleRepairContext context,
        StableId campaignId)
    {
        for (int index = Math.Max(0, runtime.FallbackIndex + 1); index < definition.FallbackOrder.Count; index++)
        {
            CriticalRoleFallbackKind kind = definition.FallbackOrder[index];
            StableId replacement = kind switch
            {
                CriticalRoleFallbackKind.NamedReplacement => assignment.NamedReplacementIds.FirstOrDefault(context.EntityAvailable),
                CriticalRoleFallbackKind.QualifiedFactionMember => context.QualifiedFactionMemberResolver?.Invoke(assignment.PreferredFactionId, assignment.RoleId) ?? StableId.Empty,
                CriticalRoleFallbackKind.AlternateFactionOffice => context.FactionOfficeResolver?.Invoke(assignment.PreferredFactionId, assignment.RoleId) ?? StableId.Empty,
                CriticalRoleFallbackKind.DocumentOrRecording => assignment.EvidenceFallbackId,
                CriticalRoleFallbackKind.TerminalOrMachine => assignment.MachineFallbackId,
                CriticalRoleFallbackKind.AftermathRoute => StableIdFactory.CreateChild(campaignId, "aftermath-role", assignment.RoleId),
                _ => StableId.Empty
            };
            if (!replacement.IsEmpty && (kind is CriticalRoleFallbackKind.DocumentOrRecording or CriticalRoleFallbackKind.TerminalOrMachine or CriticalRoleFallbackKind.AftermathRoute || context.EntityAvailable(replacement)))
                return (replacement, index, kind);
        }
        return (StableId.Empty, -1, CriticalRoleFallbackKind.AftermathRoute);
    }

    private static void Record(CampaignWorldState campaign, CampaignEventKind kind, StableId subject, StableId secondary, ulong tick, string description)
    {
        StableId id = StableIdFactory.CreateChild(campaign.Plan.Id, "campaign-event", $"{kind}:{subject}:{secondary}:{tick}:{campaign.Events.Count}");
        campaign.RecordEvent(new CampaignEventRecord(id, kind, tick, subject, secondary, description));
    }
}

public sealed class CampaignRequirementEvaluator
{
    public bool Evaluate(EndingRequirement requirement, CampaignWorldState campaign, SocietyWorldState society, StableId playerId)
    {
        return requirement.Kind switch
        {
            EndingRequirementKind.CampaignFlag => campaign.Flags.Contains(requirement.Token),
            EndingRequirementKind.CommitmentOption => campaign.FindCommitment(requirement.SubjectId) is CampaignCommitmentRecord commitment && (requirement.Token == "*" || string.Equals(commitment.OptionId, requirement.Token, StringComparison.Ordinal)),
            EndingRequirementKind.RevelationProven => campaign.GetRevelation(requirement.SubjectId).Status == RevelationStatus.Proven,
            EndingRequirementKind.FactionExists => society.TryGetFaction(requirement.SubjectId, out FactionState? faction) && faction is not null,
            EndingRequirementKind.FactionTrustAtLeast => society.GetOrCreateReputation(playerId, requirement.SubjectId)[FactionReputationDimension.Trust] >= requirement.Minimum,
            EndingRequirementKind.FactionFearAtLeast => society.GetOrCreateReputation(playerId, requirement.SubjectId)[FactionReputationDimension.Fear] >= requirement.Minimum,
            EndingRequirementKind.ConvergenceForm => campaign.ConvergenceResolution.HasValue && string.Equals(campaign.ConvergenceResolution.Value.ToString(), requirement.Token, StringComparison.Ordinal),
            EndingRequirementKind.MilestoneResolved => campaign.GetMilestone(requirement.SubjectId).Status is CampaignMilestoneStatus.Resolved or CampaignMilestoneStatus.Recovered,
            _ => false
        };
    }
}
