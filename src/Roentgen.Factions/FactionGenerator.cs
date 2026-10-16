using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public sealed record SocietyContentBundle(
    IReadOnlyDictionary<string, FactionKernelDefinition> FactionKernels,
    IReadOnlyList<SettlementGenerationProfile> RegressionSettlementProfiles);

public static class Phase15Content
{
    public static StableId ProspectSettlementId { get; } = StableIdFactory.Create("settlement", "prospect-clinic");
    public static StableId BlackwaterSettlementId { get; } = StableIdFactory.Create("settlement", "blackwater-intake");
    public static StableId RedQuarrySettlementId { get; } = StableIdFactory.Create("settlement", "red-quarry-crossing");
    public static StableId MeridianRelayLocationId { get; } = StableIdFactory.Create("location", "meridian-relay");

    public static SocietyContentBundle Create()
    {
        var kernels = new SortedDictionary<string, FactionKernelDefinition>(StringComparer.Ordinal)
        {
            ["medical-compact"] = new FactionKernelDefinition(
                "medical-compact", "{place} Clinic Compact", "Survivors preserved a functioning diagnostic clinic and made treatment a civic obligation.", "Controlled treatment, salvage triage, and reciprocal refuge.",
                Ideology(-700, -250, 200, -250, -750, -450, -350, -100),
                [new FactionNeed(SocietyCommodity.Water, 18_000, 1000), new FactionNeed(SocietyCommodity.Medicine, 12_000, 1000), new FactionNeed(SocietyCommodity.DiagnosticMaterials, 8_000, 850)],
                Set("denial-of-emergency-care", "uncontrolled-radiographic-exposure"), new LeadershipModelDefinition(LeadershipSelectionMethod.Consensus, "caretaker-convenor", 360, true, Set("medical", "caretaker")),
                Set("medical", "clinic-guard", "diagnostic"), Set("medical-compound", "refuge"), Set("clinical", "measured", "reciprocal"),
                [new FactionObjectiveDefinition("preserve-clinic", "Keep the diagnostic and treatment systems operating.", 1000, Set("medical-site")), new FactionObjectiveDefinition("expand-clean-water", "Secure a reliable clean-water source.", 850, Set("water-source"))], Set("universal-care", "resource-triage", "security")),
            ["industrial-authority"] = new FactionKernelDefinition(
                "industrial-authority", "{place} Recovery Authority", "Industrial containment crews retained extraction machinery and sealed the most dangerous works.", "Credentialed recovery, chemical processing, and controlled access.",
                Ideology(-100, 650, -650, 100, 150, -400, 450, -600),
                [new FactionNeed(SocietyCommodity.Food, 20_000, 950), new FactionNeed(SocietyCommodity.Water, 16_000, 1000), new FactionNeed(SocietyCommodity.Metals, 10_000, 700)],
                Set("unauthorized-site-entry", "unlicensed-contamination-release"), new LeadershipModelDefinition(LeadershipSelectionMethod.TechnicalQualification, "recovery-director", 0, true, Set("engineering", "security-clearance")),
                Set("industrial", "sealed", "chemical", "security"), Set("industrial-campus", "restricted"), Set("technical", "procedural", "credentialed"),
                [new FactionObjectiveDefinition("secure-works", "Control the Blackwater treatment and recovery works.", 1000, Set("industrial-site")), new FactionObjectiveDefinition("restore-machinery", "Restore extraction and containment machinery.", 800, Set("machinery"))], Set("commercial", "containment", "security")),
            ["quarry-cooperative"] = new FactionKernelDefinition(
                "quarry-cooperative", "{place} Cooperative", "Quarry crews pooled tools and ration rights after the transport corridor failed.", "Collective extraction, open accounting, and negotiated exchange.",
                Ideology(-450, -500, 550, -100, -700, -350, 100, -450),
                [new FactionNeed(SocietyCommodity.Fuel, 16_000, 1000), new FactionNeed(SocietyCommodity.Medicine, 7_000, 750), new FactionNeed(SocietyCommodity.Water, 14_000, 900)],
                Set("private-hoarding-of-shared-tools"), new LeadershipModelDefinition(LeadershipSelectionMethod.Election, "shift-delegate", 180, true, Set("worker", "member")),
                Set("quarry", "industrial", "workwear"), Set("excavation", "cooperative"), Set("plainspoken", "ledger", "collective"),
                [new FactionObjectiveDefinition("keep-route-open", "Keep the quarry road and freight route open.", 950, Set("road")), new FactionObjectiveDefinition("secure-fuel", "Secure a dependable fuel agreement.", 850, Set("fuel-source"))], Set("workers", "traders", "traditionalists"))
        };
        return new SocietyContentBundle(kernels, CreateRegressionProfiles());
    }

    public static SocietyWorldState CreateRegressionWorld(CampaignSeed seed, StableId playerId)
    {
        SocietyContentBundle content = Create(); var generator = new FactionGenerator(content); var request = new SocietyGenerationRequest(seed, StableIdFactory.CreateCampaignRoot(seed), Set("medical-site", "water-source", "industrial-site", "machinery", "road", "fuel-source"), 3, 3);
        SocietyWorldState world = generator.Generate(request);
        foreach (FactionState faction in world.Factions) world.AddReputation(new FactionReputationState(playerId, faction.Id));
        return world;
    }

    private static IReadOnlyList<SettlementGenerationProfile> CreateRegressionProfiles() =>
    [
        new(ProspectSettlementId, "Prospect Clinic", SettlementTopologyGrammar.MedicalCompound, FactionIds.ProspectClinicCompact, "medical refuge", "treatment and diagnostics", "medicine surplus but ammunition shortage", "layered gate and clinic guard", "prospect-clinic-medical", Set("civic", "market", "residential", "medical", "defensive"), Set("medical", "water", "market", "repair", "security", "diagnostic"), 180, 720),
        new(BlackwaterSettlementId, "Blackwater Intake", SettlementTopologyGrammar.IndustrialCampus, FactionIds.BlackwaterRecoveryAuthority, "industrial containment station", "chemical processing and recovery", "food shortage and failing pumps", "credentials, sealed zones, and patrol corridors", "blackwater-industrial", Set("industrial", "residential", "waste", "defensive"), Set("water", "repair", "market", "security", "transport"), 230, 820),
        new(RedQuarrySettlementId, "Red Quarry Crossing", SettlementTopologyGrammar.Excavation, FactionIds.RedQuarryCooperative, "quarry worker cooperative", "metal and structural material extraction", "fuel shortage and unstable access road", "overwatch terraces and work crews", "red-quarry-excavation", Set("workshop", "residential", "market", "defensive"), Set("repair", "market", "storage", "transport", "medical"), 150, 660)
    ];

    private static IdeologyValue[] Ideology(params int[] values)
    {
        IdeologyAxis[] axes = Enum.GetValues<IdeologyAxis>(); if (values.Length != axes.Length) throw new ArgumentException("Every ideology axis requires a value.", nameof(values)); var result = new IdeologyValue[values.Length]; for (int index = 0; index < values.Length; index++) result[index] = new IdeologyValue(axes[index], values[index]); return result;
    }
    public static IReadOnlySet<string> Set(params string[] values) => new SortedSet<string>(values, StringComparer.Ordinal);
}

public sealed class FactionGenerator
{
    public const int GeneratorVersion = 15;
    private readonly SocietyContentBundle content;
    public FactionGenerator(SocietyContentBundle content) => this.content = content ?? throw new ArgumentNullException(nameof(content));

    public SocietyWorldState Generate(SocietyGenerationRequest request)
    {
        if (request.MajorFactionCount < 1) throw new ArgumentOutOfRangeException(nameof(request));
        var world = new SocietyWorldState();
        FactionState prospect = CreateFaction(request.CampaignSeed, FactionIds.ProspectClinicCompact, content.FactionKernels["medical-compact"], "Prospect");
        FactionState blackwater = CreateFaction(request.CampaignSeed, FactionIds.BlackwaterRecoveryAuthority, content.FactionKernels["industrial-authority"], "Blackwater");
        FactionState quarry = CreateFaction(request.CampaignSeed, FactionIds.RedQuarryCooperative, content.FactionKernels["quarry-cooperative"], "Red Quarry");
        world.AddFaction(prospect); world.AddFaction(blackwater); world.AddFaction(quarry);

        IReadOnlyList<SettlementGenerationProfile> profiles = content.RegressionSettlementProfiles;
        SettlementState prospectSettlement = CreateSettlement(profiles[0], 133, new Dictionary<SocietyCommodity, (long Quantity, long Capacity, long Target)>
        {
            [SocietyCommodity.Food] = (80_000, 180_000, 100_000), [SocietyCommodity.Water] = (110_000, 220_000, 130_000), [SocietyCommodity.Medicine] = (90_000, 160_000, 70_000), [SocietyCommodity.Ammunition] = (16_000, 100_000, 70_000), [SocietyCommodity.DiagnosticMaterials] = (65_000, 120_000, 60_000), [SocietyCommodity.Metals] = (25_000, 100_000, 45_000), [SocietyCommodity.Chemicals] = (8_000, 100_000, 35_000)
        });
        SettlementState blackwaterSettlement = CreateSettlement(profiles[1], 188, new Dictionary<SocietyCommodity, (long Quantity, long Capacity, long Target)>
        {
            [SocietyCommodity.Food] = (30_000, 220_000, 150_000), [SocietyCommodity.Water] = (70_000, 240_000, 160_000), [SocietyCommodity.Chemicals] = (190_000, 260_000, 120_000), [SocietyCommodity.Electronics] = (85_000, 160_000, 80_000), [SocietyCommodity.Metals] = (110_000, 220_000, 110_000), [SocietyCommodity.Fuel] = (75_000, 180_000, 90_000), [SocietyCommodity.Medicine] = (12_000, 100_000, 55_000)
        });
        SettlementState quarrySettlement = CreateSettlement(profiles[2], 109, new Dictionary<SocietyCommodity, (long Quantity, long Capacity, long Target)>
        {
            [SocietyCommodity.Food] = (75_000, 160_000, 90_000), [SocietyCommodity.Water] = (52_000, 180_000, 100_000), [SocietyCommodity.Metals] = (210_000, 280_000, 130_000), [SocietyCommodity.Fuel] = (20_000, 140_000, 90_000), [SocietyCommodity.Medicine] = (18_000, 100_000, 50_000), [SocietyCommodity.Ammunition] = (70_000, 120_000, 70_000)
        });
        ConfigureProspect(prospectSettlement); ConfigureBlackwater(blackwaterSettlement); ConfigureQuarry(quarrySettlement);
        world.AddSettlement(prospectSettlement); world.AddSettlement(blackwaterSettlement); world.AddSettlement(quarrySettlement);
        prospect.AddControlledSettlement(prospectSettlement.Id); blackwater.AddControlledSettlement(blackwaterSettlement.Id); quarry.AddControlledSettlement(quarrySettlement.Id);

        AddRelationships(world, prospect, blackwater, quarry); AddLeadership(world, prospect, blackwater, quarry); AddTerritory(world, prospectSettlement, blackwaterSettlement, quarrySettlement); AddTradeRoutes(world, prospectSettlement, blackwaterSettlement, quarrySettlement);
        SocietyValidationReport report = new SocietyValidator().Validate(world, request.AvailableWorldTags); if (!report.IsValid) throw new InvalidOperationException("Generated Phase 15 regression society failed validation: " + string.Join("; ", report.Issues.Select(static value => value.Code)));
        return world;
    }

    private FactionState CreateFaction(CampaignSeed seed, StableId id, FactionKernelDefinition kernel, string place)
    {
        string name = kernel.DisplayNamePattern.Replace("{place}", place, StringComparison.Ordinal); string architecture = kernel.Id switch { "medical-compact" => "prospect-clinic-medical", "industrial-authority" => "blackwater-industrial", _ => "red-quarry-excavation" }; string motif = $"faction.{kernel.Id}.{Pick(seed, kernel.Id + ".motif", 4)}";
        var writer = new CanonicalWriter(); writer.WriteStableId(id); writer.WriteString(kernel.Id); writer.WriteUInt64(seed.Value); writer.WriteString(name); writer.WriteString(architecture); writer.WriteString(motif); StateChecksum fingerprint = writer.ComputeChecksum();
        var plan = new GeneratedFactionPlan(id, kernel.Id, name, kernel.FoundingEvent, kernel.SurvivalMethod, kernel.Ideology, kernel.MaterialNeeds, kernel.Taboos, kernel.Leadership, kernel.EquipmentTags, kernel.SettlementTags, kernel.SpeechTags, kernel.Objectives, kernel.InternalDivisionTags, architecture, motif, GeneratorVersion, fingerprint);
        return new FactionState(plan);
    }

    private static SettlementState CreateSettlement(SettlementGenerationProfile plan, int population, IReadOnlyDictionary<SocietyCommodity, (long Quantity, long Capacity, long Target)> stocks)
    {
        var populationState = new PopulationState(plan.SettlementId, plan.PopulationCapacity); int workers = population * 48 / 100; int children = population * 14 / 100; int elders = population * 10 / 100; int guards = Math.Max(6, population * 8 / 100); int specialists = Math.Max(5, population * 10 / 100); int dependants = Math.Max(0, population - workers - children - elders - guards - specialists);
        populationState.Upsert(new PopulationCohortState(PopulationCohortKind.WorkingAdults, workers)); populationState.Upsert(new PopulationCohortState(PopulationCohortKind.Children, children)); populationState.Upsert(new PopulationCohortState(PopulationCohortKind.Elders, elders)); populationState.Upsert(new PopulationCohortState(PopulationCohortKind.Guards, guards, 860, 760)); populationState.Upsert(new PopulationCohortState(PopulationCohortKind.Specialists, specialists, 840, 740)); populationState.Upsert(new PopulationCohortState(PopulationCohortKind.Dependants, dependants));
        var result = new SettlementState(plan, populationState); foreach (KeyValuePair<SocietyCommodity, (long Quantity, long Capacity, long Target)> pair in stocks.OrderBy(static value => value.Key)) result.UpsertStock(new ResourceStockState(pair.Key, pair.Value.Quantity, pair.Value.Capacity, pair.Value.Target)); return result;
    }

    private static void ConfigureProspect(SettlementState settlement)
    {
        settlement.SetTax(60); settlement.SetMerchantRisk(90); settlement.SetCurrencyPolicy("prospect-clinic-scrip"); settlement.AddPreferredGood("medicine"); settlement.AddPreferredGood("ammunition"); settlement.AddProhibitedGood("unsealed-radiographic-source");
        AddProcess(settlement, SocietyCommodity.Medicine, 22_000, true, "medical"); AddProcess(settlement, SocietyCommodity.Water, 18_000, true, "water"); AddProcess(settlement, SocietyCommodity.Food, 13_000, false, "food"); AddProcess(settlement, SocietyCommodity.Water, 17_000, false, "water");
        AddService(settlement, SettlementServiceKind.MedicalCare, 70, SocietyCommodity.Medicine, 7_000); AddService(settlement, SettlementServiceKind.DiagnosticAnalysis, 45, SocietyCommodity.DiagnosticMaterials, 4_000); AddService(settlement, SettlementServiceKind.CleanWater, 150, SocietyCommodity.Water, 3_000); AddService(settlement, SettlementServiceKind.Market, 80); AddService(settlement, SettlementServiceKind.Repair, 35, SocietyCommodity.Metals, 2_000); AddService(settlement, SettlementServiceKind.Security, 80, SocietyCommodity.Ammunition, 2_000);
    }
    private static void ConfigureBlackwater(SettlementState settlement)
    {
        settlement.SetTax(120); settlement.SetMerchantRisk(220); settlement.SetCurrencyPolicy("authority-credit"); settlement.AddPreferredGood("food"); settlement.AddPreferredGood("water"); settlement.AddProhibitedGood("uncredentialed-industrial-tool");
        AddProcess(settlement, SocietyCommodity.Chemicals, 32_000, true, "repair"); AddProcess(settlement, SocietyCommodity.Electronics, 12_000, true, "repair"); AddProcess(settlement, SocietyCommodity.Food, 18_000, false, "food"); AddProcess(settlement, SocietyCommodity.Water, 19_000, false, "water");
        AddService(settlement, SettlementServiceKind.Repair, 100, SocietyCommodity.Metals, 4_000); AddService(settlement, SettlementServiceKind.Security, 140, SocietyCommodity.Ammunition, 3_000); AddService(settlement, SettlementServiceKind.Transport, 90, SocietyCommodity.Fuel, 5_000); AddService(settlement, SettlementServiceKind.CleanWater, 110, SocietyCommodity.Water, 4_000); AddService(settlement, SettlementServiceKind.Market, 65);
    }
    private static void ConfigureQuarry(SettlementState settlement)
    {
        settlement.SetTax(45); settlement.SetMerchantRisk(140); settlement.SetCurrencyPolicy("cooperative-ledger"); settlement.AddPreferredGood("fuel"); settlement.AddPreferredGood("medicine"); settlement.AddProhibitedGood("stolen-cooperative-tool");
        AddProcess(settlement, SocietyCommodity.Metals, 45_000, true, "repair"); AddProcess(settlement, SocietyCommodity.Fuel, 12_000, false, "transport"); AddProcess(settlement, SocietyCommodity.Water, 13_000, false, "water"); AddProcess(settlement, SocietyCommodity.Food, 11_000, false, "food");
        AddService(settlement, SettlementServiceKind.Repair, 120, SocietyCommodity.Metals, 3_000); AddService(settlement, SettlementServiceKind.Transport, 100, SocietyCommodity.Fuel, 5_000); AddService(settlement, SettlementServiceKind.Storage, 160); AddService(settlement, SettlementServiceKind.Market, 75); AddService(settlement, SettlementServiceKind.MedicalCare, 25, SocietyCommodity.Medicine, 3_000);
    }
    private static void AddProcess(SettlementState settlement, SocietyCommodity commodity, long amount, bool production, string requiredService) => settlement.UpsertProcess(new ResourceProcessState(StableIdFactory.CreateChild(settlement.Id, production ? "production" : "consumption", commodity.ToString()), commodity, amount, production, requiredService, 1000));
    private static void AddService(SettlementState settlement, SettlementServiceKind kind, int capacity, SocietyCommodity? input = null, long amount = 0) { var service = new SettlementServiceState(StableIdFactory.CreateChild(settlement.Id, "service", kind.ToString()), settlement.Id, kind, settlement.ControllingFactionId, capacity, 800); if (input.HasValue) service.SetRequiredInput(input.Value, amount); settlement.UpsertService(service); }

    private static void AddRelationships(SocietyWorldState world, FactionState prospect, FactionState blackwater, FactionState quarry)
    {
        foreach ((FactionState source, FactionState target) in new[] { (prospect, blackwater), (blackwater, prospect), (prospect, quarry), (quarry, prospect), (blackwater, quarry), (quarry, blackwater) })
        {
            var relation = new FactionRelationshipState(source.Id, target.Id); int compatibility = ComputeCompatibility(source, target); relation.Adjust(compatibility: compatibility, intelligenceConfidence: 200);
            if ((source.Id == prospect.Id && target.Id == quarry.Id) || (source.Id == quarry.Id && target.Id == prospect.Id)) relation.Adjust(trust: 400, respect: 300, tradeReliance: 650, dependency: 400);
            if ((source.Id == blackwater.Id && target.Id == quarry.Id) || (source.Id == quarry.Id && target.Id == blackwater.Id)) relation.Adjust(trust: 50, respect: 150, tradeReliance: 420, dependency: 350, borderTension: 130);
            if ((source.Id == prospect.Id && target.Id == blackwater.Id) || (source.Id == blackwater.Id && target.Id == prospect.Id)) relation.Adjust(trust: -120, respect: 180, grievance: 170, borderTension: 220, tradeReliance: 190);
            world.AddRelationship(relation);
        }
    }
    private static int ComputeCompatibility(FactionState first, FactionState second) { int total = 0; foreach (IdeologyAxis axis in Enum.GetValues<IdeologyAxis>()) total += 1000 - Math.Abs(first.Ideology[axis] - second.Ideology[axis]) / 2; return Math.Clamp(total / Enum.GetValues<IdeologyAxis>().Length - 500, -1000, 1000); }

    private static void AddLeadership(SocietyWorldState world, params FactionState[] factions)
    {
        foreach (FactionState faction in factions)
        {
            var state = new LeadershipState(faction.Id, faction.Plan.Leadership); state.TryInstallLeader(StableIdFactory.CreateChild(faction.Id, "leader", "founding"), 0); int ordinal = 0;
            foreach (string tag in faction.Plan.InternalDivisionTags.Order(StringComparer.Ordinal)) { StableId id = StableIdFactory.CreateChild(faction.Id, "division", tag); state.UpsertDivision(new InternalDivisionState(id, faction.Id, tag, StableIdFactory.CreateChild(id, "leader", 0), 250 + ordinal * 90, 650 - ordinal * 60, faction.Plan.Objectives[ordinal % faction.Plan.Objectives.Count].Id)); ordinal++; }
            world.AddLeadership(state);
        }
    }
    private static void AddTerritory(SocietyWorldState world, params SettlementState[] settlements)
    {
        foreach (SettlementState settlement in settlements)
        {
            world.AddTerritoryClaim(new TerritoryClaimState(StableIdFactory.CreateChild(settlement.Id, "territory", "control"), settlement.Id, settlement.ControllingFactionId, TerritoryAuthorityKind.Control, 850, settlement.Id, 0));
            world.AddTerritoryClaim(new TerritoryClaimState(StableIdFactory.CreateChild(settlement.Id, "territory", "influence"), StableIdFactory.CreateChild(settlement.Id, "region", "surrounding"), settlement.ControllingFactionId, TerritoryAuthorityKind.Influence, 600, settlement.Id, 0));
        }
    }
    private static void AddTradeRoutes(SocietyWorldState world, SettlementState prospect, SettlementState blackwater, SettlementState quarry)
    {
        world.AddTradeRoute(new TradeRouteRuntimeState(StableIdFactory.Create("trade-route", "prospect-quarry-metals"), quarry.Id, prospect.Id, SocietyCommodity.Metals, 18_000, 2, 140));
        world.AddTradeRoute(new TradeRouteRuntimeState(StableIdFactory.Create("trade-route", "prospect-blackwater-medicine"), prospect.Id, blackwater.Id, SocietyCommodity.Medicine, 9_000, 2, 280));
        world.AddTradeRoute(new TradeRouteRuntimeState(StableIdFactory.Create("trade-route", "blackwater-prospect-chemicals"), blackwater.Id, prospect.Id, SocietyCommodity.Chemicals, 11_000, 2, 250));
        world.AddTradeRoute(new TradeRouteRuntimeState(StableIdFactory.Create("trade-route", "blackwater-quarry-fuel"), blackwater.Id, quarry.Id, SocietyCommodity.Fuel, 10_000, 3, 220));
        world.AddTradeRoute(new TradeRouteRuntimeState(StableIdFactory.Create("trade-route", "quarry-blackwater-metals"), quarry.Id, blackwater.Id, SocietyCommodity.Metals, 15_000, 3, 190));
    }

    private static int Pick(CampaignSeed seed, string domain, int count) { var writer = new CanonicalWriter(); writer.WriteString("roentgen.phase15.pick"); writer.WriteInt32(GeneratorVersion); writer.WriteUInt64(seed.Value); writer.WriteString(domain); return (int)(writer.ComputeChecksum().A % (ulong)Math.Max(1, count)); }
}
