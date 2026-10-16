namespace Roentgen.World;

public enum RegionFamily
{
    GlassBasin = 0,
    OssuaryFields = 1,
    BlackwaterWorks = 2,
    BrokenMeridian = 3,
    RedQuarryCountry = 4,
    QuietRange = 5
}

public enum TerrainSurfaceKind : byte
{
    DryEarth = 0,
    FusedGlass = 1,
    BrokenAsphalt = 2,
    IndustrialMud = 3,
    ShallowWater = 4,
    PaleScrub = 5,
    ExposedRock = 6,
    ContaminatedSediment = 7,
    Road = 8,
    Trail = 9
}

public enum RoadKind
{
    MajorRoad = 0,
    MinorRoad = 1,
    Trail = 2
}

public enum LandmarkPurpose
{
    Settlement = 0,
    Ruin = 1,
    Infrastructure = 2,
    Hazard = 3,
    QuestReserved = 4
}

public enum TerrainChunkLifecycleState
{
    Unrequested = 0,
    Requested = 1,
    GeneratingCpu = 2,
    AwaitingUpload = 3,
    Resident = 4,
    Dormant = 5,
    Unloading = 6,
    Failed = 7,
    Cancelled = 8
}

public readonly record struct TerrainMaterialWeights(byte Primary, byte Secondary, byte Tertiary, byte Quaternary)
{
    public int Total => Primary + Secondary + Tertiary + Quaternary;

    public static TerrainMaterialWeights Solid => new(255, 0, 0, 0);
}
