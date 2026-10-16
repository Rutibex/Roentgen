using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;

namespace Roentgen.Campaign;

public static class Phase17Content
{
    public static IReadOnlyList<CampaignPremiseKernel> CreatePremiseKernels() =>
    [
        Premise("resource-conflict", CampaignPremiseKind.ResourceConflict, "The Failing Exchange", [HistoricalEventKind.ResourceFailure, HistoricalEventKind.Conflict], ["resource-dependency", "trade-route", "current-crisis"], 760),
        Premise("diagnostic-failure", CampaignPremiseKind.DiagnosticFailure, "The Diagnosis That Governs", [HistoricalEventKind.CollapseExposure, HistoricalEventKind.Concealment], ["radiographic", "diagnostic", "automated-selection"], 800),
        Premise("buried-system", CampaignPremiseKind.BuriedSystem, "The Buried Instruction", [HistoricalEventKind.Discovery, HistoricalEventKind.CollapseExposure], ["buried-installation", "central-command", "subsurface-catalog"], 740),
        Premise("founding-myth", CampaignPremiseKind.FoundingMyth, "The Necessary Lie", [HistoricalEventKind.FactionFounded, HistoricalEventKind.Concealment], ["faction-founding", "historical-grievance", "concealment"], 720),
        Premise("transport-directive", CampaignPremiseKind.TransportDirective, "The Route That Still Chooses", [HistoricalEventKind.ResourceFailure, HistoricalEventKind.Repurposing], ["trade-route", "machine-directives", "cargo-redirection"], 780),
        Premise("medical-transformation", CampaignPremiseKind.MedicalTransformation, "Treatment Without Consent", [HistoricalEventKind.CollapseExposure, HistoricalEventKind.Discovery], ["medical", "sterilization-failure", "archived-patient-identities"], 750),
        Premise("convergent-cause", CampaignPremiseKind.ConvergentCause, "One Network Beneath Six Crises", [HistoricalEventKind.Concealment, HistoricalEventKind.Conflict, HistoricalEventKind.CollapseExposure], ["convergent-cause", "central-command", "cross-region"], 900)
    ];

    public static IReadOnlyList<MilestoneKernel> CreateMilestoneKernels() =>
    [
        new("opening", CampaignMilestoneRole.Opening, "Opening: The First Diagnostic Contradiction", ["missing-shipment", "contaminated-supply", "false-diagnosis"], new SortedSet<string>(StringComparer.Ordinal), false, false, false),
        new("first-regional-crisis", CampaignMilestoneRole.RegionalCrisis, "First Regional Crisis", ["service-collapse", "contaminated-supply", "ration-diversion"], new SortedSet<string>(StringComparer.Ordinal), false, false, false),
        new("faction-introduction", CampaignMilestoneRole.FactionIntroduction, "Faction Introduction", ["disputed-access", "warrant-and-evidence", "labor-conflict"], new SortedSet<string>(StringComparer.Ordinal), false, false, false),
        new("first-revelation", CampaignMilestoneRole.Revelation, "First Revelation", ["false-diagnosis", "hidden-structural-failure", "medical-anomaly"], new SortedSet<string>(StringComparer.Ordinal), false, true, false),
        new("regional-expansion", CampaignMilestoneRole.RegionalExpansion, "Expansion Into the Wider Regions", ["buried-installation", "competing-expedition", "caravan-loss"], new SortedSet<string>(StringComparer.Ordinal), false, false, false),
        new("faction-realignment", CampaignMilestoneRole.FactionRealignment, "Faction Realignment", ["ceasefire-violation", "prisoner-exchange", "internal-division"], new SortedSet<string>(StringComparer.Ordinal), false, false, false),
        new("first-irreversible-choice", CampaignMilestoneRole.IrreversibleChoice, "First Irreversible Alignment", ["succession-dispute", "debt-and-obligation", "disputed-access"], new SortedSet<string>(StringComparer.Ordinal), true, false, false),
        new("counteraction", CampaignMilestoneRole.Counteraction, "Counteraction", ["sabotage-chain", "settlement-defense", "hostage-surrender-crisis"], new SortedSet<string>(StringComparer.Ordinal), false, false, false),
        new("second-revelation", CampaignMilestoneRole.Revelation, "Second Revelation", ["sealed-archive", "exposed-system", "misidentified-threat"], new SortedSet<string>(StringComparer.Ordinal), false, true, false),
        new("second-irreversible-choice", CampaignMilestoneRole.IrreversibleChoice, "Second Irreversible Alignment", ["embargo-workaround", "unsafe-goods", "evacuation-relocation"], new SortedSet<string>(StringComparer.Ordinal), true, false, false),
        new("final-convergence", CampaignMilestoneRole.FinalConvergence, "Final Convergence", ["exposed-system", "hostage-surrender-crisis", "settlement-defense"], new SortedSet<string>(StringComparer.Ordinal), false, true, true),
        new("ending", CampaignMilestoneRole.Ending, "Ending and World-State Resolution", ["evacuation-relocation", "service-collapse", "debt-and-obligation"], new SortedSet<string>(StringComparer.Ordinal), false, false, false)
    ];

    public static IReadOnlyList<CriticalRoleDefinition> CreateCriticalRoles() =>
    [
        Role("regional-medical-authority", "Regional medical authority", ["medical", "authority"]),
        Role("faction-negotiator", "Faction negotiator", ["faction", "negotiator"]),
        Role("collapse-witness", "Witness to the collapse event", ["witness", "history"]),
        Role("diagnostic-record-carrier", "Carrier of the diagnostic record", ["evidence", "diagnostic"]),
        Role("settlement-representative", "Settlement representative", ["settlement", "representative"]),
        Role("opposition-commander", "Opposition commander", ["faction", "security"]),
        Role("technical-specialist", "Technical specialist", ["engineering", "science"]),
        Role("final-system-custodian", "Final-system custodian", ["machine", "custodian"])
    ];

    public static IReadOnlyList<EndingFamilyDefinition> CreateEndingFamilies(
        StableId campaignId,
        IReadOnlyList<StableId> factionIds,
        StableId firstChoiceId,
        StableId secondChoiceId,
        StableId finalRevelationId,
        StableId convergenceMilestoneId)
    {
        StableId primary = factionIds.Count > 0 ? factionIds[0] : StableId.Empty;
        return
        [
            Ending(campaignId, "repair-and-disclosure", "Repair and Disclosure", EndingTone.Restoration, 900,
            [
                new EndingRequirement(EndingRequirementKind.CampaignFlag, StableId.Empty, "system-repaired", 0),
                new EndingRequirement(EndingRequirementKind.CampaignFlag, StableId.Empty, "truth-disclosed", 0),
                new EndingRequirement(EndingRequirementKind.RevelationProven, finalRevelationId, string.Empty, 0),
                new EndingRequirement(EndingRequirementKind.MilestoneResolved, convergenceMilestoneId, string.Empty, 0)
            ], ["restored-services", "public-record"], "The network was repaired under public scrutiny, and no faction retained the power to define reality alone."),

            Ending(campaignId, "negotiated-containment", "Negotiated Containment", EndingTone.UneasyPeace, 820,
            [
                new EndingRequirement(EndingRequirementKind.ConvergenceForm, StableId.Empty, ConvergenceForm.MultiFactionNegotiation.ToString(), 0),
                new EndingRequirement(EndingRequirementKind.RevelationProven, finalRevelationId, string.Empty, 0)
            ], ["shared-control", "contained-network"], "The factions accepted a fragile shared custody of the surviving systems and the evidence they contained."),

            Ending(campaignId, "controlled-transformation", "Controlled Transformation", EndingTone.Transformation, 800,
            [new EndingRequirement(EndingRequirementKind.ConvergenceForm, StableId.Empty, ConvergenceForm.ControlledTransformation.ToString(), 0)],
            ["transformation-accepted", "diagnostic-citizenship"], "The region chose to live with deliberate alteration rather than pretend the old human baseline could be restored."),

            Ending(campaignId, "evacuation", "The Long Evacuation", EndingTone.Exodus, 760,
            [new EndingRequirement(EndingRequirementKind.ConvergenceForm, StableId.Empty, ConvergenceForm.Evacuation.ToString(), 0)],
            ["population-relocated", "old-regions-abandoned"], "The settlements survived by leaving the systems, claims, and graves that had defined them."),

            Ending(campaignId, "shutdown", "The Quiet Shutdown", EndingTone.Containment, 740,
            [new EndingRequirement(EndingRequirementKind.ConvergenceForm, StableId.Empty, ConvergenceForm.SystemShutdown.ToString(), 0)],
            ["network-silent", "diagnostic-capacity-lost"], "The network fell silent. Its harm ended, along with much of the knowledge and medicine the survivors depended upon."),

            Ending(campaignId, "faction-order", "A Single Custodian", EndingTone.AuthoritarianOrder, 700,
            [
                new EndingRequirement(EndingRequirementKind.CampaignFlag, StableId.Empty, "single-faction-control", 0),
                new EndingRequirement(EndingRequirementKind.FactionExists, primary, string.Empty, 0)
            ], ["single-custodian", "restricted-truth"], "One faction became the sole custodian of diagnosis, history, and access. Order returned, but every answer now required permission."),

            Ending(campaignId, "destructive-liberation", "Destructive Liberation", EndingTone.Fragmentation, 620,
            [new EndingRequirement(EndingRequirementKind.ConvergenceForm, StableId.Empty, ConvergenceForm.Destruction.ToString(), 0)],
            ["network-destroyed", "regional-fragmentation"], "The central installation was destroyed. No one could command it again, and no one could restore what depended upon it."),

            Ending(campaignId, "fallback-survival", "Survival Without Resolution", EndingTone.CatastropheAverted, 100,
            [
                new EndingRequirement(EndingRequirementKind.MilestoneResolved, convergenceMilestoneId, string.Empty, 0),
                new EndingRequirement(EndingRequirementKind.CommitmentOption, firstChoiceId, "*", 0),
                new EndingRequirement(EndingRequirementKind.CommitmentOption, secondChoiceId, "*", 0)
            ], ["crisis-ended", "future-uncertain"], "The immediate catastrophe ended, but the region carried its contradictions into the next generation.")
        ];
    }

    public static IReadOnlyList<EpilogueRule> CreateEpilogueRules(
        StableId campaignId,
        StableId playerId,
        SocietyWorldState society,
        RegionalHistoryPlan history,
        IReadOnlyList<StableId> companionIds,
        IReadOnlyList<EndingFamilyDefinition> endings)
    {
        var rules = new List<EpilogueRule>
        {
            new(
                StableIdFactory.CreateChild(campaignId, "epilogue-rule", "player:survived"),
                "Player",
                playerId,
                900,
                [],
                "The player survived long enough to leave a permanent mark on who could diagnose, command, conceal, or repair the region.",
                new SortedSet<string>(["player", "survival"], StringComparer.Ordinal))
        };

        foreach (RegionalHistoryRecord region in history.Regions.OrderBy(static value => value.Region))
        {
            rules.Add(new EpilogueRule(
                StableIdFactory.CreateChild(campaignId, "epilogue-rule", $"region:{region.Region}:aftermath"),
                "Region",
                region.Id,
                600,
                [],
                $"{region.PresentIdentity} endured beyond the final convergence. Its people continued to live with {string.Join(", ", region.CurrentPressures.Order(StringComparer.Ordinal))}, but the old hidden systems could no longer be treated as rumor alone.",
                new SortedSet<string>(["region", region.Region.ToString().ToLowerInvariant(), "aftermath"], StringComparer.Ordinal)));
        }

        foreach (StableId companionId in companionIds.Where(static value => !value.IsEmpty).Order().Distinct())
        {
            rules.Add(new EpilogueRule(
                StableIdFactory.CreateChild(campaignId, "epilogue-rule", $"companion:{companionId}:survived"),
                "Companion",
                companionId,
                750,
                [],
                "The companion carried memories of the campaign that no official record could fully contain, including the choices made when every faction claimed necessity.",
                new SortedSet<string>(["companion", "survival", "memory"], StringComparer.Ordinal)));
        }
        foreach (FactionState faction in society.Factions.OrderBy(static value => value.Id))
        {
            rules.Add(new EpilogueRule(
                StableIdFactory.CreateChild(campaignId, "epilogue-rule", $"faction:{faction.Id}:trusted"),
                "Faction",
                faction.Id,
                700,
                [new EndingRequirement(EndingRequirementKind.FactionTrustAtLeast, faction.Id, string.Empty, 400)],
                $"{faction.Plan.DisplayName} entered the new order with enough trust to negotiate rather than merely endure it.",
                new SortedSet<string>(["faction", "trust"], StringComparer.Ordinal)));
            rules.Add(new EpilogueRule(
                StableIdFactory.CreateChild(campaignId, "epilogue-rule", $"faction:{faction.Id}:feared"),
                "Faction",
                faction.Id,
                650,
                [new EndingRequirement(EndingRequirementKind.FactionFearAtLeast, faction.Id, string.Empty, 500)],
                $"{faction.Plan.DisplayName} remembered the player as a force to accommodate, fear, or prepare against.",
                new SortedSet<string>(["faction", "fear"], StringComparer.Ordinal)));
        }

        foreach (SettlementState settlement in society.Settlements.OrderBy(static value => value.Id))
        {
            rules.Add(new EpilogueRule(
                StableIdFactory.CreateChild(campaignId, "epilogue-rule", $"settlement:{settlement.Id}:survival"),
                "Settlement",
                settlement.Id,
                500,
                [],
                $"{settlement.Plan.DisplayName} survived into the epilogue, changed by the final disposition of the diagnostic network.",
                new SortedSet<string>(["settlement", "survival"], StringComparer.Ordinal)));
        }

        foreach (EndingFamilyDefinition ending in endings)
        {
            rules.Add(new EpilogueRule(
                StableIdFactory.CreateChild(campaignId, "epilogue-rule", $"ending:{ending.Id}"),
                "World",
                campaignId,
                1000 + ending.Priority,
                [new EndingRequirement(EndingRequirementKind.CampaignFlag, StableId.Empty, $"ending:{ending.Id}", 0)],
                ending.SummaryTemplate,
                new SortedSet<string>(["world", "ending", ending.Tone.ToString().ToLowerInvariant()], StringComparer.Ordinal)));
        }
        return rules;
    }



    private static CampaignPremiseKernel Premise(string id, CampaignPremiseKind kind, string name, HistoricalEventKind[] required, string[] tags, int priority) =>
        new(id, kind, name, new SortedSet<HistoricalEventKind>(required), new SortedSet<string>(tags, StringComparer.Ordinal), priority, 2, 3);

    private static CriticalRoleDefinition Role(string id, string name, string[] tags) => new(
        id,
        name,
        new SortedSet<string>(tags, StringComparer.Ordinal),
        [CriticalRoleFallbackKind.NamedReplacement, CriticalRoleFallbackKind.QualifiedFactionMember, CriticalRoleFallbackKind.AlternateFactionOffice, CriticalRoleFallbackKind.DocumentOrRecording, CriticalRoleFallbackKind.TerminalOrMachine, CriticalRoleFallbackKind.AftermathRoute],
        true);

    private static EndingFamilyDefinition Ending(StableId campaignId, string id, string name, EndingTone tone, int priority, IReadOnlyList<EndingRequirement> requirements, string[] flags, string summary) =>
        new(StableIdFactory.CreateChild(campaignId, "ending-family", id), name, tone, priority, requirements, new SortedSet<string>(flags, StringComparer.Ordinal), summary);
}
