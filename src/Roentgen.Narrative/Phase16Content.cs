using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Narrative;

public static class Phase16Content
{
    public static readonly StableId ProspectClinicId = StableIdFactory.Create("settlement", "prospect-clinic");
    public static readonly StableId ProspectCaretakerId = StableIdFactory.Create("actor", "prospect-caretaker");
    public static readonly StableId ProspectDeputyId = StableIdFactory.Create("actor", "prospect-deputy");
    public static readonly StableId BlackwaterFactionId = StableIdFactory.Create("faction", "blackwater-recovery-authority");
    public static readonly StableId RedQuarryFactionId = StableIdFactory.Create("faction", "red-quarry-cooperative");
    public static readonly StableId MedicineResourceId = StableIdFactory.Create("resource", "clinical-medicine");
    public static readonly StableId MeridianRelayId = StableIdFactory.Create("location", "meridian-diagnostic-relay");
    public static readonly StableId QuarantineDepotId = StableIdFactory.Create("location", "meridian-quarantine-depot");
    public static readonly StableId ShipmentManifestId = StableIdFactory.Create("evidence", "pale-manifest");
    public static readonly StableId ClinicMedicalServiceId = StableIdFactory.Create("service", "prospect-clinic-medical");

    private static readonly (string Id, string Name, string Theme)[] KernelCatalog =
    [
        ("contaminated-supply", "Contaminated Supply", "resource-crisis"),
        ("service-collapse", "Service Collapse", "settlement-service"),
        ("missing-shipment", "Missing Shipment", "trade"),
        ("disputed-access", "Disputed Access", "faction-resource"),
        ("ration-diversion", "Ration Diversion", "scarcity"),
        ("succession-dispute", "Succession Dispute", "leadership"),
        ("internal-division", "Internal Division", "faction-politics"),
        ("prisoner-exchange", "Prisoner Exchange", "diplomacy"),
        ("warrant-and-evidence", "Warrant and Evidence", "crime"),
        ("impersonation-infiltration", "Impersonation Infiltration", "disguise"),
        ("hidden-structural-failure", "Hidden Structural Failure", "diagnostic"),
        ("medical-anomaly", "Medical Anomaly", "medicine"),
        ("false-diagnosis", "False Diagnosis", "knowledge"),
        ("exposed-system", "Exposed System", "radiographic"),
        ("missing-person-trail", "Missing Person Trail", "investigation"),
        ("ceasefire-violation", "Ceasefire Violation", "conflict"),
        ("sabotage-chain", "Sabotage Chain", "sabotage"),
        ("settlement-defense", "Settlement Defense", "security"),
        ("hostage-surrender-crisis", "Hostage or Surrender Crisis", "surrender"),
        ("misidentified-threat", "Misidentified Threat", "creature"),
        ("caravan-loss", "Caravan Loss", "trade"),
        ("embargo-workaround", "Embargo Workaround", "economy"),
        ("unsafe-goods", "Unsafe Goods", "commerce"),
        ("labor-conflict", "Labor Conflict", "society"),
        ("debt-and-obligation", "Debt and Obligation", "obligation"),
        ("buried-installation", "Buried Installation", "exploration"),
        ("sealed-archive", "Sealed Archive", "knowledge"),
        ("hazardous-salvage", "Hazardous Salvage", "salvage"),
        ("competing-expedition", "Competing Expedition", "exploration"),
        ("evacuation-relocation", "Evacuation or Relocation", "migration")
    ];

    public static IReadOnlyList<QuestKernelDefinition> CreateInitialKernels() => KernelCatalog.Select(entry => BuildKernel(entry.Id, entry.Name, entry.Theme)).ToArray();

    public static QuestKernelDefinition CreatePaleManifestKernel() => BuildKernel("missing-shipment", "The Pale Manifest", "diagnostic-trade");

    public static QuestKernelDefinition BuildLaunchKernel(string id, string displayName, string theme) => BuildKernel(id, displayName, theme);

    public static NarrativeWorldState CreateRegressionWorld(CampaignSeed campaignSeed, StableId playerId)
    {
        var world = new NarrativeWorldState();
        StableId origin = StableIdFactory.Create("narrative-origin", campaignSeed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        FactRecord shipmentMissing = new(StableIdFactory.CreateChild(origin, "fact", "shipment-missing"), "shipment.missing", MedicineResourceId, NarrativeValue.FromBoolean(true), FactTruthState.True, FactScope.Settlement, origin, 0, null);
        FactRecord relayDamaged = new(StableIdFactory.CreateChild(origin, "fact", "relay-damaged"), "machine.damaged", MeridianRelayId, NarrativeValue.FromBoolean(true), FactTruthState.True, FactScope.Region, origin, 0, null);
        FactRecord hiddenCause = new(StableIdFactory.CreateChild(origin, "fact", "hidden-cause"), "shipment.redirected-by", MedicineResourceId, NarrativeValue.FromStableId(MeridianRelayId), FactTruthState.True, FactScope.Quest, origin, 0, null);
        FactRecord clinicNeedsMedicine = new(StableIdFactory.CreateChild(origin, "fact", "clinic-needs-medicine"), "settlement.needs-resource", ProspectClinicId, NarrativeValue.FromStableId(MedicineResourceId), FactTruthState.True, FactScope.Settlement, origin, 0, null);
        world.UpsertFact(shipmentMissing); world.UpsertFact(relayDamaged); world.UpsertFact(hiddenCause); world.UpsertFact(clinicNeedsMedicine);

        var caretakerSource = new KnowledgeSource(StableIdFactory.CreateChild(origin, "source", "caretaker-observation"), KnowledgeSourceKind.Observation, ProspectCaretakerId, "shipment did not arrive", 0);
        world.AddKnowledge(new KnowledgeClaim(StableIdFactory.CreateChild(shipmentMissing.Id, "claim", ProspectCaretakerId.ToString()), ProspectCaretakerId, shipmentMissing.Id, FactTruthState.True, 900, caretakerSource, 900, 0, 0, KnowledgeSecrecy.Public, false, true));
        world.AddKnowledge(new KnowledgeClaim(StableIdFactory.CreateChild(clinicNeedsMedicine.Id, "claim", ProspectCaretakerId.ToString()), ProspectCaretakerId, clinicNeedsMedicine.Id, FactTruthState.True, 1000, caretakerSource, 1000, 0, 0, KnowledgeSecrecy.Professional, false, true));

        QuestBindingContext context = CreateRegressionBindingContext();
        QuestBindingResult binding = new QuestBinder().Bind(CreatePaleManifestKernel(), context, StableIdFactory.CreateCampaignRoot(campaignSeed), campaignSeed.Value);
        if (!binding.Success || binding.Plan is null) throw new InvalidOperationException("The Pale Manifest regression quest failed to bind: " + string.Join("; ", binding.Errors));
        var quest = new QuestInstanceState(binding.Plan); quest.Offer(0); world.AddQuest(quest);
        world.SetTime(0, 0);
        return world;
    }

    public static QuestBindingContext CreateRegressionBindingContext()
    {
        var context = new QuestBindingContext();
        Add(context, ProspectClinicId, "Settlement", 1000, "medical refuge settlement", "medical", "resource-dependent");
        Add(context, ProspectCaretakerId, "Actor", 1000, "clinic caretaker", "requester", "medical", "leader");
        Add(context, ProspectDeputyId, "Actor", 800, "clinic deputy", "replacement", "medical");
        Add(context, BlackwaterFactionId, "Faction", 950, "accused industrial authority", "accused", "industrial");
        Add(context, RedQuarryFactionId, "Faction", 850, "carrier cooperative", "carrier", "trade");
        Add(context, MedicineResourceId, "Resource", 1000, "critical medicine shipment", "medical", "scarce");
        Add(context, MeridianRelayId, "Location", 1000, "damaged diagnostic relay", "technical", "hidden-cause");
        Add(context, QuarantineDepotId, "Location", 900, "automated quarantine depot", "restricted", "stealth");
        Add(context, ShipmentManifestId, "Evidence", 1000, "shipment manifest", "document", "diagnostic");
        Add(context, ClinicMedicalServiceId, "Service", 1000, "Prospect Clinic medical service", "medical", "critical");
        return context;
    }

    private static void Add(QuestBindingContext context, StableId id, string type, int score, string description, params string[] tags) => context.Add(new NarrativeBindingCandidate(id, type, new HashSet<string>(tags, StringComparer.Ordinal), score, true, description));

    private static QuestKernelDefinition BuildKernel(string id, string displayName, string theme)
    {
        QuestSlotDefinition[] slots =
        [
            new("DEPENDENT_SETTLEMENT", "Settlement", true, true, []),
            new("REQUESTING_ACTOR", "Actor", true, true, ["requester"]),
            new("REPLACEMENT_ACTOR", "Actor", false, true, ["replacement"]),
            new("ACCUSED_FACTION", "Faction", true, true, ["accused"]),
            new("CARRIER_FACTION", "Faction", false, true, ["carrier"]),
            new("MISSING_RESOURCE", "Resource", true, true, []),
            new("PRIMARY_LOCATION", "Location", true, true, []),
            new("SECONDARY_LOCATION", "Location", false, true, []),
            new("EVIDENCE", "Evidence", true, true, []),
            new("FAILED_SERVICE", "Service", false, true, [])
        ];

        NarrativeConditionDefinition evidenceFound = new(NarrativeConditionKind.FactExists, "evidence-found", "quest.evidence.discovered", "EVIDENCE", NarrativeValue.FromBoolean(true), 0, 0, []);
        NarrativeConditionDefinition resolved = new(NarrativeConditionKind.FactExists, "problem-resolved", "quest.problem.resolved", "PRIMARY_LOCATION", NarrativeValue.FromBoolean(true), 0, 0, []);
        QuestObjectiveDefinition[] objectives =
        [
            new("speak-requester", "Speak with {REQUESTING_ACTOR}.", ObjectiveKind.Speak, ObjectiveComposition.Single, false, false, "REQUESTING_ACTOR", NarrativeConditionDefinition.Always("conversation-started"), new NarrativeConditionDefinition(NarrativeConditionKind.Not, "never-fail", string.Empty, string.Empty, NarrativeValue.None, 0, 0, [NarrativeConditionDefinition.Always()]), []),
            new("discover-evidence", "Discover evidence at {EVIDENCE}.", ObjectiveKind.DiscoverFact, ObjectiveComposition.Single, false, false, "EVIDENCE", evidenceFound, new NarrativeConditionDefinition(NarrativeConditionKind.Not, "never-fail", string.Empty, string.Empty, NarrativeValue.None, 0, 0, [NarrativeConditionDefinition.Always()]), []),
            new("resolve-problem", "Resolve the crisis at {PRIMARY_LOCATION}.", ObjectiveKind.ChooseOutcome, ObjectiveComposition.Any, false, false, "PRIMARY_LOCATION", resolved, new NarrativeConditionDefinition(NarrativeConditionKind.Not, "never-fail", string.Empty, string.Empty, NarrativeValue.None, 0, 0, [NarrativeConditionDefinition.Always()]), [])
        ];

        QuestNodeDefinition[] nodes =
        [
            new("opening", QuestNodeKind.Stage, "speak-requester", NarrativeConditionDefinition.Always(), ["investigation"], []),
            new("investigation", QuestNodeKind.Objective, "discover-evidence", NarrativeConditionDefinition.Always(), ["resolution"], []),
            new("resolution", QuestNodeKind.Choice, "resolve-problem", NarrativeConditionDefinition.Always(), ["completion"], []),
            new("completion", QuestNodeKind.Completion, string.Empty, resolved, [], [])
        ];

        QuestRouteDefinition[] routes =
        [
            new("technical-route", QuestRouteKind.Technical, "investigation", ["discover-evidence", "resolve-problem"], SkillId.Engineering, 50, RetryPolicy.AfterEquipmentChange),
            new("scientific-route", QuestRouteKind.Scientific, "investigation", ["discover-evidence", "resolve-problem"], SkillId.Science, 50, RetryPolicy.AfterEvidence),
            new("medical-route", QuestRouteKind.Medical, "investigation", ["discover-evidence", "resolve-problem"], SkillId.Medicine, 50, RetryPolicy.AfterEvidence),
            new("social-route", QuestRouteKind.Social, "opening", ["speak-requester", "resolve-problem"], SkillId.Influence, 55, RetryPolicy.AfterFactionLeverage),
            new("economic-route", QuestRouteKind.Economic, "opening", ["speak-requester", "resolve-problem"], SkillId.Commerce, 50, RetryPolicy.AfterFactionLeverage),
            new("stealth-route", QuestRouteKind.Stealth, "investigation", ["discover-evidence", "resolve-problem"], SkillId.Stealth, 55, RetryPolicy.DistinctApproach),
            new("combat-route", QuestRouteKind.Combat, "resolution", ["resolve-problem"], SkillId.Ballistics, 45, RetryPolicy.DistinctApproach),
            new("exploration-route", QuestRouteKind.Exploration, "investigation", ["discover-evidence", "resolve-problem"], SkillId.Fieldcraft, 45, RetryPolicy.AfterEvidence)
        ];

        QuestRecoveryDefinition[] recoveries =
        [
            new("replace-requester", RecoveryKind.ActorReplacement, "REQUESTING_ACTOR", "REPLACEMENT_ACTOR", "opening"),
            new("evidence-substitute", RecoveryKind.EvidenceSubstitute, "EVIDENCE", "PRIMARY_LOCATION", "investigation"),
            new("aftermath-route", RecoveryKind.AftermathRoute, "PRIMARY_LOCATION", "SECONDARY_LOCATION", "resolution")
        ];

        NarrativeEffectDefinition resolveEffect = new(NarrativeEffectKind.AssertFact, "dialogue-resolve", "PRIMARY_LOCATION", "quest.problem.resolved", NarrativeValue.FromBoolean(true), 0, string.Empty);
        NarrativeEffectDefinition evidenceEffect = new(NarrativeEffectKind.AssertFact, "dialogue-evidence", "EVIDENCE", "quest.evidence.discovered", NarrativeValue.FromBoolean(true), 0, string.Empty);
        DialogueNodeDefinition[] dialogue =
        [
            new("greeting", DialogueIntent.Greet, "REQUESTING_ACTOR", "{REQUESTING_ACTOR} studies you through the clinic's pale screen. The settlement needs {MISSING_RESOURCE}, and {ACCUSED_FACTION} is being blamed.", NarrativeConditionDefinition.Always(), string.Empty,
            [
                new("ask-evidence", "What evidence do you have?", NarrativeConditionDefinition.Always(), SkillId.Science, 35, RetryPolicy.Never, "evidence", [evidenceEffect]),
                new("offer-help", "I will investigate {PRIMARY_LOCATION}.", NarrativeConditionDefinition.Always(), null, 0, RetryPolicy.Never, "assignment", []),
                new("pressure-answer", "Tell me what you are withholding.", NarrativeConditionDefinition.Always(), SkillId.Influence, 55, RetryPolicy.AfterEvidence, "evidence", [evidenceEffect])
            ], "assignment", false),
            new("evidence", DialogueIntent.Reveal, "REQUESTING_ACTOR", "The manifest indicates the cargo was redirected by a diagnostic process, not simply stolen. Someone must inspect {PRIMARY_LOCATION} and recover {EVIDENCE}.", NarrativeConditionDefinition.Always(), string.Empty, [new("accept-evidence-route", "I will trace the diagnostic record.", NarrativeConditionDefinition.Always(), null, 0, RetryPolicy.Never, "assignment", [])], "assignment", false),
            new("assignment", DialogueIntent.Request, "REQUESTING_ACTOR", "Recover the truth, then decide whether to repair, expose, negotiate, infiltrate, or seize control of the situation.", NarrativeConditionDefinition.Always(), string.Empty, [new("resolve-now-debug", "[Development] Mark the crisis resolved.", NarrativeConditionDefinition.Always(), null, 0, RetryPolicy.Never, "end", [resolveEffect])], "end", false),
            new("end", DialogueIntent.End, "REQUESTING_ACTOR", "Return when the facts can survive scrutiny.", NarrativeConditionDefinition.Always(), string.Empty, [], string.Empty, true)
        ];

        NarrativeEffectDefinition[] consequences =
        [
            new(NarrativeEffectKind.AssertFact, "quest-completed-fact", "DEPENDENT_SETTLEMENT", "quest.resolved", NarrativeValue.FromToken(id), 0, string.Empty),
            new(NarrativeEffectKind.ChangeReputation, "requester-trust", "ACCUSED_FACTION", string.Empty, NarrativeValue.None, 40, "trust"),
            new(NarrativeEffectKind.ScheduleFollowUp, "schedule-follow-up", string.Empty, string.Empty, NarrativeValue.None, 0, $"follow-up:{id}")
        ];

        return new QuestKernelDefinition(id, 1, displayName, [theme, "world-fact", "multi-solution"], slots, objectives, nodes, routes, recoveries, dialogue, consequences, [$"follow-up:{id}"]);
    }
}
