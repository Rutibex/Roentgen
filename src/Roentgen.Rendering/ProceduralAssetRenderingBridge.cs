using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roentgen.Assets;

namespace Roentgen.Rendering;

public sealed class GpuMaterialAsset : IDisposable
{
    private readonly RenderThreadGuard _threadGuard;
    private bool _disposed;

    internal GpuMaterialAsset(CompiledMaterialAsset source, Texture2D albedoTexture, MaterialDefinition material, RenderThreadGuard threadGuard)
    {
        Source = source;
        AlbedoTexture = albedoTexture;
        Material = material;
        _threadGuard = threadGuard;
    }

    public CompiledMaterialAsset Source { get; }
    public Texture2D AlbedoTexture { get; }
    public MaterialDefinition Material { get; }
    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _threadGuard.AssertAccess();
        _disposed = true;
        AlbedoTexture.Dispose();
    }
}

public static class ProceduralAssetRenderingBridge
{
    public static MeshData ToMeshData(CompiledMeshAsset asset, int lod = 0)
    {
        ArgumentNullException.ThrowIfNull(asset);
        CompiledMeshLod source = asset.Lods.SingleOrDefault(item => item.Level == lod)
            ?? throw new ArgumentOutOfRangeException(nameof(lod), $"Asset '{asset.Id}' has no LOD {lod}.");
        RoentgenVertex[] vertices = source.Vertices.Select(static vertex => new RoentgenVertex(
            ToXna(vertex.Position),
            ToXna(vertex.Normal),
            ToXna(vertex.TextureCoordinate),
            ToXna(vertex.Color))).ToArray();
        return new MeshData($"{asset.Id}.lod{lod}", vertices, source.Indices);
    }

    internal static GpuMaterialAsset UploadMaterial(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, CompiledMaterialAsset asset)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(threadGuard);
        ArgumentNullException.ThrowIfNull(asset);
        threadGuard.AssertAccess();
        var texture = new Texture2D(graphicsDevice, asset.Albedo.Width, asset.Albedo.Height, false, SurfaceFormat.Color);
        try
        {
            texture.SetData(ToColors(asset.Albedo.Rgba));
            var definition = new MaterialDefinition(asset.Id, Color.White, asset.Roughness, asset.Metallic, asset.EmissiveStrength, texture);
            return new GpuMaterialAsset(asset, texture, definition, threadGuard);
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    internal static Texture2D UploadTexture(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, CompiledTextureChannel channel)
    {
        threadGuard.AssertAccess();
        var texture = new Texture2D(graphicsDevice, channel.Width, channel.Height, false, SurfaceFormat.Color);
        try { texture.SetData(ToColors(channel.Rgba)); return texture; }
        catch { texture.Dispose(); throw; }
    }

    private static Color[] ToColors(byte[] rgba)
    {
        if (rgba.Length % 4 != 0) throw new ArgumentException("RGBA payload length must be divisible by four.", nameof(rgba));
        var result = new Color[rgba.Length / 4];
        for (int index = 0; index < result.Length; index++)
        {
            int offset = index * 4;
            result[index] = new Color(rgba[offset], rgba[offset + 1], rgba[offset + 2], rgba[offset + 3]);
        }
        return result;
    }

    private static Vector3 ToXna(AssetVector3 value) => new(value.X, value.Y, value.Z);
    private static Vector2 ToXna(AssetVector2 value) => new(value.X, value.Y);
    private static Color ToXna(AssetColor value) => new(value.R, value.G, value.B, value.A);
}
