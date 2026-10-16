using Microsoft.Xna.Framework;
using Roentgen.Assets;
using Roentgen.Contracts;
using Roentgen.Physics;
using Roentgen.Rendering;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.App;

internal sealed class Phase7WorldScene : IDisposable
{
    private readonly RendererFoundation _renderer;
    private readonly RegionalTopology _topology;
    private readonly TerrainStreamingCoordinator _streaming;
    private readonly TerrainChunkRenderRegistry _terrainRendering = new();
    private readonly TerrainCollisionRegistry _collision;
    private readonly GeneratedStructureWorld _structureWorld;
    private readonly StructureGpuRegistry _structureRendering = new();
    private readonly StructureCollisionRegistry _structureCollision = new();
    private readonly Queue<TerrainChunkGenerationResult> _uploadBacklog = new();
    private readonly Queue<(TerrainChunkAddress Address, long Epoch)> _unloadBacklog = new();
    private readonly Dictionary<TerrainChunkAddress, TerrainChunkGenerationResult> _cpuChunks = [];
    private readonly List<GpuMesh> _ownedMeshes = [];
    private readonly List<GpuMaterialAsset> _ownedMaterials = [];
    private readonly List<RenderItem> _landmarkItems = [];
    private readonly RenderItem _distantTerrain;
    private WorldPoint _focus;
    private bool _disposed;

    public Phase7WorldScene(RendererFoundation renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
        var campaignSeed = new CampaignSeed(0x524F454E5447454EUL);
        _topology = new RegionalTopologyGenerator(campaignSeed).Generate(WorldGenerationProfile.MicroRegion);
        var topologyReport = new WorldTopologyValidator().Validate(_topology);
        if (!topologyReport.IsValid)
        {
            throw new InvalidDataException(string.Join("; ", topologyReport.Issues.Select(static issue => issue.Message)));
        }

        _streaming = new TerrainStreamingCoordinator(_topology);
        _collision = new TerrainCollisionRegistry(_topology.Profile.ChunkSizeCentimeters);
        _structureWorld = new MicroRegionStructureCompiler(_topology).Compile();
        var structureCollisionCompiler = new StructureCollisionCompiler();
        long structureEpoch = 1;
        foreach (StructureBlueprint structure in _structureWorld.Structures)
        {
            _structureCollision.Register(structureCollisionCompiler.Compile(structure));
            _structureRendering.TryQueueUpload(renderer, structure, StructureCellLayer.ExteriorShell, structureEpoch);
            _structureRendering.TryQueueUpload(renderer, structure, StructureCellLayer.CriticalInterior, structureEpoch);
            _structureRendering.TryQueueUpload(renderer, structure, StructureCellLayer.Decoration, structureEpoch);
        }
        GpuMesh distantMesh = Own(renderer.UploadNow(TerrainRenderingBridge.CreateDistantMesh(_topology)));
        _distantTerrain = new RenderItem(
            "distant-regional-topology",
            distantMesh,
            new MaterialDefinition("distant-terrain", Color.White, 1f),
            Matrix.Identity,
            CastsShadow: false,
            ReceivesShadow: true);
        // Phase 7 replaces reservation placeholder props with generated structures.
        LandmarkReservation start = _topology.Reservations.First(static reservation => reservation.Purpose == LandmarkPurpose.Settlement);
        _focus = start.Center;
        _streaming.Update(_focus);
    }

    public RegionalTopology Topology => _topology;
    public TerrainStreamingSnapshot StreamingSnapshot => _streaming.Snapshot;
    public WorldPoint StartPosition => _topology.Reservations.First(static reservation => reservation.Purpose == LandmarkPurpose.Settlement).Center;
    public TerrainCollisionRegistry Collision => _collision;

    public void Update(Vector3 cameraPosition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _focus = WorldPoint.FromMeters(cameraPosition.X, cameraPosition.Z);
        _streaming.Update(_focus);
        while (_streaming.TryDequeueCompleted(out TerrainChunkGenerationResult result))
        {
            if (!result.Succeeded || !_streaming.IsCurrent(result.Address, result.Epoch))
            {
                continue;
            }

            if (result.Chunk is not TerrainChunkData chunk)
            {
                continue;
            }
            _collision.Register(chunk);
            _cpuChunks[result.Address] = result;
            _uploadBacklog.Enqueue(result);
        }

        while (_streaming.TryDequeueUnloaded(out TerrainChunkAddress address, out long epoch))
        {
            _collision.Remove(address);
            _cpuChunks.Remove(address);
            _unloadBacklog.Enqueue((address, epoch));
        }

        int uploadAttempts = _uploadBacklog.Count;
        for (int attempt = 0; attempt < uploadAttempts; attempt++)
        {
            TerrainChunkGenerationResult result = _uploadBacklog.Peek();
            int distance = result.Address.ChebyshevDistance(StreamingSnapshot.FocusChunk);
            int lod = distance <= 1 ? 0 : distance <= 2 ? 1 : 2;
            if (!_terrainRendering.TryQueueUpload(_renderer, _streaming, result, lod))
            {
                break;
            }

            _uploadBacklog.Dequeue();
        }

        int unloadAttempts = _unloadBacklog.Count;
        for (int attempt = 0; attempt < unloadAttempts; attempt++)
        {
            (TerrainChunkAddress Address, long Epoch) item = _unloadBacklog.Peek();
            if (!_terrainRendering.TryQueueUnload(_renderer, item.Address, item.Epoch))
            {
                break;
            }

            _unloadBacklog.Dequeue();
        }

        ReconcileResidentLods();
    }

    private void ReconcileResidentLods()
    {
        TerrainChunkAddress focus = StreamingSnapshot.FocusChunk;
        foreach ((TerrainChunkAddress address, TerrainChunkGenerationResult result) in _cpuChunks.OrderBy(static pair => pair.Key))
        {
            if (!_streaming.IsCurrent(address, result.Epoch) || _terrainRendering.HasPendingUpload(address))
            {
                continue;
            }

            int distance = address.ChebyshevDistance(focus);
            int desiredLod = distance <= 1 ? 0 : distance <= 2 ? 1 : 2;
            if (!_terrainRendering.TryGetResidentLod(address, out int residentLod) || residentLod == desiredLod)
            {
                continue;
            }

            if (!_terrainRendering.TryQueueLodReplacement(_renderer, _streaming, result, desiredLod))
            {
                break;
            }
        }
    }

    public bool TryGetGroundHeight(float worldX, float worldZ, out float heightMeters)
    {
        if (_collision.TrySample(WorldUnits.MetersToCentimeters(worldX), WorldUnits.MetersToCentimeters(worldZ), out TerrainSurfaceSample sample))
        {
            heightMeters = WorldUnits.CentimetersToMeters(sample.HeightCentimeters);
            return true;
        }

        heightMeters = 0f;
        return false;
    }

    public RenderFrameSnapshot Build(long tick, CameraState camera, bool showDebug)
    {
        var items = new List<RenderItem> { _distantTerrain };
        items.AddRange(_terrainRendering.BuildRenderItems(_topology.Profile));
        items.AddRange(_landmarkItems);
        items.AddRange(_structureRendering.BuildRenderItems(_structureWorld.Structures));
        IReadOnlyList<DebugLine> lines = showDebug ? BuildDebugLines() : Array.Empty<DebugLine>();
        return new RenderFrameSnapshot(
            tick,
            camera,
            items,
            new DirectionalLight(Vector3.Normalize(new Vector3(-0.45f, -1f, -0.3f)), new Color(255, 238, 202), 1.08f),
            new FogSettings(new Color(68, 78, 74), 110f, 620f),
            new Color(47, 58, 59),
            lines);
    }

    private void BuildLandmarkProps(RendererFoundation renderer)
    {
        var meshCompiler = new MeshRecipeCompiler();
        var materialCompiler = new MaterialRecipeCompiler();
        GpuMaterialAsset steel = Own(renderer.UploadNow(materialCompiler.Compile(ProceduralAssetCatalog.Materials["rusted-steel"])));
        GpuMaterialAsset concrete = Own(renderer.UploadNow(materialCompiler.Compile(ProceduralAssetCatalog.Materials["weathered-concrete"])));
        TerrainHeightEvaluator evaluator = new(_topology);
        foreach (LandmarkReservation reservation in _topology.Reservations)
        {
            string recipeId = reservation.Purpose switch
            {
                LandmarkPurpose.Settlement => "inspection-cart",
                LandmarkPurpose.Ruin => "water-tower",
                _ => "diagnostic-terminal"
            };
            CompiledMeshAsset asset = meshCompiler.Compile(ProceduralAssetCatalog.Meshes[recipeId]);
            GpuMesh mesh = Own(renderer.UploadNow(asset, Math.Min(1, asset.Lods.Count - 1)));
            int height = evaluator.SampleHeightCentimeters(reservation.Center.XCentimeters, reservation.Center.ZCentimeters);
            float scale = reservation.Purpose == LandmarkPurpose.Ruin ? 1.5f : 2f;
            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(
                WorldUnits.CentimetersToMeters(reservation.Center.XCentimeters),
                WorldUnits.CentimetersToMeters(height),
                WorldUnits.CentimetersToMeters(reservation.Center.ZCentimeters));
            _landmarkItems.Add(new RenderItem(
                $"reservation-prop-{reservation.Id}",
                mesh,
                reservation.Purpose == LandmarkPurpose.Ruin ? steel.Material : concrete.Material,
                world));
        }
    }

    private IReadOnlyList<DebugLine> BuildDebugLines()
    {
        var lines = new List<DebugLine>();
        TerrainHeightEvaluator evaluator = new(_topology);
        foreach (RoadSegment road in _topology.Roads.Segments)
        {
            Color color = road.Kind == RoadKind.Trail ? new Color(211, 173, 92) : new Color(82, 210, 169);
            for (int index = 0; index < road.Polyline.Count - 1; index++)
            {
                WorldPoint a = road.Polyline[index];
                WorldPoint b = road.Polyline[index + 1];
                lines.Add(new DebugLine(ToVector(a, evaluator) + Vector3.Up * 0.35f, ToVector(b, evaluator) + Vector3.Up * 0.35f, color));
            }
        }

        foreach (LandmarkReservation reservation in _topology.Reservations)
        {
            WorldRect bounds = reservation.Bounds;
            WorldPoint a = new(bounds.MinimumXCentimeters, bounds.MinimumZCentimeters);
            WorldPoint b = new(bounds.MaximumXCentimeters, bounds.MinimumZCentimeters);
            WorldPoint c = new(bounds.MaximumXCentimeters, bounds.MaximumZCentimeters);
            WorldPoint d = new(bounds.MinimumXCentimeters, bounds.MaximumZCentimeters);
            Color color = new(232, 205, 111);
            lines.Add(new DebugLine(ToVector(a, evaluator) + Vector3.Up, ToVector(b, evaluator) + Vector3.Up, color));
            lines.Add(new DebugLine(ToVector(b, evaluator) + Vector3.Up, ToVector(c, evaluator) + Vector3.Up, color));
            lines.Add(new DebugLine(ToVector(c, evaluator) + Vector3.Up, ToVector(d, evaluator) + Vector3.Up, color));
            lines.Add(new DebugLine(ToVector(d, evaluator) + Vector3.Up, ToVector(a, evaluator) + Vector3.Up, color));
        }

        foreach (var entry in _streaming.InspectEntries())
        {
            TerrainChunkAddress address = entry.Address;
            TerrainChunkLifecycleState state = entry.State;
            if (state is not (TerrainChunkLifecycleState.Resident or TerrainChunkLifecycleState.AwaitingUpload or TerrainChunkLifecycleState.GeneratingCpu))
            {
                continue;
            }

            WorldPoint origin = address.Origin(_topology.Profile.ChunkSizeCentimeters);
            int size = _topology.Profile.ChunkSizeCentimeters;
            WorldPoint a = origin;
            WorldPoint b = new(origin.XCentimeters + size, origin.ZCentimeters);
            WorldPoint c = new(origin.XCentimeters + size, origin.ZCentimeters + size);
            WorldPoint d = new(origin.XCentimeters, origin.ZCentimeters + size);
            Color color = state == TerrainChunkLifecycleState.Resident ? new Color(82, 157, 210) : new Color(188, 120, 80);
            lines.Add(new DebugLine(ToVector(a, evaluator) + Vector3.Up * 0.2f, ToVector(b, evaluator) + Vector3.Up * 0.2f, color));
            lines.Add(new DebugLine(ToVector(b, evaluator) + Vector3.Up * 0.2f, ToVector(c, evaluator) + Vector3.Up * 0.2f, color));
            lines.Add(new DebugLine(ToVector(c, evaluator) + Vector3.Up * 0.2f, ToVector(d, evaluator) + Vector3.Up * 0.2f, color));
            lines.Add(new DebugLine(ToVector(d, evaluator) + Vector3.Up * 0.2f, ToVector(a, evaluator) + Vector3.Up * 0.2f, color));
        }


        foreach (StructureBlueprint structure in _structureWorld.Structures)
        {
            float originX = WorldUnits.CentimetersToMeters(structure.WorldOrigin.XCentimeters);
            float originY = WorldUnits.CentimetersToMeters(structure.WorldOrigin.YCentimeters);
            float originZ = WorldUnits.CentimetersToMeters(structure.WorldOrigin.ZCentimeters);
            foreach (RoomNode room in structure.Rooms.Where(static room => room.Archetype is not RoomArchetype.Corridor and not RoomArchetype.Stairwell))
            {
                float y = originY + WorldUnits.CentimetersToMeters(room.Floor * structure.Grammar.StructuralSystem.FloorHeightCentimeters) + 0.08f;
                Vector3 a = new(originX + WorldUnits.CentimetersToMeters(room.Bounds.MinimumXCentimeters), y, originZ + WorldUnits.CentimetersToMeters(room.Bounds.MinimumZCentimeters));
                Vector3 b = new(originX + WorldUnits.CentimetersToMeters(room.Bounds.MaximumXCentimeters), y, originZ + WorldUnits.CentimetersToMeters(room.Bounds.MinimumZCentimeters));
                Vector3 c = new(originX + WorldUnits.CentimetersToMeters(room.Bounds.MaximumXCentimeters), y, originZ + WorldUnits.CentimetersToMeters(room.Bounds.MaximumZCentimeters));
                Vector3 d = new(originX + WorldUnits.CentimetersToMeters(room.Bounds.MinimumXCentimeters), y, originZ + WorldUnits.CentimetersToMeters(room.Bounds.MaximumZCentimeters));
                Color color = room.QuestCritical ? new Color(244, 206, 97) : new Color(139, 187, 205);
                lines.Add(new DebugLine(a, b, color)); lines.Add(new DebugLine(b, c, color)); lines.Add(new DebugLine(c, d, color)); lines.Add(new DebugLine(d, a, color));
            }
        }
        return lines;
    }

    private static Vector3 ToVector(WorldPoint point, TerrainHeightEvaluator evaluator) => new(
        WorldUnits.CentimetersToMeters(point.XCentimeters),
        WorldUnits.CentimetersToMeters(evaluator.SampleHeightCentimeters(point.XCentimeters, point.ZCentimeters)),
        WorldUnits.CentimetersToMeters(point.ZCentimeters));

    private GpuMesh Own(GpuMesh mesh)
    {
        _ownedMeshes.Add(mesh);
        return mesh;
    }

    private GpuMaterialAsset Own(GpuMaterialAsset material)
    {
        _ownedMaterials.Add(material);
        return material;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _streaming.Dispose();
        _terrainRendering.Dispose();
        _structureRendering.Dispose();
        foreach (GpuMesh mesh in _ownedMeshes) mesh.Dispose();
        foreach (GpuMaterialAsset material in _ownedMaterials) material.Dispose();
        _ownedMeshes.Clear();
        _ownedMaterials.Clear();
        _cpuChunks.Clear();
    }
}
