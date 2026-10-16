using Roentgen.AI;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Items;
using Roentgen.Navigation;
using Roentgen.Persistence;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Phase14.Tests;

public sealed class Phase14SystemTests
{
    [Fact]
    public void Outdoor_routes_are_deterministic_and_reach_the_requested_endpoint()
    {
        OutdoorNavigationGraph graph = CreateLineGraph();
        StableId requestId = StableIdFactory.Create("test", "route");
        var request = new NavigationRequest(requestId, StableIdFactory.Create("test", "agent"), NavigationLocation.Outdoor(new WorldPoint(-50, 0)), NavigationLocation.Outdoor(new WorldPoint(250, 0)), NavigationAgentProfile.OrdinaryHumanoid, graph.Revision);
        var finder = new OutdoorRouteFinder();
        NavigationRoute first = finder.Find(graph, request);
        NavigationRoute second = finder.Find(graph, request);
        Assert.True(first.Success);
        Assert.Equal(first.Checksum, second.Checksum);
        Assert.Equal(request.Destination.WorldPosition, first.Steps[^1].Location.WorldPosition);
    }

    [Fact]
    public void Dynamic_obstacles_invalidate_a_required_corridor()
    {
        OutdoorNavigationGraph graph = CreateLineGraph();
        var obstacles = new DynamicNavigationObstacleRegistry();
        obstacles.Upsert(new DynamicNavigationObstacle(StableIdFactory.Create("test", "block"), new WorldRect(90, -10, 110, 10), StableId.Empty, StableId.Empty, true, 0, 1, 0));
        var request = new NavigationRequest(StableIdFactory.Create("test", "blocked-route"), StableIdFactory.Create("test", "agent"), NavigationLocation.Outdoor(new WorldPoint(0, 0)), NavigationLocation.Outdoor(new WorldPoint(200, 0)), NavigationAgentProfile.OrdinaryHumanoid, graph.Revision + obstacles.Revision);
        NavigationRoute route = new OutdoorRouteFinder().Find(graph, request, obstacles);
        Assert.False(route.Success);
        Assert.Equal(NavigationFailureReason.NoRoute, route.Failure);
    }

    [Fact]
    public void Utility_ai_selects_attack_for_a_ready_alerted_combatant()
    {
        StableId actorId = StableIdFactory.Create("test", "attacker");
        var brain = new AgentBrainState(actorId, "rifleman", "hostile");
        brain.Awareness.Confirm(StableIdFactory.Create("test", "target"), new FixedVector3(1000, 0, 0), 900, 1);
        var context = new UtilityContext(10, actorId, AwarenessLevel.Confirmed, 1000, 800, 100, 0, 0, 800, 12_000, 200, true, true, true, false, false, false, false, false);
        UtilityDecision decision = new UtilityAiEvaluator().Select(brain, context);
        Assert.Equal(UtilityActionKind.Attack, decision.Kind);
    }

    [Fact]
    public void Reduced_simulation_preserves_travel_progress_and_arrival()
    {
        StableId actorId = StableIdFactory.Create("test", "traveller");
        NavigationLocation origin = NavigationLocation.Outdoor(new WorldPoint(0, 0));
        NavigationLocation destination = NavigationLocation.Outdoor(new WorldPoint(1000, 0));
        var schedule = new AgentSchedule("workday", [new AgentScheduleEntry("work", 0, 1440, ScheduleActivityKind.Work, StableIdFactory.Create("test", "work"), destination, 1, new HashSet<string>(StringComparer.Ordinal))]);
        var record = new AgentSimulationRecord(actorId, schedule, origin) { Tier = AgentSimulationTier.Reduced };
        var brain = new AgentBrainState(actorId, "worker", "civilian") { Tier = AgentSimulationTier.Reduced };
        var simulation = new ReducedAgentSimulation();
        simulation.Advance(record, brain, 1, 10);
        Assert.True(record.TravelArrivalTick > 1);
        simulation.Advance(record, brain, record.TravelArrivalTick, 10);
        Assert.Equal(destination, record.Location);
        Assert.Equal(0UL, record.TravelArrivalTick);
    }

    [Fact]
    public void Item_world_snapshot_round_trips_with_the_same_checksum()
    {
        Phase13ContentBundle content = Phase13Content.Create();
        var world = new ItemWorldState();
        StableId owner = StableIdFactory.Create("test", "owner");
        StableId itemId = StableIdFactory.Create("test", "item");
        world.EnsureInventory(owner);
        ItemInstanceState item = new ItemFactory(content).CreateSimple(itemId, "material.scrap-steel", ItemLocation.InInventory(owner), 7);
        world.AddItem(item);
        world.GetInventory(owner).Add(itemId);
        byte[] payload = ItemWorldSnapshotSerializer.WriteToBytes(world);
        ItemWorldState restored = ItemWorldSnapshotSerializer.ReadFromBytes(payload);
        Assert.Equal(world.ComputeChecksum(), restored.ComputeChecksum());
        Assert.Equal(7, restored.GetItem(itemId).Quantity);
    }

    [Fact]
    public void Awareness_decay_is_incremental_not_reapplied_from_the_original_evidence_tick()
    {
        var memory = new AgentAwarenessMemory();
        StableId target = StableIdFactory.Create("test", "awareness-target");
        memory.Confirm(target, new FixedVector3(1000, 0, 0), 900, 60);
        memory.Decay(120, 60);
        int afterFirstSecond = memory.ConfidencePermille;
        memory.Decay(121, 60);
        Assert.Equal(afterFirstSecond, memory.ConfidencePermille);
        memory.Decay(180, 60);
        Assert.Equal(afterFirstSecond - 60, memory.ConfidencePermille);
    }

    [Fact]
    public void Item_campaign_snapshot_preserves_recipe_knowledge_and_runtime_selections()
    {
        Phase13ContentBundle content = Phase13Content.Create();
        var world = new ItemWorldState();
        StableId owner = StableIdFactory.Create("test", "campaign-owner");
        StableId weaponId = StableIdFactory.Create("test", "campaign-weapon");
        StableId throwableId = StableIdFactory.Create("test", "campaign-throwable");
        world.EnsureInventory(owner);
        ItemInstanceState weapon = new ItemFactory(content).CreateSimple(weaponId, "material.scrap-steel", ItemLocation.InInventory(owner), 1);
        ItemInstanceState throwable = new ItemFactory(content).CreateSimple(throwableId, "ammo.explosive-charge", ItemLocation.InInventory(owner), 3);
        world.AddItem(weapon); world.GetInventory(owner).Add(weaponId);
        world.AddItem(throwable); world.GetInventory(owner).Add(throwableId);
        var knowledge = new RecipeKnowledgeState();
        knowledge.Learn(new RecipeKnowledgeRecord("assemble-light-cartridges", RecipeKnowledgeSource.Background, "test", 7));
        byte[] payload = ItemCampaignSnapshotSerializer.WriteToBytes(new ItemCampaignSnapshot(world, knowledge, weaponId, throwableId));
        ItemCampaignSnapshot restored = ItemCampaignSnapshotSerializer.ReadFromBytes(payload);
        Assert.Equal(world.ComputeChecksum(), restored.World.ComputeChecksum());
        Assert.True(restored.RecipeKnowledge.Knows("assemble-light-cartridges"));
        Assert.Equal(weaponId, restored.ActiveWeaponId);
        Assert.Equal(throwableId, restored.ThrowableStackId);
    }

    [Fact]
    public void Reduced_needs_advance_only_at_the_bounded_cadence()
    {
        StableId actorId = StableIdFactory.Create("test", "needs-agent");
        NavigationLocation location = NavigationLocation.Outdoor(new WorldPoint(0, 0));
        var schedule = new AgentSchedule("idle", [new AgentScheduleEntry("idle", 0, 1440, ScheduleActivityKind.Rest, actorId, location, 1, new HashSet<string>(StringComparer.Ordinal))]);
        var record = new AgentSimulationRecord(actorId, schedule, location) { Tier = AgentSimulationTier.Reduced };
        var brain = new AgentBrainState(actorId, "worker", "civilian") { Tier = AgentSimulationTier.Reduced };
        var simulation = new ReducedAgentSimulation();
        simulation.Advance(record, brain, 1, 0);
        int initialFatigue = record.FatiguePermille;
        simulation.Advance(record, brain, 299, 0);
        Assert.Equal(initialFatigue, record.FatiguePermille);
        simulation.Advance(record, brain, 301, 0);
        Assert.True(record.FatiguePermille <= initialFatigue);
    }

    [Fact]
    public void Companion_command_service_replaces_and_completes_active_orders()
    {
        StableId companion = StableIdFactory.Create("test", "companion");
        StableId issuer = StableIdFactory.Create("test", "issuer");
        var brains = new AgentBrainRegistry();
        brains.Add(new AgentBrainState(companion, "companion", "friendly"));
        var service = new CompanionCommandService();
        var first = new CompanionCommand(StableIdFactory.Create("test-command", "follow"), companion, issuer, CompanionCommandKind.Follow, StableId.Empty, FixedVector3.Zero, 1, 100, "test");
        Assert.Equal(CompanionCommandStatus.Accepted, service.Issue(first, brains, 1).Status);
        Assert.True(service.TryGet(companion, out _));
        Assert.Equal(CompanionCommandStatus.Completed, service.Complete(companion, 2).Status);
        Assert.False(service.TryGet(companion, out _));
    }


    [Fact]
    public void Interior_routes_emit_authoritative_door_actions_and_follow_dynamic_door_state()
    {
        StableId structureId = StableIdFactory.Create("test-structure", "door-route");
        StableId leftId = StableIdFactory.CreateChild(structureId, "cell", "left");
        StableId rightId = StableIdFactory.CreateChild(structureId, "cell", "right");
        StableId doorId = StableIdFactory.CreateChild(structureId, "door", "middle");
        var left = new NavigationCell(leftId, StableIdFactory.CreateChild(structureId, "room", "left"), 0, new StructureRect(0, 0, 500, 500), AccessClass.Public, false);
        var right = new NavigationCell(rightId, StableIdFactory.CreateChild(structureId, "room", "right"), 0, new StructureRect(500, 0, 1000, 500), AccessClass.Public, false);
        var portal = new NavigationPortal(StableIdFactory.CreateChild(structureId, "portal", "middle"), doorId, leftId, rightId, false, DoorLifecycleState.Closed, LockPolicy.None, 120);
        var data = new InteriorNavigationData(structureId, [left, right], [portal], [], [], [], []);
        var placement = new StructureNavigationPlacement(structureId, new WorldPoint(0, 0), 0);
        var request = new NavigationRequest(
            StableIdFactory.Create("test", "interior-door-route"),
            StableIdFactory.Create("test", "interior-agent"),
            NavigationLocation.Interior(structureId, leftId, new WorldPoint(250, 250), 0),
            NavigationLocation.Interior(structureId, rightId, new WorldPoint(750, 250), 0),
            NavigationAgentProfile.OrdinaryHumanoid,
            1);
        var obstacles = new DynamicNavigationObstacleRegistry();
        var service = new InteriorNavigationRouteService();

        NavigationRoute closedRoute = service.Find(data, placement, request, obstacles);
        Assert.True(closedRoute.Success);
        Assert.Contains(closedRoute.Steps, step => step.Kind == NavigationStepKind.OpenDoor && step.InteractionTargetId == doorId);

        obstacles.UpsertDoor(new DynamicNavigationDoorState(doorId, DoorLifecycleState.Open, 1000, 1));
        NavigationRoute openRoute = service.Find(data, placement, request with { NavigationRevision = obstacles.Revision }, obstacles);
        Assert.True(openRoute.Success);
        Assert.DoesNotContain(openRoute.Steps, step => step.Kind is NavigationStepKind.OpenDoor or NavigationStepKind.UnlockDoor or NavigationStepKind.ForceDoor);
        Assert.Equal(request.Destination, openRoute.Steps[^1].Location);
    }

    [Fact]
    public void Strategic_simulation_resolves_abstract_pressure_and_recovery_deterministically()
    {
        StableId actorId = StableIdFactory.Create("test", "strategic-agent");
        NavigationLocation location = NavigationLocation.Outdoor(new WorldPoint(0, 0));
        var schedule = new AgentSchedule("strategic", [new AgentScheduleEntry("rest", 0, 1440, ScheduleActivityKind.Rest, actorId, location, 1, new HashSet<string>(StringComparer.Ordinal))]);
        var record = new AgentSimulationRecord(actorId, schedule, location) { Tier = AgentSimulationTier.Strategic, InAbstractCombat = true, HealthPermille = 700 };
        var brain = new AgentBrainState(actorId, "scout", "test") { Tier = AgentSimulationTier.Strategic };
        var simulation = new StrategicAgentSimulation();
        simulation.Advance(record, brain, 1, 0);
        simulation.Advance(record, brain, 601, 0);
        Assert.True(record.HealthPermille < 700);
        Assert.True(brain.FearPermille > 0);

        record.InAbstractCombat = false;
        int damaged = record.HealthPermille;
        simulation.Advance(record, brain, 1201, 0);
        Assert.True(record.HealthPermille > damaged);
    }

    private static OutdoorNavigationGraph CreateLineGraph()
    {
        StableId a = StableIdFactory.Create("test-node", "a");
        StableId b = StableIdFactory.Create("test-node", "b");
        StableId c = StableIdFactory.Create("test-node", "c");
        OutdoorNavigationNode[] nodes =
        [
            new(a, new WorldPoint(0, 0), 0, TerrainSurfaceKind.Road, 0, true, new TerrainChunkAddress(0, 0)),
            new(b, new WorldPoint(100, 0), 0, TerrainSurfaceKind.Road, 0, true, new TerrainChunkAddress(0, 0)),
            new(c, new WorldPoint(200, 0), 0, TerrainSurfaceKind.Road, 0, true, new TerrainChunkAddress(0, 0))
        ];
        OutdoorNavigationEdge[] edges =
        [
            new(StableIdFactory.Create("test-edge", "ab"), a, b, 100, NavigationStepKind.Road),
            new(StableIdFactory.Create("test-edge", "bc"), b, c, 100, NavigationStepKind.Road)
        ];
        return new OutdoorNavigationGraph(nodes, edges);
    }
}
