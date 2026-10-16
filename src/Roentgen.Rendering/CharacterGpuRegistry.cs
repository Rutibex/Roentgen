using Roentgen.Animation;
using Roentgen.Contracts;

namespace Roentgen.Rendering;

public sealed class CharacterGpuRegistry : IDisposable
{
    private readonly SortedDictionary<StableId, CharacterGpuAsset> _assets = [];

    public CharacterGpuAsset UploadNow(RendererFoundation renderer, GeneratedCharacterProduct product)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(product);
        if (_assets.Remove(product.CharacterId, out CharacterGpuAsset? existing))
        {
            existing.Dispose();
        }

        CharacterGpuAsset asset = renderer.UploadCharacterNow(product);
        _assets.Add(product.CharacterId, asset);
        return asset;
    }

    public bool TryGet(StableId id, out CharacterGpuAsset? asset) => _assets.TryGetValue(id, out asset);

    public void Dispose()
    {
        foreach (CharacterGpuAsset asset in _assets.Values)
        {
            asset.Dispose();
        }

        _assets.Clear();
    }
}
