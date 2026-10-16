using Roentgen.Contracts;

namespace Roentgen.World.Tests;

public sealed class TerrainGenerationTests
{
    private static readonly CampaignSeed Seed = new(0x524F454E5447454EUL);

    [Fact]
    public void FloorDivisionHandlesNegativeWorldCoordinates()
    {
        Assert.Equal(-1, WorldUnits.FloorDivide(-1, 12800));
        Assert.Equal(-1, WorldUnits.FloorDivide(-12800, 12800));
        Assert.Equal(-2, WorldUnits.FloorDivide(-12801, 12800));
        Assert.Equal(new TerrainChunkAddress(-1, -1), TerrainChunkAddress.FromWorldCentimeters(-1, -1, 12800));
    }

    [Fact]
    public void RegionalTopologyIsDeterministic()
    {
        RegionalTopology first = new RegionalTopologyGenerator(Seed).Generate(WorldGenerationProfile.MicroRegion);
        RegionalTopology second = new RegionalTopologyGenerator(Seed).Generate(WorldGenerationProfile.MicroRegion);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ContentChecksum, second.ContentChecksum);
        Assert.Equal(first.Roads.Segments.Select(static road => road.Polyline.Count), second.Roads.Segments.Select(static road => road.Polyline.Count));
    }

    [Fact]
    public void MicroRegionContainsRequiredPrototypeIdentitiesAndReservations()
    {
        RegionalTopology topology = CreateTopology();
        Assert.Contains(RegionFamily.GlassBasin, topology.RegionFamilies);
        Assert.Contains(RegionFamily.BrokenMeridian, topology.RegionFamilies);
        Assert.Contains(RegionFamily.BlackwaterWorks, topology.RegionFamilies);
        Assert.Contains(topology.Reservations, static reservation => reservation.Purpose == LandmarkPurpose.Settlement);
        Assert.Contains(topology.Reservations, static reservation => reservation.Purpose == LandmarkPurpose.Ruin);
        Assert.True(new WorldTopologyValidator().Validate(topology).IsValid);
    }

    [Fact]
    public void ChunkCompilationIsIndependentOfGenerationOrder()
    {
        RegionalTopology topology = CreateTopology();
        var compiler = new TerrainChunkCompiler(topology);
        TerrainChunkAddress a = new(-1, -1);
        TerrainChunkAddress b = new(0, -1);
        TerrainChunkData firstA = compiler.Compile(a);
        _ = compiler.Compile(b);
        TerrainChunkData secondA = compiler.Compile(a);
        Assert.Equal(firstA.ContentChecksum, secondA.ContentChecksum);
    }

    [Fact]
    public void AdjacentChunksHaveByteIdenticalXSeamsAtEveryLod()
    {
        var compiler = new TerrainChunkCompiler(CreateTopology());
        TerrainChunkData left = compiler.Compile(new TerrainChunkAddress(-1, 0));
        TerrainChunkData right = compiler.Compile(new TerrainChunkAddress(0, 0));
        ValidationReport report = TerrainChunkValidator.ValidateSeam(left, right, alongX: true);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(static issue => issue.Message)));
    }

    [Fact]
    public void AdjacentChunksHaveByteIdenticalZSeamsAtEveryLod()
    {
        var compiler = new TerrainChunkCompiler(CreateTopology());
        TerrainChunkData back = compiler.Compile(new TerrainChunkAddress(0, -1));
        TerrainChunkData front = compiler.Compile(new TerrainChunkAddress(0, 0));
        ValidationReport report = TerrainChunkValidator.ValidateSeam(back, front, alongX: false);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(static issue => issue.Message)));
    }


    [Fact]
    public void ChunkCompilerHonorsCancellationBeforeExpensiveGeneration()
    {
        var compiler = new TerrainChunkCompiler(CreateTopology());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => compiler.Compile(new TerrainChunkAddress(0, 0), cancellationToken: cancellation.Token));
    }

    [Fact]
    public void HeightEvaluationUsesOverflowSafeRoadProjectionAcrossWorldExtents()
    {
        RegionalTopology topology = CreateTopology();
        var evaluator = new TerrainHeightEvaluator(topology);
        WorldGenerationProfile profile = topology.Profile;
        int[] samples =
        [
            evaluator.SampleHeightCentimeters(profile.MinimumWorldXCentimeters, profile.MinimumWorldZCentimeters),
            evaluator.SampleHeightCentimeters(profile.MaximumWorldXCentimeters, profile.MinimumWorldZCentimeters),
            evaluator.SampleHeightCentimeters(profile.MinimumWorldXCentimeters, profile.MaximumWorldZCentimeters),
            evaluator.SampleHeightCentimeters(profile.MaximumWorldXCentimeters, profile.MaximumWorldZCentimeters)
        ];
        Assert.All(samples, height => Assert.InRange(height, profile.MinimumElevationCentimeters, profile.MaximumElevationCentimeters));
    }

    [Fact]
    public void StreamingCoordinatorProducesBoundedCurrentResults()
    {
        RegionalTopology topology = CreateTopology();
        using var streaming = new TerrainStreamingCoordinator(topology);
        streaming.Update(new WorldPoint(0, 0));
        TerrainChunkGenerationResult? completed = null;
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && completed is null)
        {
            if (streaming.TryDequeueCompleted(out TerrainChunkGenerationResult result) && result.Succeeded)
            {
                completed = result;
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        TerrainChunkGenerationResult actual = Assert.IsType<TerrainChunkGenerationResult>(completed);
        Assert.True(streaming.Snapshot.PendingQueue <= topology.Profile.MaximumPendingGenerationRequests);
        Assert.True(streaming.IsCurrent(actual.Address, actual.Epoch));
    }

    private static RegionalTopology CreateTopology() => new RegionalTopologyGenerator(Seed).Generate(WorldGenerationProfile.MicroRegion);
}
