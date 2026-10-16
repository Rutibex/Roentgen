namespace Roentgen.World;

public sealed record WorldGenerationProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public int WorldWidthMeters { get; init; } = 2048;
    public int WorldDepthMeters { get; init; } = 2048;
    public int ChunkSizeMeters { get; init; } = 128;
    public int NearChunkSamples { get; init; } = 65;
    public int TopologyCellMeters { get; init; } = 16;
    public int WaterLevelCentimeters { get; init; } = -250;
    public int MaximumElevationCentimeters { get; init; } = 14_000;
    public int MinimumElevationCentimeters { get; init; } = -2_500;
    public int RoadHalfWidthCentimeters { get; init; } = 500;
    public int TrailHalfWidthCentimeters { get; init; } = 220;
    public int StreamingRadiusChunks { get; init; } = 3;
    public int DormantRadiusChunks { get; init; } = 5;
    public int MaximumCpuResidentBytes { get; init; } = 192 * 1024 * 1024;
    public int MaximumPendingGenerationRequests { get; init; } = 256;
    public int WorkerCount { get; init; } = 2;

    public int ChunkSizeCentimeters => WorldUnits.MetersToCentimeters(ChunkSizeMeters);
    public int TopologyCellCentimeters => WorldUnits.MetersToCentimeters(TopologyCellMeters);
    public int ChunkCountX => WorldWidthMeters / ChunkSizeMeters;
    public int ChunkCountZ => WorldDepthMeters / ChunkSizeMeters;
    public int MinimumWorldXCentimeters => -WorldUnits.MetersToCentimeters(WorldWidthMeters) / 2;
    public int MinimumWorldZCentimeters => -WorldUnits.MetersToCentimeters(WorldDepthMeters) / 2;
    public int MaximumWorldXCentimeters => MinimumWorldXCentimeters + WorldUnits.MetersToCentimeters(WorldWidthMeters);
    public int MaximumWorldZCentimeters => MinimumWorldZCentimeters + WorldUnits.MetersToCentimeters(WorldDepthMeters);
    public int TopologySamplesX => WorldWidthMeters / TopologyCellMeters + 1;
    public int TopologySamplesZ => WorldDepthMeters / TopologyCellMeters + 1;
    public WorldRect Bounds => new(MinimumWorldXCentimeters, MinimumWorldZCentimeters, MaximumWorldXCentimeters, MaximumWorldZCentimeters);

    public IReadOnlyList<int> LodSampleCounts =>
    [
        NearChunkSamples,
        (NearChunkSamples - 1) / 2 + 1,
        (NearChunkSamples - 1) / 4 + 1
    ];

    public string ProfileId { get; init; } = "micro-region";
    public bool RequiresAllRegionFamilies { get; init; }

    public static WorldGenerationProfile MicroRegion { get; } = new();

    public static WorldGenerationProfile ValidationRegion { get; } = new()
    {
        ProfileId = "validation-region",
        WorldWidthMeters = 4096,
        WorldDepthMeters = 4096,
        StreamingRadiusChunks = 4,
        DormantRadiusChunks = 7,
        MaximumCpuResidentBytes = 320 * 1024 * 1024,
        MaximumPendingGenerationRequests = 384,
        WorkerCount = 3,
        RequiresAllRegionFamilies = true
    };

    public static WorldGenerationProfile LaunchCampaign { get; } = new()
    {
        ProfileId = "launch-campaign",
        WorldWidthMeters = 8192,
        WorldDepthMeters = 8192,
        StreamingRadiusChunks = 4,
        DormantRadiusChunks = 8,
        MaximumCpuResidentBytes = 512 * 1024 * 1024,
        MaximumPendingGenerationRequests = 512,
        WorkerCount = 4,
        RequiresAllRegionFamilies = true
    };

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProfileId);

        if (SchemaVersion <= 0)
        {
            throw new InvalidOperationException("World generation schema version must be positive.");
        }

        if (WorldWidthMeters < ChunkSizeMeters || WorldDepthMeters < ChunkSizeMeters || ChunkSizeMeters <= 0)
        {
            throw new InvalidOperationException("World dimensions and chunk size are invalid.");
        }

        if (WorldWidthMeters % ChunkSizeMeters != 0 || WorldDepthMeters % ChunkSizeMeters != 0)
        {
            throw new InvalidOperationException("World dimensions must be divisible by the chunk size.");
        }

        if (ChunkSizeMeters % TopologyCellMeters != 0 || TopologyCellMeters <= 0)
        {
            throw new InvalidOperationException("The topology cell size must evenly divide the chunk size.");
        }

        if (NearChunkSamples < 17 || (((NearChunkSamples - 1) & (NearChunkSamples - 2)) != 0))
        {
            throw new InvalidOperationException("NearChunkSamples must be one greater than a power of two and at least 17.");
        }

        if (MinimumElevationCentimeters >= MaximumElevationCentimeters)
        {
            throw new InvalidOperationException("Minimum elevation must be below maximum elevation.");
        }

        if (RoadHalfWidthCentimeters <= 0 || TrailHalfWidthCentimeters <= 0)
        {
            throw new InvalidOperationException("Road and trail widths must be positive.");
        }

        if (StreamingRadiusChunks < 1 || DormantRadiusChunks < StreamingRadiusChunks)
        {
            throw new InvalidOperationException("Streaming radii are invalid.");
        }

        if (MaximumCpuResidentBytes < 8 * 1024 * 1024 || MaximumPendingGenerationRequests < 8 || WorkerCount is < 1 or > 32)
        {
            throw new InvalidOperationException("Streaming budgets are invalid.");
        }
    }
}
