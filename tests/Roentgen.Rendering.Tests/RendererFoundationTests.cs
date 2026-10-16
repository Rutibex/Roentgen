using Microsoft.Xna.Framework;
using Roentgen.Contracts;
using Roentgen.Rendering;
using Roentgen.World;

namespace Roentgen.Rendering.Tests;

public sealed class RendererFoundationTests
{
    [Fact]
    public void BoxMeshHasExpectedTopologyAndBounds()
    {
        MeshData mesh = ProceduralMeshFactory.CreateBox("box", new Vector3(2f, 4f, 6f), Color.White);
        Assert.Equal(24, mesh.Vertices.Length);
        Assert.Equal(36, mesh.Indices.Length);
        Assert.Equal(12, mesh.PrimitiveCount);
        Assert.Equal(new Vector3(-1f, -2f, -3f), mesh.LocalBounds.Min);
        Assert.Equal(new Vector3(1f, 2f, 3f), mesh.LocalBounds.Max);
    }

    [Fact]
    public void CylinderMeshBuildsCanonicalTopology()
    {
        MeshData mesh = ProceduralMeshFactory.CreateCylinder("cylinder", 1f, 3f, 12, Color.White);
        Assert.Equal(74, mesh.Vertices.Length);
        Assert.Equal(144, mesh.Indices.Length);
        Assert.Equal(48, mesh.PrimitiveCount);
    }

    [Fact]
    public void TerrainMeshBuildsCanonicalGridTopology()
    {
        MeshData mesh = ProceduralMeshFactory.CreateTerrainGrid("terrain", 4, 3, 2f, static (x, z) => x + z);
        Assert.Equal(20, mesh.Vertices.Length);
        Assert.Equal(72, mesh.Indices.Length);
        Assert.Equal(24, mesh.PrimitiveCount);
    }

    [Fact]
    public void MeshRejectsOutOfRangeIndices()
    {
        RoentgenVertex[] vertices =
        [
            new(Vector3.Zero, Vector3.Up, Vector2.Zero, Color.White),
            new(Vector3.Right, Vector3.Up, Vector2.UnitX, Color.White),
            new(Vector3.Forward, Vector3.Up, Vector2.UnitY, Color.White)
        ];
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeshData("bad", vertices, [0, 1, 3]));
    }

    [Fact]
    public void CameraMatricesProduceFrustum()
    {
        var options = new RenderingOptions();
        CameraState camera = CameraState.CreateDefault(16f / 9f, options);
        CameraMatrices matrices = camera.ToMatrices(16f / 9f);
        Assert.NotEqual(Matrix.Identity, matrices.View);
        Assert.NotEqual(Matrix.Identity, matrices.Projection);
        Assert.NotNull(matrices.Frustum);
    }

    [Fact]
    public void RenderingOptionsRejectInvalidShadowMapSize()
    {
        var options = new RenderingOptions { ShadowMapSize = 1000 };
        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void RenderThreadGuardRejectsForeignThread()
    {
        var guard = new RenderThreadGuard();
        guard.BindCurrentThread();
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { guard.AssertAccess(); }
            catch (Exception exception) { captured = exception; }
        });
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(captured);
    }
    [Fact]
    public void BoundedUploadQueueRejectsOperationAndByteOverflow()
    {
        var queue = new RenderResourceUploadQueue(maximumPendingOperations: 2, maximumPendingBytes: 100);
        Assert.True(queue.TryEnqueue(new RenderUploadRequest("a", 40, RenderUploadPriority.WorldGeometry, 1, _ => { })));
        Assert.True(queue.TryEnqueue(new RenderUploadRequest("b", 40, RenderUploadPriority.WorldGeometry, 1, _ => { })));
        Assert.False(queue.TryEnqueue(new RenderUploadRequest("operation-overflow", 1, RenderUploadPriority.NearTerrain, 1, _ => { })));
        Assert.Equal(2, queue.PendingCount);
        Assert.Equal(80, queue.PendingBytes);
        Assert.Equal(1, queue.Snapshot.RejectedCount);

        var byteQueue = new RenderResourceUploadQueue(maximumPendingOperations: 4, maximumPendingBytes: 50);
        Assert.True(byteQueue.TryEnqueue(new RenderUploadRequest("c", 40, RenderUploadPriority.WorldGeometry, 1, _ => { })));
        Assert.False(byteQueue.TryEnqueue(new RenderUploadRequest("byte-overflow", 20, RenderUploadPriority.WorldGeometry, 1, _ => { })));
    }

    [Fact]
    public void TerrainBridgeBuildsCanonicalChunkMesh()
    {
        RegionalTopology topology = new RegionalTopologyGenerator(new CampaignSeed(0x524F454E5447454EUL)).Generate(WorldGenerationProfile.MicroRegion);
        TerrainChunkData chunk = new TerrainChunkCompiler(topology).Compile(new TerrainChunkAddress(0, 0));
        MeshData grid = TerrainRenderingBridge.ToMeshData(chunk, 1, includeSkirts: false);
        Assert.Equal(33 * 33, grid.Vertices.Length);
        Assert.Equal(32 * 32 * 6, grid.Indices.Length);
        Assert.Equal(32 * 32 * 2, grid.PrimitiveCount);

        MeshData skirted = TerrainRenderingBridge.ToMeshData(chunk, 1);
        Assert.Equal(33 * 33 + 33 * 4, skirted.Vertices.Length);
        Assert.Equal(32 * 32 * 6 + 4 * 32 * 6, skirted.Indices.Length);
        Assert.Equal(32 * 32 * 2 + 4 * 32 * 2, skirted.PrimitiveCount);
        Assert.True(TerrainRenderingBridge.EstimateGpuUploadBytes(skirted) > TerrainRenderingBridge.EstimateGpuUploadBytes(grid));
    }

}
