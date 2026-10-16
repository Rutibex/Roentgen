using Roentgen.Contracts;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Structures.Tests;

public sealed class UpgradeGeometryTests
{
    [Fact]
    public void StairFlightsHaveSmallTreadsAndOpenUpperSlabs()
    {
        var topology=new RegionalTopologyGenerator(new CampaignSeed(0x524F454E5447454EUL)).Generate(WorldGenerationProfile.MicroRegion);
        var world=new MicroRegionStructureCompiler(topology).Compile();int flights=0;
        foreach(var building in world.Structures)
        foreach(var flight in building.VerticalLinks)
        {
            flights++;
            var treads=building.Primitives.Where(p=>p.Kind==StructurePrimitiveKind.Stair&&p.Bounds.Minimum.YCentimeters>=flight.Bounds.Minimum.YCentimeters&&p.Bounds.Maximum.YCentimeters<=flight.Bounds.Maximum.YCentimeters).ToArray();
            Assert.True(treads.Length>=10);
            Assert.All(treads,t=>Assert.InRange(t.Bounds.Maximum.YCentimeters-t.Bounds.Minimum.YCentimeters,1,18));
            var aperture=flight.Bounds;
            Assert.DoesNotContain(building.Primitives,p=>p.Kind==StructurePrimitiveKind.Floor&&p.Bounds.Maximum.YCentimeters==aperture.Maximum.YCentimeters&&p.Bounds.Minimum.XCentimeters<aperture.Maximum.XCentimeters&&p.Bounds.Maximum.XCentimeters>aperture.Minimum.XCentimeters&&p.Bounds.Minimum.ZCentimeters<aperture.Maximum.ZCentimeters&&p.Bounds.Maximum.ZCentimeters>aperture.Minimum.ZCentimeters);
        }
        Assert.True(flights>0);
    }
}
