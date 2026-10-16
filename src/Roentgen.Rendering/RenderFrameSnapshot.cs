using Microsoft.Xna.Framework;

namespace Roentgen.Rendering;

public sealed class RenderFrameSnapshot
{
    public RenderFrameSnapshot(
        long simulationTick,
        CameraState camera,
        IReadOnlyList<RenderItem> opaqueItems,
        DirectionalLight sun,
        FogSettings fog,
        Color clearColor,
        IReadOnlyList<DebugLine>? debugLines = null,
        IReadOnlyList<CharacterRenderItem>? characterItems = null)
    {
        if (simulationTick < 0) throw new ArgumentOutOfRangeException(nameof(simulationTick));
        ArgumentNullException.ThrowIfNull(opaqueItems);
        simulationTick = Math.Max(0, simulationTick);
        fog.Validate();
        SimulationTick = simulationTick;
        Camera = camera;
        OpaqueItems = opaqueItems.ToArray();
        Sun = sun.Normalized();
        Fog = fog;
        ClearColor = clearColor;
        DebugLines = debugLines is null ? Array.Empty<DebugLine>() : debugLines.ToArray();
        CharacterItems = characterItems is null ? Array.Empty<CharacterRenderItem>() : characterItems.ToArray();
    }

    public long SimulationTick { get; }
    public CameraState Camera { get; }
    public IReadOnlyList<RenderItem> OpaqueItems { get; }
    public DirectionalLight Sun { get; }
    public FogSettings Fog { get; }
    public Color ClearColor { get; }
    public IReadOnlyList<DebugLine> DebugLines { get; }
    public IReadOnlyList<CharacterRenderItem> CharacterItems { get; }
}

public readonly record struct DebugLine(Vector3 Start, Vector3 End, Color Color);
