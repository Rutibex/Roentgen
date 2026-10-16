using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Persistence;

string command = args.FirstOrDefault() ?? "smoke"; ulong seedValue = ParseUlong(args, "--seed", 0x524F454E5447454EUL); var seed = new CampaignSeed(seedValue); StableId playerId = StableIdFactory.Create("factionlab", "player"); SocietyWorldState world = Phase15Content.CreateRegressionWorld(seed, playerId);
switch (command)
{
    case "generate": PrintWorld(world); break;
    case "inspect-faction": InspectFaction(world, args.Skip(1).FirstOrDefault()); break;
    case "inspect-settlement": InspectSettlement(world, args.Skip(1).FirstOrDefault()); break;
    case "simulate-days": Simulate(world, ParseInt(args, 1, 30)); break;
    case "simulate-years": Simulate(world, checked(ParseInt(args, 1, 1) * 365)); break;
    case "kill-leader": KillLeader(world, args.Skip(1).FirstOrDefault()); break;
    case "interrupt-route": InterruptRoute(world, args.Skip(1).FirstOrDefault()); break;
    case "add-shortage": AddShortage(world, args.Skip(1).FirstOrDefault(), args.Skip(2).FirstOrDefault()); break;
    case "start-conflict": StartConflict(world); break;
    case "record-crime": RecordCrime(world, playerId); break;
    case "apply-disguise": ApplyDisguise(world, playerId); break;
    case "inspect-reputation": InspectReputation(world, playerId); break;
    case "validate": Validate(world); break;
    case "repair": Repair(world); break;
    case "export": Export(world, args.Skip(1).FirstOrDefault() ?? "artifacts/factionlab/phase15.rgsoc"); break;
    case "replay": Replay(seed, ParseInt(args, 1, 365)); break;
    case "smoke": PrintWorld(world); Simulate(world, 30); RecordCrime(world, playerId); ApplyDisguise(world, playerId); Validate(world); break;
    default:
        Console.WriteLine("Roentgen FactionLab — Phase 15");
        Console.WriteLine("Commands: smoke, generate, inspect-faction [name|id], inspect-settlement [name|id], simulate-days [n], simulate-years [n], kill-leader [name|id], interrupt-route [id], add-shortage [settlement] [commodity], start-conflict, record-crime, apply-disguise, inspect-reputation, validate, repair, export [path], replay [days]");
        break;
}

static void PrintWorld(SocietyWorldState world)
{
    Console.WriteLine($"society day {world.Day}; factions {world.Factions.Count}; settlements {world.Settlements.Count}; routes {world.TradeRoutes.Count}; checksum {world.ComputeChecksum()}");
    foreach (FactionState faction in world.Factions.OrderBy(static value => value.Id)) Console.WriteLine($"  {faction.Plan.DisplayName} [{faction.Id}] cohesion {faction.CohesionPermille} legitimacy {faction.LegitimacyPermille} settlements {faction.ControlledSettlements.Count}");
    foreach (SettlementState settlement in world.Settlements.OrderBy(static value => value.Id)) Console.WriteLine($"  {settlement.Plan.DisplayName} [{settlement.Id}] pop {settlement.Population.TotalPopulation}/{settlement.Population.HousingCapacity} stability {settlement.StabilityPermille} controller {settlement.ControllingFactionId}");
}

static void InspectFaction(SocietyWorldState world, string? selector)
{
    FactionState faction = ResolveFaction(world, selector); Console.WriteLine($"{faction.Plan.DisplayName} [{faction.Id}]\nFounding: {faction.Plan.FoundingEvent}\nSurvival: {faction.Plan.SurvivalMethod}\nArchitecture: {faction.Plan.ArchitecturalProfileId}\nMotif: {faction.Plan.MusicalMotifDomain}");
    Console.WriteLine("Ideology: " + string.Join(", ", faction.Ideology.Select(static pair => $"{pair.Key}={pair.Value}"))); Console.WriteLine("Needs: " + string.Join(", ", faction.Plan.MaterialNeeds.Select(static value => $"{value.Commodity}:{value.RequiredPerDayMilliunits}"))); Console.WriteLine("Taboos: " + string.Join(", ", faction.Plan.Taboos));
    foreach (FactionRelationshipState relation in world.Relationships.Where(value => value.SourceFactionId == faction.Id).OrderBy(static value => value.TargetFactionId)) Console.WriteLine($"  -> {world.GetFaction(relation.TargetFactionId).Plan.DisplayName}: {relation.DerivedKind} trust {relation.TrustPermille} respect {relation.RespectPermille} reliance {relation.TradeReliancePermille}");
}

static void InspectSettlement(SocietyWorldState world, string? selector)
{
    SettlementState settlement = ResolveSettlement(world, selector); Console.WriteLine($"{settlement.Plan.DisplayName} [{settlement.Id}] topology {settlement.Plan.Topology} controller {world.GetFaction(settlement.ControllingFactionId).Plan.DisplayName}"); Console.WriteLine($"Population {settlement.Population.TotalPopulation}/{settlement.Population.HousingCapacity}, stability {settlement.StabilityPermille}, migration {settlement.Population.MigrationPressurePermille}");
    foreach (ResourceStockState stock in settlement.Stocks.Values.OrderBy(static value => value.Commodity)) Console.WriteLine($"  {stock.Commodity}: {stock.QuantityMilliunits}/{stock.StorageCapacityMilliunits}, scarcity {stock.ScarcityPermille}");
    foreach (SettlementServiceState service in settlement.Services.OrderBy(static value => value.Kind)) Console.WriteLine($"  {service.Kind}: operating {service.Operating}, capacity {service.EffectiveCapacity}, queue {service.QueuePressurePermille}");
}

static void Simulate(SocietyWorldState world, int days)
{
    var engine = new SocietySimulationEngine(new CampaignSeed(0x524F454E5447454EUL)); SocietyTickResult last = default!; for (int index = 0; index < days; index++) last = engine.AdvanceDay(world); Console.WriteLine($"simulated {days} days; day {world.Day}; shipments {world.Shipments.Count}; events {world.Events.Count}; checksum {last.Checksum}"); InspectSettlement(world, "Prospect Clinic");
}

static void KillLeader(SocietyWorldState world, string? selector)
{
    FactionState faction = ResolveFaction(world, selector); var command = new VacateLeadershipCommand(StableIdFactory.Create("factionlab-command", "vacate"), world.Revision, faction.Id); SocietyCommandResult result = new SocietyCommandProcessor().Execute(world, command); Console.WriteLine($"{result.Status}: {result.Message}"); _ = new SocietySimulationEngine(new CampaignSeed(1)).AdvanceDay(world); Console.WriteLine($"new leader {world.GetLeadership(faction.Id).HighestOffice?.HolderActorId}");
}

static void InterruptRoute(SocietyWorldState world, string? selector)
{
    TradeRouteRuntimeState route = world.TradeRoutes.FirstOrDefault(value => selector is null || value.Id.ToString().StartsWith(selector, StringComparison.OrdinalIgnoreCase)) ?? world.TradeRoutes.First(); var command = new SetTradeRouteActiveCommand(StableIdFactory.Create("factionlab-command", "route"), world.Revision, route.Id, false); SocietyCommandResult result = new SocietyCommandProcessor().Execute(world, command); Console.WriteLine($"{result.Status}: {result.Message} {route.Id}");
}

static void AddShortage(SocietyWorldState world, string? settlementSelector, string? commoditySelector)
{
    SettlementState settlement = ResolveSettlement(world, settlementSelector); SocietyCommodity commodity = Enum.TryParse(commoditySelector, true, out SocietyCommodity parsed) ? parsed : SocietyCommodity.Water; ResourceStockState stock = settlement.GetStock(commodity); long removed = stock.Consume(stock.QuantityMilliunits * 9 / 10); Console.WriteLine($"removed {removed} {commodity} from {settlement.Plan.DisplayName}; scarcity {stock.ScarcityPermille}");
}

static void StartConflict(SocietyWorldState world)
{
    var command = new StartConflictCommand(StableIdFactory.Create("factionlab-command", "conflict"), world.Revision, FactionIds.BlackwaterRecoveryAuthority, FactionIds.ProspectClinicCompact, Phase15Content.MeridianRelayLocationId, StrategicConflictStage.Pressure, 750); SocietyCommandResult result = new SocietyCommandProcessor().Execute(world, command); Console.WriteLine($"{result.Status}: {result.Message}; reserved {world.GetConflict(command.Id).ReservedForPlayableEncounter}");
}

static void RecordCrime(SocietyWorldState world, StableId playerId)
{
    StableId witness = StableIdFactory.Create("factionlab", "witness"); var command = new RecordCrimeCommand(StableIdFactory.Create("factionlab-crime", world.Day.ToString()), world.Revision, CrimeKind.FactionImpersonation, FactionIds.BlackwaterRecoveryAuthority, Phase15Content.BlackwaterSettlementId, playerId, 620, [witness], 780); SocietyCommandResult result = new SocietyCommandProcessor().Execute(world, command); Console.WriteLine($"{result.Status}: {result.Message}"); _ = new SocietySimulationEngine(new CampaignSeed(2)).AdvanceDay(world); Console.WriteLine($"crime status {world.GetCrime(command.Id).Status}; standing {world.GetOrCreateReputation(playerId, FactionIds.BlackwaterRecoveryAuthority).DerivedStanding}");
}

static void ApplyDisguise(SocietyWorldState world, StableId playerId)
{
    var disguise = new DisguiseIdentityState(StableIdFactory.Create("factionlab", "disguise"), playerId, FactionIds.BlackwaterRecoveryAuthority, new SortedSet<string>(["recovery-pass"], StringComparer.Ordinal), new SortedSet<string>(["industrial", "sealed"], StringComparer.Ordinal), 720, true); world.AddDisguise(disguise); DisguiseObservationState observation = new DisguiseEvaluator().Evaluate(disguise, StableIdFactory.Create("factionlab", "guard"), FactionIds.Player, 620, 700, 100, world.Day); world.AddDisguiseObservation(observation); Console.WriteLine($"disguise {observation.Evaluation}; believed faction {observation.BelievedFactionId}; confidence {observation.ConfidencePermille}");
}

static void InspectReputation(SocietyWorldState world, StableId playerId) { foreach (FactionState faction in world.Factions) { FactionReputationState state = world.GetOrCreateReputation(playerId, faction.Id); Console.WriteLine($"{faction.Plan.DisplayName}: {state.DerivedStanding}; " + string.Join(", ", state.Values.Select(static pair => $"{pair.Key}={pair.Value}"))); } }
static void Validate(SocietyWorldState world) { SocietyValidationReport report = new SocietyValidator().Validate(world, Phase15Content.Set("medical-site", "water-source", "industrial-site", "machinery", "road", "fuel-source")); Console.WriteLine($"valid {report.IsValid}; issues {report.Issues.Count}; repairs {report.RepairCount}"); foreach (SocietyValidationIssue issue in report.Issues) Console.WriteLine($"  {issue.Code}: {issue.Message}"); }
static void Repair(SocietyWorldState world) { SocietyValidationReport report = new SocietyRepairEngine().Repair(world, Phase15Content.Set("medical-site", "water-source", "industrial-site", "machinery", "road", "fuel-source")); Console.WriteLine($"valid {report.IsValid}; repairs {report.RepairCount}; issues {report.Issues.Count}"); }
static void Export(SocietyWorldState world, string path) { SocietyWorldSnapshotSerializer.WriteAtomic(path, world); Console.WriteLine($"wrote {Path.GetFullPath(path)} ({new FileInfo(path).Length} bytes) checksum {world.ComputeChecksum()}"); }
static void Replay(CampaignSeed seed, int days) { SocietyWorldState first = Phase15Content.CreateRegressionWorld(seed, StableIdFactory.Create("factionlab", "player")); SocietyWorldState second = Phase15Content.CreateRegressionWorld(seed, StableIdFactory.Create("factionlab", "player")); var firstEngine = new SocietySimulationEngine(seed); var secondEngine = new SocietySimulationEngine(seed); for (int index = 0; index < days; index++) { _ = firstEngine.AdvanceDay(first); _ = secondEngine.AdvanceDay(second); } Console.WriteLine($"replay days {days}; equal {first.ComputeChecksum() == second.ComputeChecksum()}; checksum {first.ComputeChecksum()}"); }
static FactionState ResolveFaction(SocietyWorldState world, string? selector) => world.Factions.FirstOrDefault(value => selector is null || value.Id.ToString().StartsWith(selector, StringComparison.OrdinalIgnoreCase) || value.Plan.DisplayName.Contains(selector, StringComparison.OrdinalIgnoreCase)) ?? world.Factions.First();
static SettlementState ResolveSettlement(SocietyWorldState world, string? selector) => world.Settlements.FirstOrDefault(value => selector is null || value.Id.ToString().StartsWith(selector, StringComparison.OrdinalIgnoreCase) || value.Plan.DisplayName.Contains(selector, StringComparison.OrdinalIgnoreCase)) ?? world.Settlements.First();
static int ParseInt(string[] values, int index, int fallback) => values.Length > index && int.TryParse(values[index], out int parsed) ? Math.Clamp(parsed, 1, 36500) : fallback;
static ulong ParseUlong(string[] values, string key, ulong fallback) { int index = Array.IndexOf(values, key); return index >= 0 && values.Length > index + 1 && ulong.TryParse(values[index + 1], out ulong parsed) ? parsed : fallback; }
