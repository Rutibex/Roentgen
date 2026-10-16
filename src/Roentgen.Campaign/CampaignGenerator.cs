using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Generation;
using Roentgen.Narrative;
using Roentgen.World;

namespace Roentgen.Campaign;

public sealed class CampaignGenerator : IVersionedGenerator<CampaignGenerationRequest, GeneratedCampaignPlan>
{
    public const int CurrentGeneratorVersion = 17;
    public string GeneratorId => "roentgen.campaign-director";
    public int SchemaVersion => CurrentGeneratorVersion;

    public GeneratedCampaignPlan Generate(CampaignGenerationRequest request, DomainSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Society);
        ArgumentNullException.ThrowIfNull(request.QuestBindings);
        if (request.PlayerId.IsEmpty) throw new ArgumentException("Campaign generation requires a player ID.", nameof(request));
        if (request.QuestKernels.Count < 12) throw new ArgumentException("Campaign generation requires at least twelve quest kernels.", nameof(request));

        StableId campaignRoot = StableIdFactory.CreateCampaignRoot(request.CampaignSeed);
        StableId campaignId = StableIdFactory.CreateChild(campaignRoot, "generated-campaign", CurrentGeneratorVersion);
        RegionalHistoryPlan history = new RegionalHistoryCompiler().Compile(request.CampaignSeed, request.Society, request.Topology, GenerationDomain.Derive(seed, "regional-history"));
        CampaignPremiseSelection premise = SelectPremise(campaignId, history, request.Society, GenerationDomain.Derive(seed, "premise"));
        IReadOnlyList<MilestoneKernel> milestoneKernels = Phase17Content.CreateMilestoneKernels();
        StableId[] milestoneIds = milestoneKernels.Select((kernel, index) => StableIdFactory.CreateChild(campaignId, "milestone", $"{index:D2}:{kernel.Id}")).ToArray();
        StableId[] revelationIds =
        [
            StableIdFactory.CreateChild(campaignId, "revelation", "first"),
            StableIdFactory.CreateChild(campaignId, "revelation", "second"),
            StableIdFactory.CreateChild(campaignId, "revelation", "final")
        ];
        StableId[] choiceIds =
        [
            StableIdFactory.CreateChild(campaignId, "irreversible-choice", "first-alignment"),
            StableIdFactory.CreateChild(campaignId, "irreversible-choice", "second-alignment")
        ];

        IReadOnlyList<RevelationDefinition> revelations = BuildRevelations(history, revelationIds, milestoneIds);
        IReadOnlyList<IrreversibleChoiceDefinition> choices = BuildChoices(choiceIds, milestoneIds, premise, request.Society);
        IReadOnlyList<GeneratedMilestonePlan> milestones = BuildMilestones(campaignId, request, history, premise, milestoneKernels, milestoneIds, revelationIds, choiceIds, seed);
        IReadOnlyList<CriticalRoleDefinition> criticalRoles = Phase17Content.CreateCriticalRoles();
        IReadOnlyList<CriticalRoleAssignmentPlan> assignments = BuildCriticalRoleAssignments(campaignId, criticalRoles, premise, request.Society, history);
        GeneratedMilestonePlan convergenceMilestone = milestones.Single(static value => value.Kernel.Role == CampaignMilestoneRole.FinalConvergence);
        ConvergenceDefinition convergence = BuildConvergence(campaignId, convergenceMilestone, premise, request.Society);
        IReadOnlyList<StableId> factionIds = request.Society.Factions.OrderBy(static value => value.Id).Select(static value => value.Id).ToArray();
        IReadOnlyList<EndingFamilyDefinition> endings = Phase17Content.CreateEndingFamilies(campaignId, factionIds, choiceIds[0], choiceIds[1], revelationIds[2], convergenceMilestone.Id);
        IReadOnlyList<EpilogueRule> epilogueRules = Phase17Content.CreateEpilogueRules(
            campaignId,
            request.PlayerId,
            request.Society,
            history,
            request.CompanionIds ?? [],
            endings);
        var validationHistory = new List<string>
        {
            "campaign.history.generated",
            $"campaign.premise:{premise.Kernel.Id}",
            $"campaign.milestones:{milestones.Count}",
            $"campaign.revelations:{revelations.Count}",
            $"campaign.choices:{choices.Count}",
            $"campaign.endings:{endings.Count}",
            $"campaign.critical-roles:{assignments.Count}"
        };

        var provisional = new GeneratedCampaignPlan(
            campaignId,
            request.CampaignSeed,
            history,
            premise,
            milestones,
            revelations,
            choices,
            convergence,
            endings,
            epilogueRules,
            criticalRoles,
            assignments,
            validationHistory,
            [],
            CurrentGeneratorVersion,
            StateChecksum.Zero);

        StateChecksum fingerprint = ComputePlanFingerprint(provisional);
        var completed = provisional with { Fingerprint = fingerprint };
        CampaignValidationReport report = new CampaignPlanValidator().Validate(completed);
        if (!report.IsValid)
        {
            throw new InvalidOperationException("Generated campaign failed validation: " + string.Join("; ", report.Issues.Where(static value => value.IsCritical).Select(static value => value.Message)));
        }
        return completed with { ValidationHistory = validationHistory.Concat(report.Issues.Where(static value => !value.IsCritical).Select(static value => $"warning:{value.Code}")).ToArray() };
    }

    public GeneratedCampaignPlan Generate(CampaignGenerationRequest request)
    {
        DomainSeed seed = GenerationDomain.Derive(request.CampaignSeed, "campaign.director");
        return Generate(request, seed);
    }

    private static CampaignPremiseSelection SelectPremise(StableId campaignId, RegionalHistoryPlan history, SocietyWorldState society, DomainSeed seed)
    {
        IReadOnlyList<CampaignPremiseKernel> kernels = Phase17Content.CreatePremiseKernels();
        int factionCount = society.Factions.Count;
        int regionCount = history.Regions.Count;
        var scored = new List<(CampaignPremiseKernel Kernel, int Score, string[] Reasons)>();
        foreach (CampaignPremiseKernel kernel in kernels)
        {
            bool requiredKinds = kernel.RequiredEventKinds.All(kind => history.Events.Any(value => value.Kind == kind));
            if (!requiredKinds || factionCount < kernel.MinimumFactionCount || regionCount < kernel.MinimumRegionCount) continue;
            int kindMatches = history.Events.Count(value => kernel.RequiredEventKinds.Contains(value.Kind));
            int tagMatches = history.Events.Sum(value => value.Tags.Count(kernel.PreferredTags.Contains));
            int hiddenMatches = history.Events.Count(value => value.PublicVisibilityPermille < 500 && value.Tags.Any(kernel.PreferredTags.Contains));
            int score = kernel.BasePriority + kindMatches * 35 + tagMatches * 20 + hiddenMatches * 45;
            scored.Add((kernel, score, [$"required-event-matches:{kindMatches}", $"preferred-tag-matches:{tagMatches}", $"hidden-cause-matches:{hiddenMatches}"]));
        }
        if (scored.Count == 0) throw new InvalidOperationException("No campaign premise kernel can bind to the generated history.");

        int bestScore = scored.Max(static value => value.Score);
        (CampaignPremiseKernel Kernel, int Score, string[] Reasons)[] best = scored.Where(value => value.Score == bestScore).OrderBy(static value => value.Kernel.Id, StringComparer.Ordinal).ToArray();
        Pcg32Random random = RandomStreamFactory.Create(seed, "premise-tie-break");
        (CampaignPremiseKernel kernel, int score, string[] reasons) selected = best[random.NextInt32(best.Length)];
        HistoricalEventRecord hiddenCause = history.Events
            .Where(value => value.Tags.Any(selected.kernel.PreferredTags.Contains))
            .OrderBy(static value => value.PublicVisibilityPermille)
            .ThenByDescending(static value => value.Day)
            .ThenBy(static value => value.Id)
            .FirstOrDefault() ?? history.Events.OrderBy(static value => value.PublicVisibilityPermille).ThenBy(static value => value.Id).First();
        (StableId primaryFaction, StableId secondaryFaction) = SelectFactionPair(society, hiddenCause.ParticipantIds);
        StableId id = StableIdFactory.CreateChild(campaignId, "campaign-premise", selected.kernel.Id);
        return new CampaignPremiseSelection(id, selected.kernel, hiddenCause.Region, primaryFaction, secondaryFaction, hiddenCause.LocationId, hiddenCause.Id, selected.score, selected.reasons);
    }

    private static (StableId Primary, StableId Secondary) SelectFactionPair(SocietyWorldState society, IReadOnlyList<StableId> historicalParticipants)
    {
        StableId[] factions = society.Factions.OrderBy(static value => value.Id).Select(static value => value.Id).ToArray();
        if (factions.Length == 0)
        {
            return (historicalParticipants.FirstOrDefault(), historicalParticipants.Skip(1).FirstOrDefault());
        }
        FactionRelationshipState? tense = society.Relationships
            .OrderByDescending(static value => value.HistoricalGrievancePermille + value.BorderTensionPermille + value.FearPermille - value.TrustPermille)
            .ThenBy(static value => value.SourceFactionId)
            .ThenBy(static value => value.TargetFactionId)
            .FirstOrDefault();
        if (tense is not null) return (tense.SourceFactionId, tense.TargetFactionId);
        return (factions[0], factions.Length > 1 ? factions[1] : factions[0]);
    }

    private static IReadOnlyList<RevelationDefinition> BuildRevelations(RegionalHistoryPlan history, IReadOnlyList<StableId> revelationIds, IReadOnlyList<StableId> milestoneIds)
    {
        HistoricalEventRecord[] hidden = history.Events
            .Where(static value => value.PublicVisibilityPermille < 500)
            .OrderBy(static value => value.PublicVisibilityPermille)
            .ThenByDescending(static value => value.Day)
            .ThenBy(static value => value.Id)
            .Take(3)
            .ToArray();
        while (hidden.Length < 3) hidden = hidden.Concat(history.Events.OrderBy(static value => value.Id).Take(3 - hidden.Length)).ToArray();
        string[] names = ["The Local Diagnosis Was Deliberate", "The Regional Crises Share a Command Line", "The Network Is Still Selecting Outcomes"];
        int[] unlockSequence = [4, 9, 11];
        var result = new RevelationDefinition[3];
        for (int index = 0; index < result.Length; index++)
        {
            HistoricalEventRecord source = hidden[index];
            StableId[] factIds = source.DerivedFacts.Select(definition => StableIdFactory.CreateChild(source.Id, "fact", $"{definition.PredicateId}:{definition.SubjectId}")).ToArray();
            result[index] = new RevelationDefinition(
                revelationIds[index],
                names[index],
                factIds,
                source.EvidenceIds,
                Math.Max(1, Math.Min(source.EvidenceIds.Count, index + 1)),
                [milestoneIds[unlockSequence[index]]],
                new SortedSet<string>(source.Tags.Concat(["campaign-revelation", $"tier-{index + 1}"]), StringComparer.Ordinal));
        }
        return result;
    }

    private static IReadOnlyList<IrreversibleChoiceDefinition> BuildChoices(IReadOnlyList<StableId> choiceIds, IReadOnlyList<StableId> milestoneIds, CampaignPremiseSelection premise, SocietyWorldState society)
    {
        StableId primary = premise.PrimaryFactionId;
        StableId secondary = premise.SecondaryFactionId;
        StableId neutral = society.Factions.Select(static value => value.Id).Order().FirstOrDefault(value => value != primary && value != secondary);
        if (neutral.IsEmpty) neutral = primary;
        return
        [
            new IrreversibleChoiceDefinition(
                choiceIds[0],
                "Who receives the first enforceable claim over the exposed system?",
                milestoneIds[6],
                [
                    Option("support-primary", "Recognize the primary faction's custody claim.", primary, secondary, ["first-choice-primary", "single-faction-precedent"], [], 140, 40, 80, "The primary faction gains legal and material precedence."),
                    Option("support-secondary", "Transfer custody to the opposing faction.", secondary, primary, ["first-choice-secondary", "custody-reversed"], [], 130, 60, 90, "The opposing faction gains the system and inherits its obligations."),
                    Option("shared-custody", "Create a supervised shared-custody compact.", neutral, StableId.Empty, ["first-choice-shared", "shared-control"], ["single-faction-precedent"], 80, -20, 60, "No faction receives uncontested authority, creating a fragile cooperative structure.")
                ],
                true,
                new SortedSet<string>(["alignment", "custody", "irreversible"], StringComparer.Ordinal)),

            new IrreversibleChoiceDefinition(
                choiceIds[1],
                "What truth and risk will the settlements accept before convergence?",
                milestoneIds[9],
                [
                    Option("public-disclosure", "Release the complete diagnostic record.", neutral, StableId.Empty, ["truth-disclosed", "public-record"], ["truth-suppressed"], 70, 20, 120, "Every faction gains access to the truth, including evidence against itself."),
                    Option("restricted-disclosure", "Release only the operational facts required for survival.", primary, secondary, ["truth-restricted", "managed-disclosure"], ["public-record"], 100, 30, 40, "The immediate crisis can be managed, but the historical record remains controlled."),
                    Option("suppress-record", "Destroy or seal the record to prevent renewed conflict.", primary, neutral, ["truth-suppressed", "single-faction-control"], ["truth-disclosed", "public-record"], 60, 140, -50, "The evidence disappears from public reach and becomes a tool of whoever retains the key.")
                ],
                true,
                new SortedSet<string>(["alignment", "truth", "irreversible"], StringComparer.Ordinal))
        ];
    }

    private static CampaignChoiceOption Option(string id, string name, StableId favored, StableId opposed, string[] add, string[] remove, int trust, int fear, int respect, string summary) =>
        new(id, name, favored, opposed, new SortedSet<string>(add, StringComparer.Ordinal), new SortedSet<string>(remove, StringComparer.Ordinal), trust, fear, respect, summary);

    private static IReadOnlyList<GeneratedMilestonePlan> BuildMilestones(
        StableId campaignId,
        CampaignGenerationRequest request,
        RegionalHistoryPlan history,
        CampaignPremiseSelection premise,
        IReadOnlyList<MilestoneKernel> kernels,
        IReadOnlyList<StableId> ids,
        IReadOnlyList<StableId> revelationIds,
        IReadOnlyList<StableId> choiceIds,
        DomainSeed seed)
    {
        var questCatalog = request.QuestKernels.ToDictionary(static value => value.Id, StringComparer.Ordinal);
        RegionFamily[] regions = Enum.GetValues<RegionFamily>();
        StableId[] factions = request.Society.Factions.OrderBy(static value => value.Id).Select(static value => value.Id).ToArray();
        if (factions.Length == 0) factions = [premise.PrimaryFactionId, premise.SecondaryFactionId];
        var plans = new List<GeneratedMilestonePlan>(kernels.Count);
        for (int index = 0; index < kernels.Count; index++)
        {
            MilestoneKernel kernel = kernels[index];
            QuestKernelDefinition questKernel = kernel.PreferredQuestKernelIds.Select(id => questCatalog.TryGetValue(id, out QuestKernelDefinition? value) ? value : null).FirstOrDefault(static value => value is not null)
                ?? request.QuestKernels.OrderBy(static value => value.Id, StringComparer.Ordinal).ElementAt(index % request.QuestKernels.Count);
            DomainSeed milestoneSeed = GenerationDomain.Derive(seed, $"milestone.{index}.{kernel.Id}");
            ulong bindingSeed = RandomStreamFactory.Create(milestoneSeed, "quest-binding").NextUInt64();
            QuestBindingResult binding = new QuestBinder().Bind(questKernel, request.QuestBindings, campaignId, bindingSeed);
            if (!binding.Success || binding.Plan is null) throw new InvalidOperationException($"Milestone '{kernel.Id}' could not bind quest '{questKernel.Id}': {string.Join("; ", binding.Errors)}");

            RegionFamily region = index == 0 ? premise.PrimaryRegion : regions[((int)premise.PrimaryRegion + index) % regions.Length];
            RegionalHistoryRecord regionHistory = history.Regions.Single(value => value.Region == region);
            StableId primaryFaction = index % 2 == 0 ? premise.PrimaryFactionId : premise.SecondaryFactionId;
            StableId secondaryFaction = factions[(index + 1) % factions.Length];
            StableId revelationId = index switch { 3 => revelationIds[0], 8 => revelationIds[1], 10 => revelationIds[2], _ => StableId.Empty };
            StableId choiceId = index switch { 6 => choiceIds[0], 9 => choiceIds[1], _ => StableId.Empty };
            string[] roles = kernel.Role switch
            {
                CampaignMilestoneRole.Opening => ["regional-medical-authority", "diagnostic-record-carrier"],
                CampaignMilestoneRole.FactionIntroduction => ["faction-negotiator", "settlement-representative"],
                CampaignMilestoneRole.Revelation => ["collapse-witness", "diagnostic-record-carrier", "technical-specialist"],
                CampaignMilestoneRole.IrreversibleChoice => ["faction-negotiator", "settlement-representative"],
                CampaignMilestoneRole.FinalConvergence => ["opposition-commander", "final-system-custodian", "technical-specialist"],
                _ => ["settlement-representative", "technical-specialist"]
            };
            plans.Add(new GeneratedMilestonePlan(
                ids[index],
                kernel,
                index,
                region,
                primaryFaction,
                secondaryFaction,
                regionHistory.RepresentativeLocationId,
                binding.Plan,
                index == 0 ? [] : [ids[index - 1]],
                index == kernels.Count - 1 ? [] : [ids[index + 1]],
                revelationId,
                choiceId,
                roles,
                (ulong)(index * 3),
                (ulong)(index * 3 + 18),
                kernel.Role is CampaignMilestoneRole.Ending ? "ending-fallback" : "recovery-or-aftermath"));
        }
        return plans;
    }

    private static IReadOnlyList<CriticalRoleAssignmentPlan> BuildCriticalRoleAssignments(
        StableId campaignId,
        IReadOnlyList<CriticalRoleDefinition> roles,
        CampaignPremiseSelection premise,
        SocietyWorldState society,
        RegionalHistoryPlan history)
    {
        StableId[] factionMembers = society.Factions.OrderBy(static value => value.Id).SelectMany(static value => value.MemberActorIds).Order().Distinct().ToArray();
        StableId[] factions = society.Factions.OrderBy(static value => value.Id).Select(static value => value.Id).ToArray();
        HistoricalEventRecord[] hiddenEvents = history.Events.OrderBy(static value => value.PublicVisibilityPermille).ThenBy(static value => value.Id).ToArray();
        var assignments = new List<CriticalRoleAssignmentPlan>();
        for (int index = 0; index < roles.Count; index++)
        {
            CriticalRoleDefinition role = roles[index];
            StableId assigned = factionMembers.Length > 0 ? factionMembers[index % factionMembers.Length] : StableIdFactory.CreateChild(campaignId, "critical-actor", role.Id);
            StableId preferredFaction = index % 2 == 0 ? premise.PrimaryFactionId : premise.SecondaryFactionId;
            if (preferredFaction.IsEmpty && factions.Length > 0) preferredFaction = factions[index % factions.Length];
            StableId namedReplacement = StableIdFactory.CreateChild(campaignId, "critical-actor-replacement", role.Id);
            HistoricalEventRecord source = hiddenEvents[index % hiddenEvents.Length];
            StableId evidence = source.EvidenceIds.FirstOrDefault();
            StableId machine = StableIdFactory.CreateChild(history.Regions[index % history.Regions.Count].RepresentativeLocationId, "machine", role.Id);
            assignments.Add(new CriticalRoleAssignmentPlan(
                StableIdFactory.CreateChild(campaignId, "critical-role-assignment", role.Id),
                role.Id,
                assigned,
                preferredFaction,
                [namedReplacement],
                evidence,
                machine));
        }
        return assignments;
    }

    private static ConvergenceDefinition BuildConvergence(StableId campaignId, GeneratedMilestonePlan milestone, CampaignPremiseSelection premise, SocietyWorldState society)
    {
        ConvergenceForm[] forms = premise.Kernel.Kind switch
        {
            CampaignPremiseKind.ResourceConflict => [ConvergenceForm.MultiFactionNegotiation, ConvergenceForm.TransferOfControl, ConvergenceForm.Evacuation, ConvergenceForm.SiegeOrAssault],
            CampaignPremiseKind.DiagnosticFailure => [ConvergenceForm.SystemRepair, ConvergenceForm.SystemShutdown, ConvergenceForm.Containment, ConvergenceForm.DeliberateExposure],
            CampaignPremiseKind.BuriedSystem => [ConvergenceForm.SystemRepair, ConvergenceForm.Destruction, ConvergenceForm.Containment, ConvergenceForm.ControlledTransformation],
            CampaignPremiseKind.FoundingMyth => [ConvergenceForm.MultiFactionNegotiation, ConvergenceForm.TransferOfControl, ConvergenceForm.DeliberateExposure, ConvergenceForm.SiegeOrAssault],
            CampaignPremiseKind.TransportDirective => [ConvergenceForm.SystemRepair, ConvergenceForm.SystemShutdown, ConvergenceForm.TransferOfControl, ConvergenceForm.Evacuation],
            CampaignPremiseKind.MedicalTransformation => [ConvergenceForm.ControlledTransformation, ConvergenceForm.Containment, ConvergenceForm.SystemShutdown, ConvergenceForm.DeliberateExposure],
            _ => [ConvergenceForm.MultiFactionNegotiation, ConvergenceForm.SystemRepair, ConvergenceForm.SystemShutdown, ConvergenceForm.ControlledTransformation, ConvergenceForm.Destruction]
        };
        StableId[] factions = society.Factions.OrderBy(static value => value.Id).Select(static value => value.Id).ToArray();
        return new ConvergenceDefinition(
            StableIdFactory.CreateChild(campaignId, "convergence", premise.Kernel.Id),
            milestone.Id,
            forms,
            factions,
            milestone.LocationId,
            new SortedSet<string>(["second-choice-recorded", "final-revelation-proven"], StringComparer.Ordinal),
            new SortedSet<string>(["main-campaign-impossible"], StringComparer.Ordinal));
    }

    private static StateChecksum ComputePlanFingerprint(GeneratedCampaignPlan plan)
    {
        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.generated-campaign.fingerprint");
        writer.WriteInt32(CurrentGeneratorVersion);
        writer.WriteStableId(plan.Id);
        writer.WriteUInt64(plan.CampaignSeed.Value);
        plan.History.WriteCanonical(writer);
        plan.Premise.WriteCanonical(writer);
        writer.WriteCount(plan.Milestones.Count);
        foreach (GeneratedMilestonePlan milestone in plan.Milestones.OrderBy(static value => value.Sequence)) milestone.WriteCanonical(writer);
        writer.WriteCount(plan.Revelations.Count);
        foreach (RevelationDefinition revelation in plan.Revelations.OrderBy(static value => value.Id)) revelation.WriteCanonical(writer);
        writer.WriteCount(plan.Choices.Count);
        foreach (IrreversibleChoiceDefinition choice in plan.Choices.OrderBy(static value => value.Id)) choice.WriteCanonical(writer);
        plan.Convergence.WriteCanonical(writer);
        writer.WriteCount(plan.EndingFamilies.Count);
        foreach (EndingFamilyDefinition ending in plan.EndingFamilies.OrderBy(static value => value.Id)) ending.WriteCanonical(writer);
        writer.WriteCount(plan.EpilogueRules.Count);
        foreach (EpilogueRule rule in plan.EpilogueRules.OrderBy(static value => value.Id)) rule.WriteCanonical(writer);
        writer.WriteCount(plan.CriticalRoles.Count);
        foreach (CriticalRoleDefinition role in plan.CriticalRoles.OrderBy(static value => value.Id, StringComparer.Ordinal)) role.WriteCanonical(writer);
        writer.WriteCount(plan.CriticalRoleAssignments.Count);
        foreach (CriticalRoleAssignmentPlan assignment in plan.CriticalRoleAssignments.OrderBy(static value => value.RoleId, StringComparer.Ordinal)) assignment.WriteCanonical(writer);
        writer.WriteInt32(plan.GeneratorVersion);
        return writer.ComputeChecksum();
    }
}
