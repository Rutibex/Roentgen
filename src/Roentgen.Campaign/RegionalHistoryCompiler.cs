using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Generation;
using Roentgen.Narrative;
using Roentgen.World;

namespace Roentgen.Campaign;

public sealed class RegionalHistoryCompiler
{
    public const int GeneratorVersion = 17;

    private static readonly IReadOnlyDictionary<RegionFamily, (string PreCollapse, string Present, string[] Pressures, string[] Secrets)> RegionProfiles =
        new Dictionary<RegionFamily, (string, string, string[], string[])>
        {
            [RegionFamily.GlassBasin] = ("radiographic materials proving ground and structural-inspection district", "fused basin of exposed foundations, mirage fields, and buried inspection rails", ["water-scarcity", "heat", "exposed-foundations"], ["subsurface-catalog", "remote-inspection-array"]),
            [RegionFamily.OssuaryFields] = ("medical disposal corridor, tissue archive, and automated sterilization reserve", "pale forests rooted in eroded medical-industrial waste", ["biological-contamination", "medicine-scarcity", "migration"], ["archived-patient-identities", "sterilization-failure"]),
            [RegionFamily.BlackwaterWorks] = ("regional treatment works and chemical diagnostic separation complex", "flooded treatment works, pump settlements, and contaminated marsh channels", ["failing-pumps", "chemical-contamination", "food-shortage"], ["quarantine-directive", "cargo-redirection"]),
            [RegionFamily.BrokenMeridian] = ("high-capacity freight, passenger, and diagnostic transport corridor", "collapsed urban strip whose surviving relays still issue obsolete routing orders", ["route-failure", "banditry", "machine-directives"], ["sealed-manifest", "automated-selection"]),
            [RegionFamily.RedQuarryCountry] = ("mineral extraction and deep structural tomography district", "quarry settlements surrounding unstable cuts and dormant excavation machinery", ["fuel-shortage", "labor-conflict", "slope-instability"], ["buried-installation", "forbidden-depth"]),
            [RegionFamily.QuietRange] = ("observatory, atmospheric survey, and long-range radiographic relay network", "isolated highland towers above buried command installations", ["isolation", "storm-damage", "signal-loss"], ["central-command-record", "false-sky-survey"])
        };

    public RegionalHistoryPlan Compile(CampaignSeed campaignSeed, SocietyWorldState society, RegionalTopology? topology, DomainSeed seed)
    {
        ArgumentNullException.ThrowIfNull(society);
        StableId campaignId = StableIdFactory.CreateCampaignRoot(campaignSeed);
        StableId historyId = StableIdFactory.CreateChild(campaignId, "regional-history", GeneratorVersion);
        FactionState[] factions = society.Factions.OrderBy(static value => value.Id).ToArray();
        SettlementState[] settlements = society.Settlements.OrderBy(static value => value.Id).ToArray();
        var regions = new List<RegionalHistoryRecord>();
        var events = new List<HistoricalEventRecord>();
        var validation = new List<string>();
        var repairs = new List<string>();

        if (factions.Length < 2)
        {
            repairs.Add("history.synthetic-faction-participants: insufficient society factions; deterministic historical witnesses were synthesized.");
        }

        foreach (RegionFamily region in Enum.GetValues<RegionFamily>())
        {
            DomainSeed regionSeed = GenerationDomain.Derive(seed, $"region.{region.ToString().ToLowerInvariant()}");
            Pcg32Random random = RandomStreamFactory.Create(regionSeed, "history-events");
            StableId regionId = StableIdFactory.CreateChild(historyId, "region", region.ToString());
            StableId locationId = ResolveLocation(regionId, region, topology);
            StableId factionA = ResolveParticipant(factions, historyId, region, 0);
            StableId factionB = ResolveParticipant(factions, historyId, region, 1);
            StableId settlement = ResolveSettlement(settlements, regionId, region);
            var eventIds = new List<StableId>();
            (string preCollapse, string present, string[] pressures, string[] secrets) = RegionProfiles[region];

            HistoricalEventRecord infrastructure = CreateEvent(
                regionId, "infrastructure", HistoricalEventKind.PreCollapseInfrastructure, region, 100UL + (ulong)((int)region * 15), locationId,
                [settlement], [],
                $"The region operated as a {preCollapse}.", 920,
                ["pre-collapse", "infrastructure", RegionToken(region)],
                [new HistoricalFactDefinition("history.region.pre-collapse-purpose", regionId, NarrativeValue.FromToken(preCollapse), FactTruthState.True, FactScope.Region)]);
            Add(infrastructure);

            StableId exposureEvidence = StableIdFactory.CreateChild(regionId, "evidence", "collapse-exposure-log");
            HistoricalEventRecord exposure = CreateEvent(
                regionId, "collapse-exposure", HistoricalEventKind.CollapseExposure, region, 900UL + (ulong)random.NextInt32(0, 80), locationId,
                [factionA], [infrastructure.Id],
                $"During the collapse, inspection systems exposed and catalogued matter across {region}, then continued without human authority.", 380,
                ["collapse", "radiographic", "cataloguing", secrets[0]],
                [
                    new HistoricalFactDefinition("history.region.exposure-occurred", regionId, NarrativeValue.FromBoolean(true), FactTruthState.True, FactScope.Region),
                    new HistoricalFactDefinition("history.region.exposure-record", regionId, NarrativeValue.FromStableId(exposureEvidence), FactTruthState.True, FactScope.Region)
                ],
                [exposureEvidence]);
            Add(exposure);

            HistoricalEventKind aftermathKind = region is RegionFamily.BlackwaterWorks or RegionFamily.BrokenMeridian ? HistoricalEventKind.Repurposing : HistoricalEventKind.Abandonment;
            HistoricalEventRecord aftermath = CreateEvent(
                regionId, "aftermath", aftermathKind, region, 1040UL + (ulong)random.NextInt32(0, 120), locationId,
                [factionA, settlement], [exposure.Id],
                $"Survivors {(aftermathKind == HistoricalEventKind.Repurposing ? "repurposed" : "abandoned") } the surviving systems while preserving only fragments of their operating logic.", 700,
                ["aftermath", "survival", pressures[0]],
                [new HistoricalFactDefinition("history.region.present-identity", regionId, NarrativeValue.FromToken(present), FactTruthState.True, FactScope.Region)]);
            Add(aftermath);

            HistoricalEventRecord founding = CreateEvent(
                regionId, "faction-founding", HistoricalEventKind.FactionFounded, region, 1180UL + (ulong)random.NextInt32(0, 140), settlement,
                [factionA, factionB], [aftermath.Id],
                $"Competing survivor groups established control claims around {present}, defining access by what they would diagnose, repair, conceal, or amputate.", 820,
                ["faction-founding", "territory", "resource-dependency"],
                [
                    new HistoricalFactDefinition("history.faction-founded-in-region", factionA, NarrativeValue.FromStableId(regionId), FactTruthState.True, FactScope.Faction),
                    new HistoricalFactDefinition("history.region.contested-by", regionId, NarrativeValue.FromStableIds([factionA, factionB]), FactTruthState.True, FactScope.Region)
                ]);
            Add(founding);

            HistoricalEventKind crisisKind = region switch
            {
                RegionFamily.GlassBasin => HistoricalEventKind.ResourceFailure,
                RegionFamily.OssuaryFields => HistoricalEventKind.Discovery,
                RegionFamily.BlackwaterWorks => HistoricalEventKind.Concealment,
                RegionFamily.BrokenMeridian => HistoricalEventKind.ResourceFailure,
                RegionFamily.RedQuarryCountry => HistoricalEventKind.Conflict,
                RegionFamily.QuietRange => HistoricalEventKind.Discovery,
                _ => HistoricalEventKind.ResourceFailure
            };
            StableId crisisEvidence = StableIdFactory.CreateChild(regionId, "evidence", "current-crisis-record");
            HistoricalEventRecord crisis = CreateEvent(
                regionId, "current-crisis", crisisKind, region, 1520UL + (ulong)random.NextInt32(0, 160), locationId,
                [factionA, factionB, settlement], [founding.Id, exposure.Id],
                $"A present crisis links {pressures[0]} to evidence of {secrets[1]}, but the controlling groups disagree over whether to expose, repair, exploit, or suppress it.", 250,
                ["current-crisis", pressures[0], secrets[1], "campaign-hook"],
                [
                    new HistoricalFactDefinition("history.region.current-crisis", regionId, NarrativeValue.FromToken(pressures[0]), FactTruthState.True, FactScope.Region),
                    new HistoricalFactDefinition("history.region.hidden-cause", regionId, NarrativeValue.FromToken(secrets[1]), FactTruthState.True, FactScope.Quest),
                    new HistoricalFactDefinition("history.region.crisis-location", regionId, NarrativeValue.FromLocation(locationId), FactTruthState.True, FactScope.Region)
                ],
                [crisisEvidence]);
            Add(crisis);

            regions.Add(new RegionalHistoryRecord(regionId, region, preCollapse, present, locationId, eventIds, new SortedSet<string>(pressures, StringComparer.Ordinal), new SortedSet<string>(secrets, StringComparer.Ordinal)));

            void Add(HistoricalEventRecord historicalEvent)
            {
                events.Add(historicalEvent);
                eventIds.Add(historicalEvent.Id);
            }
        }

        AddCrossRegionalEvents(historyId, factions, regions, events, seed);
        validation.Add($"history.regions:{regions.Count}");
        validation.Add($"history.events:{events.Count}");
        validation.Add("history.all-regions-have-causal-chain");
        validation.Add("history.current-crises-reference-physical-evidence");
        return new RegionalHistoryPlan(historyId, campaignSeed, regions, events, validation, repairs, GeneratorVersion);
    }

    private static HistoricalEventRecord CreateEvent(
        StableId regionId,
        string key,
        HistoricalEventKind kind,
        RegionFamily region,
        ulong day,
        StableId locationId,
        IReadOnlyList<StableId> participants,
        IReadOnlyList<StableId> causes,
        string summary,
        int visibility,
        IReadOnlyList<string> tags,
        IReadOnlyList<HistoricalFactDefinition> facts,
        IReadOnlyList<StableId>? evidence = null)
    {
        StableId id = StableIdFactory.CreateChild(regionId, "historical-event", key);
        IReadOnlyList<StableId> evidenceIds = evidence ?? [StableIdFactory.CreateChild(id, "evidence", "primary")];
        return new HistoricalEventRecord(id, kind, region, day, locationId, participants, causes, summary, Math.Clamp(visibility, 0, 1000), evidenceIds, new SortedSet<string>(tags, StringComparer.Ordinal), facts);
    }

    private static void AddCrossRegionalEvents(StableId historyId, IReadOnlyList<FactionState> factions, IReadOnlyList<RegionalHistoryRecord> regions, ICollection<HistoricalEventRecord> events, DomainSeed seed)
    {
        Pcg32Random random = RandomStreamFactory.Create(GenerationDomain.Derive(seed, "cross-region"), "conflict-history");
        StableId firstFaction = ResolveParticipant(factions, historyId, RegionFamily.BrokenMeridian, 0);
        StableId secondFaction = ResolveParticipant(factions, historyId, RegionFamily.RedQuarryCountry, 1);
        RegionalHistoryRecord meridian = regions.Single(static value => value.Region == RegionFamily.BrokenMeridian);
        RegionalHistoryRecord quarry = regions.Single(static value => value.Region == RegionFamily.RedQuarryCountry);
        StableId conflictId = StableIdFactory.CreateChild(historyId, "historical-event", "meridian-quarry-route-war");
        events.Add(new HistoricalEventRecord(
            conflictId,
            HistoricalEventKind.Conflict,
            RegionFamily.BrokenMeridian,
            1330UL + (ulong)random.NextInt32(0, 80),
            meridian.RepresentativeLocationId,
            [firstFaction, secondFaction],
            [meridian.EventIds[^1], quarry.EventIds[^1]],
            "A route war transformed a technical disagreement over freight diagnostics into a durable political grievance.",
            860,
            [StableIdFactory.CreateChild(conflictId, "evidence", "armistice-ledger")],
            new SortedSet<string>(["cross-region", "conflict", "trade-route", "historical-grievance"], StringComparer.Ordinal),
            [new HistoricalFactDefinition("history.factions.route-war", firstFaction, NarrativeValue.FromStableId(secondFaction), FactTruthState.True, FactScope.World)]));

        RegionalHistoryRecord quiet = regions.Single(static value => value.Region == RegionFamily.QuietRange);
        StableId concealmentId = StableIdFactory.CreateChild(historyId, "historical-event", "quiet-range-command-concealment");
        events.Add(new HistoricalEventRecord(
            concealmentId,
            HistoricalEventKind.Concealment,
            RegionFamily.QuietRange,
            1470UL + (ulong)random.NextInt32(0, 60),
            quiet.RepresentativeLocationId,
            [firstFaction, secondFaction],
            [conflictId, quiet.EventIds[1]],
            "Surviving leaders concealed evidence that several regional crises were responses to one still-active command network.",
            120,
            [StableIdFactory.CreateChild(concealmentId, "evidence", "command-fragment")],
            new SortedSet<string>(["cross-region", "concealment", "convergent-cause", "central-command"], StringComparer.Ordinal),
            [new HistoricalFactDefinition("history.world.shared-hidden-cause", historyId, NarrativeValue.FromStableId(concealmentId), FactTruthState.True, FactScope.World)]));
    }

    private static StableId ResolveLocation(StableId regionId, RegionFamily region, RegionalTopology? topology)
    {
        if (topology is not null)
        {
            LandmarkReservation? reservation = topology.Reservations
                .Where(value => topology.RegionAtWorld(value.Center.XCentimeters, value.Center.ZCentimeters) == region)
                .OrderBy(static value => value.Id)
                .FirstOrDefault();
            if (reservation is not null) return reservation.Id;
        }
        return StableIdFactory.CreateChild(regionId, "location", "representative-site");
    }

    private static StableId ResolveParticipant(IReadOnlyList<FactionState> factions, StableId historyId, RegionFamily region, int offset)
    {
        if (factions.Count == 0) return StableIdFactory.CreateChild(historyId, "historical-faction", $"{region}:{offset}");
        return factions[((int)region + offset) % factions.Count].Id;
    }

    private static StableId ResolveSettlement(IReadOnlyList<SettlementState> settlements, StableId regionId, RegionFamily region)
    {
        if (settlements.Count == 0) return StableIdFactory.CreateChild(regionId, "historical-settlement", "survivor-enclave");
        return settlements[(int)region % settlements.Count].Id;
    }

    private static string RegionToken(RegionFamily region) => region.ToString().ToLowerInvariant();
}

public sealed class RegionalHistoryProjector
{
    public IReadOnlyList<StableId> Project(RegionalHistoryPlan history, NarrativeWorldState narrative, IEnumerable<StableId> publicKnowers, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(narrative);
        StableId[] knowers = publicKnowers.Order().Distinct().ToArray();
        var projected = new List<StableId>();
        foreach (HistoricalEventRecord historicalEvent in history.Events.OrderBy(static value => value.Day).ThenBy(static value => value.Id))
        {
            foreach (HistoricalFactDefinition definition in historicalEvent.DerivedFacts)
            {
                StableId factId = StableIdFactory.CreateChild(historicalEvent.Id, "fact", $"{definition.PredicateId}:{definition.SubjectId}");
                narrative.UpsertFact(new FactRecord(factId, definition.PredicateId, definition.SubjectId, definition.Object, definition.Truth, definition.Scope, historicalEvent.Id, tick, null));
                projected.Add(factId);
                if (historicalEvent.PublicVisibilityPermille < 500) continue;
                foreach (StableId knower in knowers)
                {
                    StableId claimId = StableIdFactory.CreateChild(factId, "history-knowledge", knower.ToString());
                    narrative.AddKnowledge(new KnowledgeClaim(
                        claimId,
                        knower,
                        factId,
                        definition.Truth,
                        historicalEvent.PublicVisibilityPermille,
                        new KnowledgeSource(historicalEvent.Id, KnowledgeSourceKind.WorldGeneration, knower, historicalEvent.Summary, tick),
                        historicalEvent.PublicVisibilityPermille,
                        tick,
                        tick,
                        historicalEvent.PublicVisibilityPermille >= 800 ? KnowledgeSecrecy.Public : KnowledgeSecrecy.FactionInternal,
                        false,
                        true));
                }
            }
        }
        return projected;
    }
}
