using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

public sealed class GpuMesh : IDisposable
{
    private bool _disposed;
    private readonly RenderThreadGuard _threadGuard;

    private GpuMesh(string name, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int primitiveCount, BoundingBox localBounds, RenderThreadGuard threadGuard)
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
    public bool IsDisposed => _disposed;

    public static GpuMesh Upload(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, MeshData data)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(threadGuard);
        ArgumentNullException.ThrowIfNull(data);
        threadGuard.AssertAccess();

        var vertexBuffer = new VertexBuffer(graphicsDevice, RoentgenVertex.VertexDeclaration, data.Vertices.Length, BufferUsage.WriteOnly);
        var indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, data.Indices.Length, BufferUsage.WriteOnly);
        try
        {
            vertexBuffer.SetData(data.Vertices);
            indexBuffer.SetData(data.Indices);
            return new GpuMesh(data.Name, vertexBuffer, indexBuffer, data.PrimitiveCount, data.LocalBounds, threadGuard);
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
        if (_disposed) return;
        _threadGuard.AssertAccess();
        _disposed = true;
        IndexBuffer.Dispose();
        VertexBuffer.Dispose();
    }
}
