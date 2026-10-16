using Roentgen.AI;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Factions;
using Roentgen.Navigation;
using Roentgen.Physics;
using Roentgen.Simulation;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.App;

/// <summary>Owns cross-space navigation, schedules and full/reduced/strategic NPC continuity.</summary>
internal sealed class Phase14WorldAgentRuntime
{
    private readonly Dictionary<StableId, CharacterState> characters = [];
    private readonly Dictionary<StableId, NavigationMotor> motors = [];
    private readonly HashSet<StableId> combatControlled = [];
    private readonly AgentBrainRegistry worldBrains = new();
    private readonly AgentSimulationTierService tierService = new();
    private readonly ReducedAgentSimulation reduced = new();
    private readonly StrategicAgentSimulation strategic = new();
    private readonly PlayerSimulationRuntime playerRuntime;
    private FullAiController? combatAi;
    private readonly NavigationAgentSpatialRegistry spatialRegistry = new();

    public Phase14WorldAgentRuntime(Phase9WorldScene scene, IPlayerCollisionWorld collisionWorld, PlayerSimulationRuntime playerRuntime)
    {
        ArgumentNullException.ThrowIfNull(scene);
        this.playerRuntime = playerRuntime ?? throw new ArgumentNullException(nameof(playerRuntime));
        Navigation = new HierarchicalNavigationService();
        Navigation.SetOutdoorGraph(OutdoorNavigationGraphBuilder.BuildRoadGraph(scene.Topology, NavigationAgentProfile.OrdinaryHumanoid));
        var emitter = new StructureNavigationEmitter();
        foreach (StructureBlueprint structure in scene.Structures.OrderBy(static value => value.Id))
        {
            InteriorNavigationData data = emitter.Emit(structure);
            Navigation.RegisterInterior(data, new StructureNavigationPlacement(
                structure.Id,
                new WorldPoint(structure.WorldOrigin.XCentimeters, structure.WorldOrigin.ZCentimeters),
                structure.WorldOrigin.YCentimeters,
                structure.Grammar.StructuralSystem.FloorHeightCentimeters));
        }

        var collisionMovement = new CollisionNavigationMovementQuery(collisionWorld);
        Movement = new AvoidanceNavigationMovementQuery(collisionMovement, spatialRegistry);
        Interactions = new PlayerNavigationInteractionHandler(playerRuntime);
        SyncDoorNavigationState();
        foreach (Phase9CharacterHandle handle in scene.CharacterHandles.OrderBy(static value => value.State.Id))
        {
            CharacterState character = handle.State;
            characters.Add(character.Id, character);
            NavigationAgentProfile profile = Profile(character);
            spatialRegistry.Register(character, profile);
            motors.Add(character.Id, new NavigationMotor(Navigation, Movement, Interactions));
            StableId faction = ResolveFaction(character.Appearance);
            worldBrains.Add(new AgentBrainState(character.Id, character.Appearance.ArchetypeId, faction));
            NavigationLocation home = ResolveLocation(character.FeetPosition);
            AgentSchedule schedule = BuildSchedule(character.Id, home, character.Appearance.ArchetypeId);
            Records.Add(new AgentSimulationRecord(character.Id, schedule, home));
        }
    }

    public HierarchicalNavigationService Navigation { get; }
    public INavigationMovementQuery Movement { get; }
    public INavigationInteractionHandler Interactions { get; }
    public AgentSimulationRegistry Records { get; } = new();
    public int FullCount => Records.Records.Count(static value => value.Tier == AgentSimulationTier.Full);
    public int ReducedCount => Records.Records.Count(static value => value.Tier == AgentSimulationTier.Reduced);
    public int StrategicCount => Records.Records.Count(static value => value.Tier == AgentSimulationTier.Strategic);

    public void AttachCombatAi(FullAiController controller, IEnumerable<StableId> controlledIds)
    {
        combatAi = controller ?? throw new ArgumentNullException(nameof(controller));
        foreach (StableId id in controlledIds) combatControlled.Add(id);
    }

    public void Update(FixedVector3 playerPosition, ulong tick)
    {
        SyncDoorNavigationState();
        Navigation.Obstacles.Expire(tick);
        int minuteOfDay = checked((int)((tick / 3600UL) % 1440UL));
        foreach (AgentSimulationRecord record in Records.Records.OrderBy(static value => value.AgentId))
        {
            CharacterState character = characters[record.AgentId];
            AgentBrainState brain = worldBrains.Get(record.AgentId);
            AgentSimulationTier previousTier = record.Tier;
            AgentSimulationTier desired = tierService.Resolve(record, playerPosition);
            AgentBrainState? combatBrain = null;
            if (combatAi is not null && combatAi.Brains.TryGet(record.AgentId, out AgentBrainState? foundCombatBrain)) combatBrain = foundCombatBrain;

            if (combatBrain is not null && previousTier == AgentSimulationTier.Full && desired != AgentSimulationTier.Full)
                brain.CopyDynamicStateFrom(combatBrain);

            tierService.Apply(record, brain, character, desired, ResolveLocation);

            if (combatBrain is not null)
            {
                if (previousTier != AgentSimulationTier.Full && desired == AgentSimulationTier.Full)
                    combatBrain.CopyDynamicStateFrom(brain);
                combatBrain.Tier = desired;
                combatBrain.HungerPermille = record.HungerPermille;
                combatBrain.FatiguePermille = record.FatiguePermille;
                combatBrain.Revision++;
            }

            if (desired != AgentSimulationTier.Full)
            {
                if (desired == AgentSimulationTier.Strategic) strategic.Advance(record, brain, tick, minuteOfDay);
                else reduced.Advance(record, brain, tick, minuteOfDay);
                continue;
            }

            record.Location = ResolveLocation(character.FeetPosition);
            AgentScheduleEntry entry = record.Schedule.Resolve(minuteOfDay);
            bool scheduleChanged = !string.Equals(record.CurrentScheduleEntryId, entry.Id, StringComparison.Ordinal);
            if (scheduleChanged)
            {
                record.CurrentScheduleEntryId = entry.Id;
                record.CurrentActivity = entry.Activity;
                record.Revision++;
            }
            if (!combatControlled.Contains(record.AgentId))
            {
                NavigationMotor motor = motors[record.AgentId];
                if (scheduleChanged || previousTier != AgentSimulationTier.Full || !motor.Status.HasRoute)
                    motor.SetDestination(character, entry.Destination, Profile(character), tick, true);
                bool run = entry.Activity is ScheduleActivityKind.Patrol or ScheduleActivityKind.Guard or ScheduleActivityKind.Travel;
                NavigationMotorStatus status = motor.Tick(character, Profile(character), tick, run);
                if (status.ReachedDestination) ApplyStationaryActivity(character, entry.Activity);
            }
            AdvanceFullSimulationNeeds(record, entry.Activity, tick);
            brain.HungerPermille = record.HungerPermille;
            brain.FatiguePermille = record.FatiguePermille;
            brain.Revision++;
        }
    }

    private void SyncDoorNavigationState()
    {
        foreach (DoorRuntimeState door in playerRuntime.Doors.OrderBy(static value => value.Definition.Id))
        {
            DoorLifecycleState effectiveState = door.State == DoorLifecycleState.Open && door.OpenProgressPermille <= 850
                ? DoorLifecycleState.Closed
                : door.State;
            int clearProgress = door.OpenProgressPermille > 850 ? 1000 : 0;
            Navigation.Obstacles.UpsertDoor(new DynamicNavigationDoorState(door.Definition.Id, effectiveState, clearProgress, 0));
        }
    }

    private NavigationLocation ResolveLocation(FixedVector3 value) => Navigation.ResolveLocation(
        new WorldPoint(checked((int)(value.XMillimeters / 10)), checked((int)(value.ZMillimeters / 10))),
        checked((int)(value.YMillimeters / 10)));

    private static void AdvanceFullSimulationNeeds(AgentSimulationRecord record, ScheduleActivityKind activity, ulong tick)
    {
        if (record.LastNeedsTick == 0) record.LastNeedsTick = tick;
        if (tick <= record.LastNeedsTick) return;
        ulong steps = (tick - record.LastNeedsTick) / 300UL;
        if (steps == 0) return;
        int boundedSteps = checked((int)Math.Min(1000UL, steps));
        int hungerDelta = activity == ScheduleActivityKind.Eat ? -20 * boundedSteps : boundedSteps;
        int fatigueDelta = activity is ScheduleActivityKind.Sleep or ScheduleActivityKind.Rest ? -25 * boundedSteps : boundedSteps;
        record.HungerPermille = Math.Clamp(record.HungerPermille + hungerDelta, 0, 1000);
        record.FatiguePermille = Math.Clamp(record.FatiguePermille + fatigueDelta, 0, 1000);
        record.LastNeedsTick = checked(record.LastNeedsTick + steps * 300UL);
        record.Revision++;
    }

    private static NavigationAgentProfile Profile(CharacterState character)
    {
        if (character.Appearance.Species == CharacterSpecies.IndustrialQuadruped) return NavigationAgentProfile.Quadruped;
        if (character.Appearance.Species == CharacterSpecies.RoboticChassis) return NavigationAgentProfile.RoboticChassis;
        string archetype = character.Appearance.ArchetypeId;
        if (archetype.Contains("technician", StringComparison.Ordinal) || archetype.Contains("caretaker", StringComparison.Ordinal)) return NavigationAgentProfile.TechnicalHumanoid;
        if (archetype.Contains("hostile", StringComparison.Ordinal) || archetype.Contains("breacher", StringComparison.Ordinal)) return NavigationAgentProfile.BreacherHumanoid;
        if (archetype.Contains("hazard", StringComparison.Ordinal) || archetype.Contains("sealed", StringComparison.Ordinal)) return NavigationAgentProfile.HazardProtectedHumanoid;
        if (archetype.Contains("small", StringComparison.Ordinal) || archetype.Contains("crawler", StringComparison.Ordinal)) return NavigationAgentProfile.SmallCreature;
        if (archetype.Contains("burrow", StringComparison.Ordinal)) return NavigationAgentProfile.Burrower;
        if (archetype.Contains("heavy", StringComparison.Ordinal)) return NavigationAgentProfile.HeavyHumanoid;
        return NavigationAgentProfile.OrdinaryHumanoid;
    }

    private static AgentSchedule BuildSchedule(StableId agentId, NavigationLocation home, string archetype)
    {
        int sign = (agentId.Low & 1UL) == 0 ? 1 : -1;
        NavigationLocation work = Offset(home, 900 * sign, 650);
        NavigationLocation service = Offset(home, -600 * sign, 850);
        ScheduleActivityKind duty = archetype.Contains("guard", StringComparison.Ordinal) ? ScheduleActivityKind.Guard
            : archetype.Contains("technician", StringComparison.Ordinal) ? ScheduleActivityKind.Repair
            : archetype.Contains("hostile", StringComparison.Ordinal) ? ScheduleActivityKind.Patrol
            : archetype.Contains("scavenger", StringComparison.Ordinal) ? ScheduleActivityKind.Work
            : ScheduleActivityKind.Socialize;
        var none = new HashSet<string>(StringComparer.Ordinal);
        return new AgentSchedule($"schedule.{archetype}",
        [
            new AgentScheduleEntry("sleep", 0, 360, ScheduleActivityKind.Sleep, agentId, home, 10, none),
            new AgentScheduleEntry("morning", 360, 120, ScheduleActivityKind.Eat, agentId, service, 20, none),
            new AgentScheduleEntry("duty-a", 480, 300, duty, agentId, work, 30, none),
            new AgentScheduleEntry("midday", 780, 90, ScheduleActivityKind.Eat, agentId, service, 20, none),
            new AgentScheduleEntry("duty-b", 870, 300, duty, agentId, work, 30, none),
            new AgentScheduleEntry("social", 1170, 150, ScheduleActivityKind.Socialize, agentId, service, 15, none),
            new AgentScheduleEntry("rest", 1320, 120, ScheduleActivityKind.Rest, agentId, home, 10, none)
        ]);
    }

    private static NavigationLocation Offset(NavigationLocation source, int xCentimeters, int zCentimeters) => NavigationLocation.Outdoor(
        new WorldPoint(source.WorldPosition.XCentimeters + xCentimeters, source.WorldPosition.ZCentimeters + zCentimeters),
        source.ElevationCentimeters);

    private static void ApplyStationaryActivity(CharacterState character, ScheduleActivityKind activity)
    {
        character.Velocity = FixedVector3.Zero;
        character.Action = activity switch
        {
            ScheduleActivityKind.Sleep => CharacterActionKind.Sleep,
            ScheduleActivityKind.Repair or ScheduleActivityKind.Work => CharacterActionKind.RepairObject,
            ScheduleActivityKind.Trade or ScheduleActivityKind.Socialize => CharacterActionKind.Converse,
            ScheduleActivityKind.MedicalTreatment => CharacterActionKind.UseTerminal,
            _ => CharacterActionKind.Idle
        };
        character.ActionProgressPermille = (character.ActionProgressPermille + 7) % 1000;
        character.Revision++;
    }

    private static StableId ResolveFaction(CharacterAppearanceDescriptor appearance)
    {
        if (appearance.Species == CharacterSpecies.IndustrialQuadruped || appearance.ArchetypeId.Contains("hostile", StringComparison.Ordinal)) return FactionIds.BlackwaterRecoveryAuthority;
        if (appearance.ArchetypeId.Contains("guard", StringComparison.Ordinal) || appearance.ArchetypeId.Contains("caretaker", StringComparison.Ordinal)) return FactionIds.ProspectClinicCompact;
        return FactionIds.Civilians;
    }
}

internal sealed class PlayerNavigationInteractionHandler(PlayerSimulationRuntime runtime) : INavigationInteractionHandler
{
    public NavigationInteractionResult Execute(StableId agentId, NavigationRouteStep step, NavigationAgentProfile profile, ulong tick)
    {
        if (step.InteractionTargetId.IsEmpty) return NavigationInteractionResult.Completed();
        bool canUnlock = step.Kind == NavigationStepKind.UnlockDoor && profile.Has(NavigationCapability.UnlockDoors);
        bool canForce = step.Kind == NavigationStepKind.ForceDoor && profile.Has(NavigationCapability.ForceDoors);
        if (!runtime.TryPrepareDoorForNavigation(step.InteractionTargetId, canUnlock, canForce, out bool ready, out string message))
            return NavigationInteractionResult.Failed(message);
        return ready ? NavigationInteractionResult.Completed(message) : NavigationInteractionResult.Pending(message);
    }
}
