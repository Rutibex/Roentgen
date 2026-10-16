using Roentgen.Core;

namespace Roentgen.Assets;

public static class AssetBinaryPayload
{
    public static byte[] Write(CompiledMeshAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var writer = new CanonicalWriter();
        writer.WriteString("RGASSET-MESH");
        writer.WriteInt32(1);
        writer.WriteChecksum(asset.ContentChecksum);
        asset.WriteCanonical(writer);
        return writer.ToArray();
    }

    public static byte[] Write(CompiledMaterialAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var writer = new CanonicalWriter();
        writer.WriteString("RGASSET-MATERIAL");
        writer.WriteInt32(1);
        writer.WriteChecksum(asset.ContentChecksum);
        asset.WriteCanonical(writer);
        return writer.ToArray();
    }
}
