using Roentgen.Contracts;
using Roentgen.World;

namespace Roentgen.Physics.Tests;

public sealed class TerrainCollisionTests
{
    [Fact]
    public void RegisteredChunkSupportsHeightNormalSurfaceAndRaycastQueries()
    {
        var topology = new RegionalTopologyGenerator(new CampaignSeed(123456789UL)).Generate(WorldGenerationProfile.MicroRegion);
        TerrainChunkData chunk = new TerrainChunkCompiler(topology).Compile(new TerrainChunkAddress(0, 0));
        var registry = new TerrainCollisionRegistry(topology.Profile.ChunkSizeCentimeters);
        registry.Register(chunk);
        WorldPoint center = chunk.Address.Center(topology.Profile.ChunkSizeCentimeters);
        Assert.True(registry.TrySample(center.XCentimeters, center.ZCentimeters, out TerrainSurfaceSample sample));
        Assert.True(sample.Normal.Y > 0d);
        Assert.True(registry.TryRaycastDown(center.XCentimeters, sample.HeightCentimeters + 1000, center.ZCentimeters, 2000, out TerrainRaycastHit hit));
        Assert.Equal(1000, hit.TravelCentimeters);
        Assert.Equal(sample.Surface, hit.Surface);
    }

    [Fact]
    public void RemovingChunkRemovesCollision()
    {
        var topology = new RegionalTopologyGenerator(new CampaignSeed(42UL)).Generate(WorldGenerationProfile.MicroRegion);
        TerrainChunkData chunk = new TerrainChunkCompiler(topology).Compile(new TerrainChunkAddress(0, 0));
        var registry = new TerrainCollisionRegistry(topology.Profile.ChunkSizeCentimeters);
        registry.Register(chunk);
        Assert.True(registry.Remove(chunk.Address));
        WorldPoint center = chunk.Address.Center(topology.Profile.ChunkSizeCentimeters);
        Assert.False(registry.TrySample(center.XCentimeters, center.ZCentimeters, out _));
    }
}
