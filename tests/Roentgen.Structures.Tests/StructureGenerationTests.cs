using Roentgen.Contracts;
using Roentgen.Navigation;
using Roentgen.Physics;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Structures.Tests;

public sealed class StructureGenerationTests
{
    private static readonly CampaignSeed Seed = new(0x524F454E5447454EUL);

    [Fact]
    public void MicroRegionStructureWorldIsDeterministic()
    {
        GeneratedStructureWorld first = CreateWorld();
        GeneratedStructureWorld second = CreateWorld();
        Assert.Equal(first.ContentChecksum, second.ContentChecksum);
        Assert.Equal(first.Structures.Select(static value => value.ContentChecksum), second.Structures.Select(static value => value.ContentChecksum));
    }

    [Fact]
    public void PermanentRegressionSitesAreGenerated()
    {
        GeneratedStructureWorld world = CreateWorld();
        Assert.Contains(world.Structures, static value => value.Name.Contains("Prospect Clinic", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(world.Structures, static value => value.Name.Contains("Blackwater Intake", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(world.Structures, static value => value.Name.Contains("Meridian Relay", StringComparison.OrdinalIgnoreCase));
        Assert.True(world.Structures.Count >= 10);
    }

    [Fact]
    public void EveryGeneratedStructurePassesCriticalValidation()
    {
        foreach (StructureBlueprint structure in CreateWorld().Structures)
        {
            StructureValidationReport report = new StructureValidator().Validate(structure);
            Assert.False(report.HasCriticalIssues, $"{structure.Name}: {string.Join("; ", report.Issues.Select(static issue => issue.Message))}");
            Assert.Equal(structure.Rooms.Count, report.Metrics.ReachableRooms);
        }
    }

    [Fact]
    public void GeneratorProducesRoomGraphBeforePhysicalProducts()
    {
        foreach (StructureBlueprint structure in CreateWorld().Structures)
        {
            Assert.NotEmpty(structure.Rooms);
            Assert.NotEmpty(structure.Connections);
            Assert.NotEmpty(structure.Primitives);
            Assert.Contains("room-graph-created-before-geometry", structure.Provenance.ValidationResults);
        }
    }

    [Fact]
    public void NavigationEmissionReachesQuestCriticalRooms()
    {
        var emitter = new StructureNavigationEmitter();
        var validator = new AutomatedStructureTraversalValidator();
        foreach (StructureBlueprint structure in CreateWorld().Structures)
        {
            InteriorNavigationData navigation = emitter.Emit(structure);
            TraversalValidationReport report = validator.Validate(structure, navigation);
            Assert.True(report.IsValid, $"{structure.Name}: {string.Join("; ", report.Issues.Select(static issue => issue.Message))}");
            Assert.True(report.RoutesSucceeded > 0);
        }
    }

    [Fact]
    public void HistoricalDamagePreservesRequiredTraversalSurfaces()
    {
        foreach (StructureBlueprint structure in CreateWorld().Structures)
        {
            Assert.DoesNotContain(structure.Primitives, static value => value.RemovedByDamage && value.Kind is StructurePrimitiveKind.Floor or StructurePrimitiveKind.Stair);
            Assert.Contains(structure.Doors, static value => value.Exterior && value.InitialState is not DoorLifecycleState.Destroyed and not DoorLifecycleState.Missing);
        }
    }

    [Fact]
    public void RepairEngineRestoresMissingExteriorEntrance()
    {
        StructureBlueprint original = CreateWorld().Structures.First();
        DoorDefinition[] brokenDoors = original.Doors.Select(static door => door.Exterior ? door with { InitialState = DoorLifecycleState.Missing } : door).ToArray();
        var broken = new StructureBlueprint(original.Id, original.ParentSiteId, original.ParcelId, original.Name, original.Grammar, original.WorldOrigin, original.Footprint, original.FloorCount, original.Rooms, original.Connections, brokenDoors, original.VerticalLinks, original.Primitives, original.Reservations, original.DamageHistory, original.StreamingCells, original.Provenance);
        StructureValidationReport report = new StructureValidator().Validate(broken);
        Assert.True(report.HasCriticalIssues);
        StructureRepairResult result = new StructureRepairEngine().Repair(broken, report, 8);
        Assert.True(result.Succeeded, string.Join("; ", result.Report.Issues.Select(static issue => issue.Message)));
        Assert.NotEmpty(result.Actions);
    }

    [Fact]
    public void StaticCollisionProductsAgreeWithBlueprintBounds()
    {
        StructureBlueprint structure = CreateWorld().Structures.First();
        StructureCollisionData collision = new StructureCollisionCompiler().Compile(structure);
        Assert.Equal(structure.Id, collision.StructureId);
        Assert.NotEmpty(collision.Colliders);
        Assert.All(collision.Colliders, static box => Assert.False(box.WorldBounds.WidthCentimeters <= 0 || box.WorldBounds.HeightCentimeters <= 0 || box.WorldBounds.DepthCentimeters <= 0));
    }

    [Fact]
    public async Task StructureStreamingIsBoundedAndRejectsStaleEpochs()
    {
        GeneratedStructureWorld world = CreateWorld();
        await using var streaming = new StructureStreamingCoordinator(world.Structures, new StructureStreamingOptions(2, 8, 8, 8L * 1024L * 1024L));
        StructureBlueprint structure = world.Structures.First();
        Assert.True(streaming.TryRequest(structure.Id, StructureCellLayer.ExteriorShell, 0, CancellationToken.None, out long oldEpoch));
        Assert.True(streaming.TryRequest(structure.Id, StructureCellLayer.ExteriorShell, 0, CancellationToken.None, out long currentEpoch));
        Assert.True(currentEpoch > oldEpoch);
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        StructureStreamingResult? result = null;
        while (DateTime.UtcNow < deadline && result is null)
        {
            if (streaming.TryReadResult(out StructureStreamingResult? next) && next is not null && next.Epoch == currentEpoch) result = next;
            else await Task.Delay(5);
        }
        Assert.NotNull(result);
        Assert.True(result!.Succeeded);
        Assert.True(streaming.CaptureTelemetry().ResidentCpuBytes <= 8L * 1024L * 1024L);
    }

    private static GeneratedStructureWorld CreateWorld()
    {
        RegionalTopology topology = new RegionalTopologyGenerator(Seed).Generate(WorldGenerationProfile.MicroRegion);
        return new MicroRegionStructureCompiler(topology).Compile();
    }
}
