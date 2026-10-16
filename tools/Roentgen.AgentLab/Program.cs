using Roentgen.AI;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Navigation;
using Roentgen.World;
using Roentgen.World.Structures;

string command = args.FirstOrDefault() ?? "smoke";
switch (command)
{
    case "route": RouteAudit(ParseCount(args, 1, 10_000)); break;
    case "utility": UtilityAudit(ParseCount(args, 1, 10_000)); break;
    case "schedule": ScheduleAudit(ParseCount(args, 1, 14_400)); break;
    case "door": DoorAudit(); break;
    case "strategic": StrategicAudit(ParseCount(args, 1, 14_400)); break;
    case "smoke": RouteAudit(1_000); UtilityAudit(1_000); ScheduleAudit(1_440); DoorAudit(); StrategicAudit(1_440); break;
    default:
        Console.WriteLine("Roentgen AgentLab — Phase 14");
        Console.WriteLine("Commands: smoke, route [count], utility [count], schedule [ticks], door, strategic [ticks]");
        break;
}

void RouteAudit(int count)
{
    OutdoorNavigationGraph graph = BuildGrid(24);
    var finder = new OutdoorRouteFinder();
    var writer = new CanonicalWriter();
    int failures = 0;
    for (int index = 0; index < count; index++)
    {
        int sx = index % 24; int sz = index / 24 % 24; int gx = (index * 7 + 3) % 24; int gz = (index * 11 + 5) % 24;
        var request = new NavigationRequest(StableIdFactory.Create("agentlab-route", index.ToString()), StableIdFactory.Create("agentlab-agent", (index % 64).ToString()), NavigationLocation.Outdoor(new WorldPoint(sx * 1000, sz * 1000)), NavigationLocation.Outdoor(new WorldPoint(gx * 1000, gz * 1000)), NavigationAgentProfile.OrdinaryHumanoid, graph.Revision);
        NavigationRoute route = finder.Find(graph, request);
        if (!route.Success) failures++;
        writer.WriteChecksum(route.Checksum);
    }
    Console.WriteLine($"route requests {count}; failures {failures}; checksum {writer.ComputeChecksum()}");
}

void UtilityAudit(int count)
{
    var evaluator = new UtilityAiEvaluator(); var writer = new CanonicalWriter();
    for (int index = 0; index < count; index++)
    {
        StableId id = StableIdFactory.Create("agentlab-brain", index.ToString());
        var brain = new AgentBrainState(id, "audit", "audit");
        AwarenessLevel awareness = (AwarenessLevel)(index % 5);
        var context = new UtilityContext((ulong)index, id, awareness, 150 + index % 851, 100 + index * 13 % 901, index * 17 % 1001, index * 19 % 1001, index * 23 % 1001, 700, 5_000 + index % 40_000, index * 29 % 1001, index % 4 != 0, true, true, index % 7 == 0, false, false, false, false);
        UtilityDecision decision = evaluator.Select(brain, context);
        writer.WriteString(decision.ActionId); writer.WriteInt32(decision.Score);
    }
    Console.WriteLine($"utility decisions {count}; checksum {writer.ComputeChecksum()}");
}

void ScheduleAudit(int ticks)
{
    StableId id = StableIdFactory.Create("agentlab", "scheduled-agent");
    NavigationLocation home = NavigationLocation.Outdoor(new WorldPoint(0, 0));
    NavigationLocation work = NavigationLocation.Outdoor(new WorldPoint(40_000, 10_000));
    var none = new HashSet<string>(StringComparer.Ordinal);
    var schedule = new AgentSchedule("audit-day", [new AgentScheduleEntry("work", 0, 720, ScheduleActivityKind.Work, id, work, 1, none), new AgentScheduleEntry("rest", 720, 720, ScheduleActivityKind.Rest, id, home, 1, none)]);
    var record = new AgentSimulationRecord(id, schedule, home) { Tier = AgentSimulationTier.Reduced };
    var brain = new AgentBrainState(id, "worker", "audit") { Tier = AgentSimulationTier.Reduced };
    var simulation = new ReducedAgentSimulation();
    for (ulong tick = 1; tick <= (ulong)ticks; tick++) simulation.Advance(record, brain, tick, (int)(tick / 10 % 1440));
    var writer = new CanonicalWriter(); record.WriteCanonical(writer); brain.WriteCanonical(writer);
    Console.WriteLine($"schedule ticks {ticks}; location {record.Location.WorldPosition}; activity {record.CurrentActivity}; checksum {writer.ComputeChecksum()}");
}


void DoorAudit()
{
    StableId structureId = StableIdFactory.Create("agentlab-structure", "door-audit");
    StableId leftId = StableIdFactory.CreateChild(structureId, "cell", "left");
    StableId rightId = StableIdFactory.CreateChild(structureId, "cell", "right");
    StableId doorId = StableIdFactory.CreateChild(structureId, "door", "middle");
    var left = new NavigationCell(leftId, StableIdFactory.CreateChild(structureId, "room", "left"), 0, new StructureRect(0, 0, 500, 500), AccessClass.Public, false);
    var right = new NavigationCell(rightId, StableIdFactory.CreateChild(structureId, "room", "right"), 0, new StructureRect(500, 0, 1000, 500), AccessClass.Public, false);
    var portal = new NavigationPortal(StableIdFactory.CreateChild(structureId, "portal", "middle"), doorId, leftId, rightId, false, DoorLifecycleState.Closed, LockPolicy.None, 120);
    var data = new InteriorNavigationData(structureId, [left, right], [portal], [], [], [], []);
    var placement = new StructureNavigationPlacement(structureId, new WorldPoint(0, 0), 0);
    var request = new NavigationRequest(StableIdFactory.Create("agentlab", "door-route"), StableIdFactory.Create("agentlab", "door-agent"), NavigationLocation.Interior(structureId, leftId, new WorldPoint(250, 250), 0), NavigationLocation.Interior(structureId, rightId, new WorldPoint(750, 250), 0), NavigationAgentProfile.OrdinaryHumanoid, 1);
    var dynamicState = new DynamicNavigationObstacleRegistry();
    var service = new InteriorNavigationRouteService();
    NavigationRoute closed = service.Find(data, placement, request, dynamicState);
    dynamicState.UpsertDoor(new DynamicNavigationDoorState(doorId, DoorLifecycleState.Open, 1000, 1));
    NavigationRoute open = service.Find(data, placement, request with { NavigationRevision = dynamicState.Revision }, dynamicState);
    Console.WriteLine($"door route closed-actions {closed.Steps.Count(step => !step.InteractionTargetId.IsEmpty)}; open-actions {open.Steps.Count(step => !step.InteractionTargetId.IsEmpty)}; checksums {closed.Checksum}/{open.Checksum}");
}

void StrategicAudit(int ticks)
{
    StableId id = StableIdFactory.Create("agentlab", "strategic-agent");
    NavigationLocation home = NavigationLocation.Outdoor(new WorldPoint(0, 0));
    var schedule = new AgentSchedule("strategic-day", [new AgentScheduleEntry("rest", 0, 1440, ScheduleActivityKind.Rest, id, home, 1, new HashSet<string>(StringComparer.Ordinal))]);
    var record = new AgentSimulationRecord(id, schedule, home) { Tier = AgentSimulationTier.Strategic, InAbstractCombat = true };
    var brain = new AgentBrainState(id, "strategic", "audit") { Tier = AgentSimulationTier.Strategic };
    var simulation = new StrategicAgentSimulation();
    for (ulong tick = 1; tick <= (ulong)ticks; tick++) simulation.Advance(record, brain, tick, (int)(tick / 10 % 1440));
    var writer = new CanonicalWriter(); record.WriteCanonical(writer); brain.WriteCanonical(writer);
    Console.WriteLine($"strategic ticks {ticks}; health {record.HealthPermille}; fear {brain.FearPermille}; abstract-combat {record.InAbstractCombat}; checksum {writer.ComputeChecksum()}");
}

OutdoorNavigationGraph BuildGrid(int size)
{
    var nodes = new List<OutdoorNavigationNode>(); var edges = new List<OutdoorNavigationEdge>(); var ids = new StableId[size, size];
    for (int z = 0; z < size; z++) for (int x = 0; x < size; x++) { StableId id = StableIdFactory.Create("agentlab-node", $"{x}:{z}"); ids[x,z] = id; nodes.Add(new OutdoorNavigationNode(id, new WorldPoint(x * 1000, z * 1000), 0, TerrainSurfaceKind.Road, 0, true, new TerrainChunkAddress(x / 8, z / 8))); }
    for (int z = 0; z < size; z++) for (int x = 0; x < size; x++) { if (x + 1 < size) edges.Add(new OutdoorNavigationEdge(StableIdFactory.Create("agentlab-edge", $"{x}:{z}:x"), ids[x,z], ids[x+1,z], 1000, NavigationStepKind.Road)); if (z + 1 < size) edges.Add(new OutdoorNavigationEdge(StableIdFactory.Create("agentlab-edge", $"{x}:{z}:z"), ids[x,z], ids[x,z+1], 1000, NavigationStepKind.Road)); }
    return new OutdoorNavigationGraph(nodes, edges);
}

static int ParseCount(string[] values, int index, int fallback) => values.Length > index && int.TryParse(values[index], out int count) ? Math.Clamp(count, 1, 1_000_000) : fallback;
