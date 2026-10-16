namespace Roentgen.World.Structures;

public sealed record RoomRequirement(
    string Key,
    RoomArchetype Archetype,
    AccessClass Access,
    PrivacyClass Privacy,
    int MinimumAreaSquareMeters,
    int PreferredAreaSquareMeters,
    int MinimumWidthCentimeters,
    int FloorPreference = 0,
    bool RequiresExteriorWall = false,
    bool QuestCritical = false,
    int OccupantCapacity = 1)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Key);
        if (MinimumAreaSquareMeters <= 0 || PreferredAreaSquareMeters < MinimumAreaSquareMeters || MinimumWidthCentimeters < 100 || FloorPreference < -1 || OccupantCapacity < 0)
            throw new InvalidOperationException($"Room requirement '{Key}' is invalid.");
    }
}

public sealed record AdjacencyRule(string RoomKeyA, string RoomKeyB, ConnectionRequirement Requirement, ConnectionKind Kind = ConnectionKind.Door, LockPolicy LockPolicy = LockPolicy.None)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RoomKeyA);
        ArgumentException.ThrowIfNullOrWhiteSpace(RoomKeyB);
        if (string.Equals(RoomKeyA, RoomKeyB, StringComparison.Ordinal)) throw new InvalidOperationException("A room cannot require adjacency to itself.");
    }
}

public sealed record FootprintRules(int MinimumWidthCentimeters, int MaximumWidthCentimeters, int MinimumDepthCentimeters, int MaximumDepthCentimeters, int SetbackCentimeters = 200)
{
    public void Validate()
    {
        if (MinimumWidthCentimeters < 400 || MinimumDepthCentimeters < 400 || MaximumWidthCentimeters < MinimumWidthCentimeters || MaximumDepthCentimeters < MinimumDepthCentimeters || SetbackCentimeters < 0)
            throw new InvalidOperationException("Footprint rules are invalid.");
    }
}

public sealed record FloorCountRules(int MinimumFloors, int MaximumFloors, bool AllowsBasement = false, bool RequiresRoofAccess = false)
{
    public void Validate()
    {
        if (MinimumFloors < 1 || MaximumFloors < MinimumFloors || MaximumFloors > 4) throw new InvalidOperationException("Phase 7 supports one through four above-ground floors.");
    }
}

public sealed record StructuralSystemRules(StructuralSystemKind Kind, int WallThicknessCentimeters, int FloorHeightCentimeters, int MaximumUnsupportedSpanCentimeters)
{
    public void Validate()
    {
        if (WallThicknessCentimeters is < 10 or > 80 || FloorHeightCentimeters is < 240 or > 600 || MaximumUnsupportedSpanCentimeters < 300)
            throw new InvalidOperationException("Structural-system rules are invalid.");
    }
}

public sealed record NavigationRequirements(int MinimumCorridorWidthCentimeters = 150, int MinimumDoorWidthCentimeters = 100, bool RequiresAlternateCriticalRoute = false, bool RequiresRetreatRoute = true)
{
    public void Validate()
    {
        if (MinimumCorridorWidthCentimeters < 100 || MinimumDoorWidthCentimeters < 80) throw new InvalidOperationException("Navigation dimensions are too small.");
    }
}

public sealed record StructureBudget(int MaximumRooms, int MaximumPrimitives, int MaximumDoors, int MaximumStreamingCells)
{
    public void Validate()
    {
        if (MaximumRooms < 2 || MaximumPrimitives < 16 || MaximumDoors < 1 || MaximumStreamingCells < 1) throw new InvalidOperationException("Structure budget is invalid.");
    }
}

public sealed record BuildingGrammar(
    string GrammarId,
    int SchemaVersion,
    BuildingPurpose Purpose,
    FootprintRules Footprint,
    FloorCountRules Floors,
    StructuralSystemRules StructuralSystem,
    IReadOnlyList<RoomRequirement> RequiredRooms,
    IReadOnlyList<RoomRequirement> OptionalRooms,
    IReadOnlyList<AdjacencyRule> AdjacencyRules,
    NavigationRequirements Navigation,
    StructureBudget Budget,
    int DamageSeverityPermille = 0)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(GrammarId);
        if (SchemaVersion <= 0 || DamageSeverityPermille is < 0 or > 1000) throw new InvalidOperationException("Grammar version or damage severity is invalid.");
        Footprint.Validate(); Floors.Validate(); StructuralSystem.Validate(); Navigation.Validate(); Budget.Validate();
        if (RequiredRooms.Count < 2 || RequiredRooms.Count > Budget.MaximumRooms) throw new InvalidOperationException("Grammar room count is outside its budget.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (RoomRequirement room in RequiredRooms.Concat(OptionalRooms)) { room.Validate(); if (!keys.Add(room.Key)) throw new InvalidOperationException($"Duplicate room key '{room.Key}'."); }
        foreach (AdjacencyRule rule in AdjacencyRules) { rule.Validate(); if (!keys.Contains(rule.RoomKeyA) || !keys.Contains(rule.RoomKeyB)) throw new InvalidOperationException("Adjacency references an unknown room."); }
    }
}
