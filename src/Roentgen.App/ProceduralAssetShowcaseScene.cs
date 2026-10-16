using Microsoft.Xna.Framework;
using Roentgen.Assets;
using Roentgen.Rendering;

namespace Roentgen.App;

internal sealed class ProceduralAssetShowcaseScene : IDisposable
{
    private readonly List<GpuMesh> _meshes = [];
    private readonly List<GpuMaterialAsset> _materials = [];
    private readonly List<RenderItem> _items = [];

    public ProceduralAssetShowcaseScene(RendererFoundation renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        var meshCompiler = new MeshRecipeCompiler();
        var materialCompiler = new MaterialRecipeCompiler();
        var gpuMaterials = new Dictionary<string, GpuMaterialAsset>(StringComparer.Ordinal);
        foreach ((string id, MaterialRecipe recipe) in ProceduralAssetCatalog.Materials)
        {
            GpuMaterialAsset material = Own(renderer.UploadNow(materialCompiler.Compile(recipe)));
            gpuMaterials.Add(id, material);
        }

        GpuMesh terrain = Own(renderer.UploadNow(ProceduralMeshFactory.CreateTerrainGrid(
            "phase5-terrain", 64, 64, 2f,
            static (x, z) => MathF.Sin(x * 0.17f) * 1.2f + MathF.Cos(z * 0.11f) * 0.8f + MathF.Sin((x + z) * 0.047f) * 1.8f,
            static (x, z) => ((x + z) & 3) == 0 ? new Color(119, 108, 84) : new Color(144, 128, 94))));
        _items.Add(new RenderItem("terrain", terrain, new MaterialDefinition("earth", new Color(166, 145, 105), 0.96f), Matrix.Identity));

        AddAsset(renderer, meshCompiler, "water-tower", gpuMaterials["rusted-steel"].Material, Matrix.CreateTranslation(-18f, 0f, -18f));
        AddAsset(renderer, meshCompiler, "diagnostic-terminal", gpuMaterials["diagnostic-phosphor"].Material, Matrix.CreateScale(2.2f) * Matrix.CreateTranslation(4f, 2f, -10f));
        AddAsset(renderer, meshCompiler, "scrap-rifle", gpuMaterials["rusted-steel"].Material, Matrix.CreateScale(4f) * Matrix.CreateRotationY(-0.45f) * Matrix.CreateTranslation(4f, 3f, -3f));
        AddAsset(renderer, meshCompiler, "dead-tree", gpuMaterials["aged-wood"].Material, Matrix.CreateScale(1.7f) * Matrix.CreateTranslation(18f, 0f, -13f));

        CompiledMeshAsset barrierAsset = meshCompiler.Compile(ProceduralAssetCatalog.Meshes["concrete-barrier"]);
        GpuMesh barrierLod0 = Own(renderer.UploadNow(barrierAsset, 0));
        GpuMesh barrierLod1 = Own(renderer.UploadNow(barrierAsset, Math.Min(1, barrierAsset.Lods.Count - 1)));
        for (int index = 0; index < 14; index++)
        {
            float x = -14f + index * 2.1f;
            float z = 9f + MathF.Sin(index * 0.65f) * 1.4f;
            GpuMesh mesh = index < 7 ? barrierLod0 : barrierLod1;
            Matrix world = Matrix.CreateRotationY(0.08f * MathF.Sin(index)) * Matrix.CreateTranslation(x, 0.6f, z);
            _items.Add(new RenderItem($"barrier-{index:00}", mesh, gpuMaterials["weathered-concrete"].Material, world));
        }
    }

    public RenderFrameSnapshot Build(long tick, CameraState camera, bool showDebug)
    {
        IReadOnlyList<DebugLine> lines = showDebug ? CreateDebugLines() : Array.Empty<DebugLine>();
        return new RenderFrameSnapshot(
            tick,
            camera,
            _items,
            new DirectionalLight(Vector3.Normalize(new Vector3(-0.45f, -1f, -0.3f)), new Color(255, 238, 202), 1.12f),
            new FogSettings(new Color(68, 78, 74), 80f, 420f),
            new Color(47, 58, 59),
            lines);
    }

    private void AddAsset(RendererFoundation renderer, MeshRecipeCompiler compiler, string id, MaterialDefinition material, Matrix world)
    {
        CompiledMeshAsset asset = compiler.Compile(ProceduralAssetCatalog.Meshes[id]);
        GpuMesh mesh = Own(renderer.UploadNow(asset));
        _items.Add(new RenderItem(id, mesh, material, world));
    }

    private static IReadOnlyList<DebugLine> CreateDebugLines()
    {
        var lines = new List<DebugLine>();
        for (int value = -32; value <= 32; value += 4)
        {
            Color color = value == 0 ? new Color(82, 210, 169) : new Color(84, 96, 92);
            lines.Add(new DebugLine(new Vector3(value, 0.05f, -32f), new Vector3(value, 0.05f, 32f), color));
            lines.Add(new DebugLine(new Vector3(-32f, 0.05f, value), new Vector3(32f, 0.05f, value), color));
        }
        return lines;
    }

    private GpuMesh Own(GpuMesh mesh) { _meshes.Add(mesh); return mesh; }
    private GpuMaterialAsset Own(GpuMaterialAsset material) { _materials.Add(material); return material; }

    public void Dispose()
    {
        foreach (GpuMesh mesh in _meshes) mesh.Dispose();
        foreach (GpuMaterialAsset material in _materials) material.Dispose();
        _meshes.Clear(); _materials.Clear();
    }
}
