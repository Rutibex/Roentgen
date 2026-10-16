using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Navigation;

[Flags]
public enum NavigationCapability
{
    None = 0,
    Walk = 1 << 0,
    Run = 1 << 1,
    Crouch = 1 << 2,
    Climb = 1 << 3,
    Jump = 1 << 4,
    Swim = 1 << 5,
    Float = 1 << 6,
    Burrow = 1 << 7,
    OpenDoors = 1 << 8,
    UnlockDoors = 1 << 9,
    ForceDoors = 1 << 10,
    UseLadders = 1 << 11,
    HazardProtected = 1 << 12,
    IgnoreNarrowClearance = 1 << 13
}

public enum NavigationSpaceKind
{
    Outdoor = 0,
    Interior = 1,
    Transition = 2,
    AbstractRoute = 3
}

public enum NavigationStepKind
{
    Walk = 0,
    Road = 1,
    Trail = 2,
    OpenDoor = 3,
    UnlockDoor = 4,
    ForceDoor = 5,
    ClimbLadder = 6,
    UseStairs = 7,
    Jump = 8,
    Drop = 9,
    EnterStructure = 10,
    ExitStructure = 11,
    WaitForStreaming = 12,
    AbstractTravel = 13
}

public enum NavigationFailureReason
{
    None = 0,
    InvalidRequest = 1,
    StartUnavailable = 2,
    DestinationUnavailable = 3,
    NoRoute = 4,
    CapabilityMissing = 5,
    DynamicObstacle = 6,
    SearchBudgetExceeded = 7,
    StreamingUnavailable = 8,
    RouteInvalidated = 9
}

public sealed record TraversalCostProfile(
    int BaseCost = 1000,
    int RoadCostPermille = 650,
    int TrailCostPermille = 820,
    int ShallowWaterCostPermille = 1700,
    int MudCostPermille = 1450,
    int ScrubCostPermille = 1200,
    int RockCostPermille = 1150,
    int ContaminationCostPermille = 1800,
    int SlopeCostPerPermille = 2,
    int DoorInteractionCost = 600,
    int LadderCost = 1400,
    int HazardPenalty = 4000)
{
    public int SurfaceCost(TerrainSurfaceKind surface, bool hazardProtected)
    {
        int permille = surface switch
        {
            TerrainSurfaceKind.Road => RoadCostPermille,
            TerrainSurfaceKind.Trail => TrailCostPermille,
            TerrainSurfaceKind.ShallowWater => ShallowWaterCostPermille,
            TerrainSurfaceKind.IndustrialMud => MudCostPermille,
            TerrainSurfaceKind.PaleScrub => ScrubCostPermille,
            TerrainSurfaceKind.ExposedRock => RockCostPermille,
            TerrainSurfaceKind.ContaminatedSediment => ContaminationCostPermille,
            _ => 1000
        };
        int cost = Math.Max(1, BaseCost * permille / 1000);
        return surface == TerrainSurfaceKind.ContaminatedSediment && !hazardProtected ? checked(cost + HazardPenalty) : cost;
    }
}

public sealed record NavigationAgentProfile(
    string Id,
    NavigationCapability Capabilities,
    int RadiusMillimeters,
    int StandingHeightMillimeters,
    int MaximumSlopePermille,
    int MaximumStepMillimeters,
    int MaximumDropMillimeters,
    int WalkSpeedMillimetersPerSecond,
    int RunSpeedMillimetersPerSecond,
    TraversalCostProfile Costs)
{
    public bool Has(NavigationCapability capability) => (Capabilities & capability) == capability;

    public static NavigationAgentProfile OrdinaryHumanoid { get; } = new(
        "ordinary-humanoid",
        NavigationCapability.Walk | NavigationCapability.Run | NavigationCapability.Crouch | NavigationCapability.Climb |
        NavigationCapability.Jump | NavigationCapability.OpenDoors | NavigationCapability.UseLadders,
        360, 1800, 520, 420, 900, 2400, 4300, new TraversalCostProfile());

    public static NavigationAgentProfile TechnicalHumanoid { get; } = OrdinaryHumanoid with
    {
        Id = "technical-humanoid",
        Capabilities = OrdinaryHumanoid.Capabilities | NavigationCapability.UnlockDoors | NavigationCapability.HazardProtected
    };

    public static NavigationAgentProfile HeavyHumanoid { get; } = OrdinaryHumanoid with
    {
        Id = "heavy-humanoid",
        RadiusMillimeters = 470,
        StandingHeightMillimeters = 2050,
        MaximumSlopePermille = 420,
        WalkSpeedMillimetersPerSecond = 1900,
        RunSpeedMillimetersPerSecond = 3200
    };

    public static NavigationAgentProfile BreacherHumanoid { get; } = HeavyHumanoid with
    {
        Id = "breacher-humanoid",
        Capabilities = HeavyHumanoid.Capabilities | NavigationCapability.ForceDoors
    };

    public static NavigationAgentProfile HazardProtectedHumanoid { get; } = OrdinaryHumanoid with
    {
        Id = "hazard-protected-humanoid",
        Capabilities = OrdinaryHumanoid.Capabilities | NavigationCapability.HazardProtected
    };

    public static NavigationAgentProfile SmallCreature { get; } = OrdinaryHumanoid with
    {
        Id = "small-creature",
        Capabilities = NavigationCapability.Walk | NavigationCapability.Run | NavigationCapability.Jump | NavigationCapability.IgnoreNarrowClearance,
        RadiusMillimeters = 190,
        StandingHeightMillimeters = 650,
        MaximumSlopePermille = 720,
        MaximumStepMillimeters = 300,
        RunSpeedMillimetersPerSecond = 4800
    };

    public static NavigationAgentProfile Burrower { get; } = SmallCreature with
    {
        Id = "burrower",
        Capabilities = SmallCreature.Capabilities | NavigationCapability.Burrow,
        MaximumSlopePermille = 900
    };

    public static NavigationAgentProfile Quadruped { get; } = OrdinaryHumanoid with
    {
        Id = "quadruped",
        Capabilities = NavigationCapability.Walk | NavigationCapability.Run | NavigationCapability.Jump,
        RadiusMillimeters = 420,
        StandingHeightMillimeters = 1150,
        MaximumSlopePermille = 650,
        MaximumStepMillimeters = 500,
        RunSpeedMillimetersPerSecond = 5200
    };

    public static NavigationAgentProfile Floating { get; } = new(
        "floating",
        NavigationCapability.Float | NavigationCapability.OpenDoors,
        350, 1100, 1000, 2000, 5000, 2800, 4500, new TraversalCostProfile());

    public static NavigationAgentProfile RoboticChassis { get; } = HeavyHumanoid with
    {
        Id = "robotic-chassis",
        Capabilities = HeavyHumanoid.Capabilities | NavigationCapability.ForceDoors | NavigationCapability.HazardProtected,
        RadiusMillimeters = 520,
        StandingHeightMillimeters = 1900,
        MaximumStepMillimeters = 500
    };
}

public readonly record struct NavigationLocation(
    NavigationSpaceKind Space,
    WorldPoint WorldPosition,
    int ElevationCentimeters,
    StableId StructureId,
    StableId CellId)
{
    public static NavigationLocation Outdoor(WorldPoint point, int elevationCentimeters = 0) =>
        new(NavigationSpaceKind.Outdoor, point, elevationCentimeters, StableId.Empty, StableId.Empty);

    public static NavigationLocation Interior(StableId structureId, StableId cellId, WorldPoint point, int elevationCentimeters) =>
        new(NavigationSpaceKind.Interior, point, elevationCentimeters, structureId, cellId);
}

public sealed record NavigationRequest(
    StableId RequestId,
    StableId AgentId,
    NavigationLocation Start,
    NavigationLocation Destination,
    NavigationAgentProfile Profile,
    ulong NavigationRevision,
    int MaximumExpandedNodes = 20_000,
    bool PreferRoads = true,
    bool AllowPartialRoute = false);

public sealed record NavigationRouteStep(
    StableId NodeId,
    NavigationStepKind Kind,
    NavigationLocation Location,
    int Cost,
    StableId InteractionTargetId,
    string Annotation) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(NodeId);
        writer.WriteInt32((int)Kind);
        writer.WriteInt32((int)Location.Space);
        writer.WriteInt32(Location.WorldPosition.XCentimeters);
        writer.WriteInt32(Location.WorldPosition.ZCentimeters);
        writer.WriteInt32(Location.ElevationCentimeters);
        writer.WriteStableId(Location.StructureId);
        writer.WriteStableId(Location.CellId);
        writer.WriteInt32(Cost);
        writer.WriteStableId(InteractionTargetId);
        writer.WriteString(Annotation);
    }
}

public sealed class NavigationRoute : ICanonicalWritable
{
    public NavigationRoute(
        StableId requestId,
        bool success,
        NavigationFailureReason failure,
        IReadOnlyList<NavigationRouteStep> steps,
        int totalCost,
        ulong navigationRevision,
        int expandedNodes,
        string message)
    {
        RequestId = requestId;
        Success = success;
        Failure = failure;
        Steps = steps.ToArray();
        TotalCost = totalCost;
        NavigationRevision = navigationRevision;
        ExpandedNodes = expandedNodes;
        Message = message;
    }

    public StableId RequestId { get; }
    public bool Success { get; }
    public NavigationFailureReason Failure { get; }
    public IReadOnlyList<NavigationRouteStep> Steps { get; }
    public int TotalCost { get; }
    public ulong NavigationRevision { get; }
    public int ExpandedNodes { get; }
    public string Message { get; }
    public StateChecksum Checksum
    {
        get
        {
            var writer = new CanonicalWriter();
            WriteCanonical(writer);
            return writer.ComputeChecksum();
        }
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(RequestId);
        writer.WriteBoolean(Success);
        writer.WriteInt32((int)Failure);
        writer.WriteCount(Steps.Count);
        foreach (NavigationRouteStep step in Steps) step.WriteCanonical(writer);
        writer.WriteInt32(TotalCost);
        writer.WriteUInt64(NavigationRevision);
        writer.WriteInt32(ExpandedNodes);
        writer.WriteString(Message);
    }

    public static NavigationRoute Failed(NavigationRequest request, NavigationFailureReason reason, string message, int expanded = 0) =>
        new(request.RequestId, false, reason, [], 0, request.NavigationRevision, expanded, message);
}

public sealed record DynamicNavigationDoorState(
    StableId DoorId,
    DoorLifecycleState State,
    int OpenProgressPermille,
    ulong SourceRevision);

public sealed record DynamicNavigationObstacle(
    StableId Id,
    WorldRect Bounds,
    StableId StructureId,
    StableId CellId,
    bool BlocksMovement,
    int AdditionalCost,
    ulong Revision,
    ulong ExpiresTick);

public sealed class DynamicNavigationObstacleRegistry
{
    private readonly SortedDictionary<StableId, DynamicNavigationObstacle> obstacles = [];
    private readonly SortedDictionary<StableId, DynamicNavigationDoorState> doors = [];
    public ulong Revision { get; private set; }
    public IReadOnlyCollection<DynamicNavigationObstacle> Obstacles => obstacles.Values;
    public IReadOnlyCollection<DynamicNavigationDoorState> Doors => doors.Values;

    public void Upsert(DynamicNavigationObstacle obstacle)
    {
        obstacle.Bounds.Validate();
        if (obstacles.TryGetValue(obstacle.Id, out DynamicNavigationObstacle? current) && current == obstacle) return;
        obstacles[obstacle.Id] = obstacle;
        Revision++;
    }

    public void UpsertDoor(DynamicNavigationDoorState door)
    {
        if (door.DoorId.IsEmpty) throw new ArgumentException("Door ID cannot be empty.", nameof(door));
        if (doors.TryGetValue(door.DoorId, out DynamicNavigationDoorState? current) && current == door) return;
        doors[door.DoorId] = door;
        Revision++;
    }

    public bool TryGetDoor(StableId doorId, out DynamicNavigationDoorState? door) => doors.TryGetValue(doorId, out door);

    public bool RemoveDoor(StableId doorId)
    {
        bool changed = doors.Remove(doorId);
        if (changed) Revision++;
        return changed;
    }

    public bool Remove(StableId id)
    {
        bool changed = obstacles.Remove(id);
        if (changed) Revision++;
        return changed;
    }

    public void Expire(ulong tick)
    {
        foreach (StableId id in obstacles.Values.Where(value => value.ExpiresTick > 0 && value.ExpiresTick <= tick).Select(value => value.Id).ToArray())
            Remove(id);
    }

    public bool IsBlocked(WorldPoint point, StableId structureId, StableId cellId)
    {
        return obstacles.Values.Any(value => value.BlocksMovement &&
            (value.StructureId.IsEmpty || value.StructureId == structureId) &&
            (value.CellId.IsEmpty || value.CellId == cellId) && value.Bounds.Contains(point));
    }

    public int AdditionalCost(WorldPoint point, StableId structureId, StableId cellId) => obstacles.Values
        .Where(value => !value.BlocksMovement && (value.StructureId.IsEmpty || value.StructureId == structureId) &&
            (value.CellId.IsEmpty || value.CellId == cellId) && value.Bounds.Contains(point))
        .Sum(static value => Math.Max(0, value.AdditionalCost));
}
