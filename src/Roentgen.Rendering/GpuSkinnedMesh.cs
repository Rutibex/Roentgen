using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roentgen.Animation;

namespace Roentgen.Rendering;

public sealed class GpuSkinnedMesh : IDisposable
{
    private readonly RenderThreadGuard _threadGuard;
    private bool _disposed;

    private GpuSkinnedMesh(
        string name,
        VertexBuffer vertexBuffer,
        IndexBuffer indexBuffer,
        int primitiveCount,
        BoundingBox localBounds,
        RenderThreadGuard threadGuard)
    {
        Name = name;
        VertexBuffer = vertexBuffer;
        IndexBuffer = indexBuffer;
        PrimitiveCount = primitiveCount;
        LocalBounds = localBounds;
        _threadGuard = threadGuard;
    }

    public string Name { get; }
    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public int PrimitiveCount { get; }
    public BoundingBox LocalBounds { get; }

    public static GpuSkinnedMesh Upload(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, CharacterMeshProduct mesh)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(threadGuard);
        ArgumentNullException.ThrowIfNull(mesh);
        threadGuard.AssertAccess();
        SkinnedVertex[] vertices = CharacterRenderingBridge.ToSkinnedVertices(mesh);
        var vertexBuffer = new VertexBuffer(graphicsDevice, SkinnedVertex.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        var indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, mesh.Indices.Length, BufferUsage.WriteOnly);
        try
        {
            vertexBuffer.SetData(vertices);
            indexBuffer.SetData(mesh.Indices);
            return new GpuSkinnedMesh(mesh.Name, vertexBuffer, indexBuffer, mesh.PrimitiveCount, CharacterRenderingBridge.ToBoundingBox(mesh.Bounds), threadGuard);
        }
        catch
        {
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            throw;
        }
    }

    public void Bind(GraphicsDevice graphicsDevice)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        graphicsDevice.SetVertexBuffer(VertexBuffer);
        graphicsDevice.Indices = IndexBuffer;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _threadGuard.AssertAccess();
        _disposed = true;
        IndexBuffer.Dispose();
        VertexBuffer.Dispose();
    }
}
