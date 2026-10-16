using Microsoft.Xna.Framework;
using Roentgen.Rendering;

namespace Roentgen.App;

internal sealed class RendererSmokeScene : IDisposable
{
    private readonly List<GpuMesh> _ownedMeshes = [];
    private readonly List<RenderItem> _items = [];

    public RendererSmokeScene(RendererFoundation renderer)
    {
        GpuMesh terrain = Own(renderer.UploadNow(ProceduralMeshFactory.CreateTerrainGrid(
            "phase4-terrain",
            64,
            64,
            2f,
            static (x, z) => MathF.Sin(x * 0.19f) * 1.4f + MathF.Cos(z * 0.13f) * 1.1f + MathF.Sin((x + z) * 0.055f) * 2.2f,
            static (x, z) => ((x + z) & 3) == 0 ? new Color(126, 112, 83) : new Color(149, 132, 94))));
        GpuMesh box = Own(renderer.UploadNow(ProceduralMeshFactory.CreateBox("phase4-box", Vector3.One, Color.White)));
        GpuMesh tower = Own(renderer.UploadNow(ProceduralMeshFactory.CreateCylinder("phase4-tower", 1f, 1f, 12, Color.White)));
        var earth = new MaterialDefinition("earth", new Color(175, 154, 111), 0.95f);
        var concrete = new MaterialDefinition("concrete", new Color(147, 151, 146), 0.9f);
        var rust = new MaterialDefinition("rusted-steel", new Color(136, 72, 44), 0.7f, 0.35f);
        var phosphor = new MaterialDefinition("diagnostic-phosphor", new Color(80, 190, 157), 0.25f, 0.15f, 0.22f);
        _items.Add(new RenderItem("terrain", terrain, earth, Matrix.Identity, true, true));

        for (int index = 0; index < 18; index++)
        {
            float angle = MathHelper.TwoPi * index / 18f;
            float radius = 18f + (index % 3) * 4f;
            Vector3 position = new(MathF.Sin(angle) * radius, 2.1f + (index % 4) * 0.5f, MathF.Cos(angle) * radius);
            Vector3 scale = new(2.4f + index % 3, 4f + index % 5, 2.2f + (index + 1) % 3);
            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateRotationY(angle * 0.6f) * Matrix.CreateTranslation(position);
            _items.Add(new RenderItem($"ruin-{index:00}", box, index % 4 == 0 ? rust : concrete, world));
        }

        Matrix towerWorld = Matrix.CreateScale(2.4f, 17f, 2.4f) * Matrix.CreateTranslation(-15f, 9f, -18f);
        _items.Add(new RenderItem("relay-tower", tower, rust, towerWorld));
        Matrix beaconWorld = Matrix.CreateScale(1.0f, 2f, 1.0f) * Matrix.CreateTranslation(-15f, 27f, -18f);
        _items.Add(new RenderItem("relay-beacon", tower, phosphor, beaconWorld, true, true));
    }

    public RenderFrameSnapshot Build(long tick, CameraState camera, bool showDebug)
    {
        IReadOnlyList<DebugLine> lines = showDebug ? CreateDebugLines() : Array.Empty<DebugLine>();
        return new RenderFrameSnapshot(
            tick,
            camera,
            _items,
            new DirectionalLight(Vector3.Normalize(new Vector3(-0.45f, -1f, -0.3f)), new Color(255, 238, 202), 1.1f),
            new FogSettings(new Color(72, 82, 78), 80f, 420f),
            new Color(50, 61, 62),
            lines);
    }

    private static IReadOnlyList<DebugLine> CreateDebugLines()
    {
        var lines = new List<DebugLine>();
        for (int value = -32; value <= 32; value += 4)
        {
            Color color = value == 0 ? new Color(90, 210, 178) : new Color(90, 100, 96);
            lines.Add(new DebugLine(new Vector3(value, 0.05f, -32f), new Vector3(value, 0.05f, 32f), color));
            lines.Add(new DebugLine(new Vector3(-32f, 0.05f, value), new Vector3(32f, 0.05f, value), color));
        }

        lines.Add(new DebugLine(Vector3.Zero, Vector3.Right * 5f, Color.Red));
        lines.Add(new DebugLine(Vector3.Zero, Vector3.Up * 5f, Color.Green));
        lines.Add(new DebugLine(Vector3.Zero, Vector3.Forward * 5f, Color.Blue));
        return lines;
    }

    private GpuMesh Own(GpuMesh mesh)
    {
        _ownedMeshes.Add(mesh);
        return mesh;
    }

    public void Dispose()
    {
        foreach (GpuMesh mesh in _ownedMeshes) mesh.Dispose();
        _ownedMeshes.Clear();
    }
}
