using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

internal sealed class DebugLineRenderer : IDisposable
{
    private readonly BasicEffect _effect;
    private VertexPositionColor[] _vertices = [];

    public DebugLineRenderer(GraphicsDevice graphicsDevice)
    {
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = false
        };
    }

    public void Draw(GraphicsDevice graphicsDevice, IReadOnlyList<DebugLine> lines, CameraMatrices camera)
    {
        if (lines.Count == 0) return;
        int vertexCount = checked(lines.Count * 2);
        if (_vertices.Length < vertexCount) _vertices = new VertexPositionColor[vertexCount];
        for (int index = 0; index < lines.Count; index++)
        {
            DebugLine line = lines[index];
            _vertices[index * 2] = new VertexPositionColor(line.Start, line.Color);
            _vertices[index * 2 + 1] = new VertexPositionColor(line.End, line.Color);
        }

        _effect.World = Microsoft.Xna.Framework.Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _vertices, 0, lines.Count);
        }
    }

    public void Dispose() => _effect.Dispose();
}
