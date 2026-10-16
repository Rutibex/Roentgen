using Microsoft.Xna.Framework.Graphics;
using Roentgen.Animation;
using Roentgen.Contracts;

namespace Roentgen.Rendering;

public sealed class CharacterGpuMeshPair : IDisposable
{
    public CharacterGpuMeshPair(GpuSkinnedMesh skinned, DynamicCpuSkinnedMesh fallback)
    {
        Skinned = skinned;
        Fallback = fallback;
    }

    public GpuSkinnedMesh Skinned { get; }
    public DynamicCpuSkinnedMesh Fallback { get; }

    public void Dispose()
    {
        Fallback.Dispose();
        Skinned.Dispose();
    }
}

public sealed class CharacterGpuLod : IDisposable
{
    public CharacterGpuLod(CharacterGpuMeshPair body, CharacterGpuMeshPair? clothing, int lodLevel)
    {
        Body = body;
        Clothing = clothing;
        LodLevel = lodLevel;
    }

    public CharacterGpuMeshPair Body { get; }
    public CharacterGpuMeshPair? Clothing { get; }
    public int LodLevel { get; }

    public void Dispose()
    {
        Clothing?.Dispose();
        Body.Dispose();
    }
}

public sealed class CharacterGpuAsset : IDisposable
{
    public CharacterGpuAsset(StableId id, GeneratedCharacterProduct source, IReadOnlyList<CharacterGpuLod> lods)
    {
        Id = id;
        Source = source;
        Lods = lods.OrderBy(static value => value.LodLevel).ToArray();
    }

    public StableId Id { get; }
    public GeneratedCharacterProduct Source { get; }
    public IReadOnlyList<CharacterGpuLod> Lods { get; }

    public CharacterGpuLod SelectLod(CharacterLodLevel level)
    {
        int requested = Math.Clamp((int)level, 0, Lods.Count - 1);
        return Lods[requested];
    }

    public static CharacterGpuAsset Upload(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, GeneratedCharacterProduct source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lods = new List<CharacterGpuLod>();
        try
        {
            foreach (CharacterLodProduct lod in source.Lods.OrderBy(static value => value.LodLevel))
            {
                CharacterGpuMeshPair body = UploadPair(graphicsDevice, threadGuard, lod.Body);
                try
                {
                    CharacterGpuMeshPair? clothing = lod.Clothing is null ? null : UploadPair(graphicsDevice, threadGuard, lod.Clothing);
                    lods.Add(new CharacterGpuLod(body, clothing, lod.LodLevel));
                }
                catch
                {
                    body.Dispose();
                    throw;
                }
            }

            return new CharacterGpuAsset(source.CharacterId, source, lods);
        }
        catch
        {
            foreach (CharacterGpuLod lod in lods)
            {
                lod.Dispose();
            }

            throw;
        }
    }

    private static CharacterGpuMeshPair UploadPair(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, CharacterMeshProduct mesh)
    {
        GpuSkinnedMesh skinned = GpuSkinnedMesh.Upload(graphicsDevice, threadGuard, mesh);
        try
        {
            return new CharacterGpuMeshPair(skinned, new DynamicCpuSkinnedMesh(graphicsDevice, threadGuard, mesh));
        }
        catch
        {
            skinned.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (CharacterGpuLod lod in Lods)
        {
            lod.Dispose();
        }
    }
}
