using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roentgen.Animation;

namespace Roentgen.Rendering;

public sealed class DynamicCpuSkinnedMesh : IDisposable
{
    private readonly CharacterMeshProduct _source;
    private readonly DynamicVertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly RenderThreadGuard _threadGuard;
    private readonly RoentgenVertex[] _vertices;
    private bool _disposed;

    public DynamicCpuSkinnedMesh(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, CharacterMeshProduct source)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(threadGuard);
        ArgumentNullException.ThrowIfNull(source);
        threadGuard.AssertAccess();
        _source = source;
        _threadGuard = threadGuard;
        _vertices = new RoentgenVertex[source.Vertices.Length];
        _vertexBuffer = new DynamicVertexBuffer(graphicsDevice, RoentgenVertex.VertexDeclaration, _vertices.Length, BufferUsage.WriteOnly);
        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, source.Indices.Length, BufferUsage.WriteOnly);
        _indexBuffer.SetData(source.Indices);
        PrimitiveCount = source.PrimitiveCount;
        LocalBounds = CharacterRenderingBridge.ToBoundingBox(source.Bounds);
    }

    public int PrimitiveCount { get; }
    public BoundingBox LocalBounds { get; }

    public void Update(PoseBuffer pose)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _threadGuard.AssertAccess();
        SkinnedCharacterVertex[] skinned = CpuSkinner.Skin(_source, pose.BuildSkinMatrices());
        for (int index = 0; index < skinned.Length; index++)
        {
            SkinnedCharacterVertex vertex = skinned[index];
            _vertices[index] = new RoentgenVertex(
                CharacterRenderingBridge.ToXna(vertex.Position),
                CharacterRenderingBridge.ToXna(vertex.Normal),
                CharacterRenderingBridge.ToXna(vertex.TextureCoordinate),
                CharacterRenderingBridge.ToColor(vertex.Color));
        }

        _vertexBuffer.SetData(_vertices, 0, _vertices.Length, SetDataOptions.Discard);
    }

    public void Bind(GraphicsDevice graphicsDevice)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        graphicsDevice.SetVertexBuffer(_vertexBuffer);
        graphicsDevice.Indices = _indexBuffer;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _threadGuard.AssertAccess();
        _disposed = true;
        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();
    }
}
