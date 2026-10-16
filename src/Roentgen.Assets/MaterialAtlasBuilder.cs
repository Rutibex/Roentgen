using Roentgen.Core;

namespace Roentgen.Assets;

public sealed record AtlasRegion(string MaterialId, int X, int Y, int Width, int Height, AssetVector2 UvMinimum, AssetVector2 UvMaximum);

public sealed record CompiledMaterialAtlas(
    int Width,
    int Height,
    int Padding,
    StateChecksum ContentChecksum,
    IReadOnlyDictionary<string, AtlasRegion> Regions,
    CompiledTextureChannel Albedo,
    CompiledTextureChannel Surface,
    CompiledTextureChannel Emissive);

public sealed class MaterialAtlasBuilder
{
    public CompiledMaterialAtlas Build(IEnumerable<CompiledMaterialAsset> materials, int maximumWidth = 2048, int padding = 2)
    {
        ArgumentNullException.ThrowIfNull(materials);
        if (maximumWidth < 16 || padding < 0) throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        CompiledMaterialAsset[] ordered = materials.OrderBy(static material => material.Id, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) throw new ArgumentException("At least one material is required.", nameof(materials));
        var placements = new List<(CompiledMaterialAsset Material, int X, int Y)>();
        int x = padding; int y = padding; int rowHeight = 0; int usedWidth = 0;
        foreach (CompiledMaterialAsset material in ordered)
        {
            int requiredWidth = material.Albedo.Width + padding * 2;
            int requiredHeight = material.Albedo.Height + padding * 2;
            if (requiredWidth > maximumWidth) throw new InvalidOperationException($"Material '{material.Id}' cannot fit atlas width {maximumWidth}.");
            if (x + material.Albedo.Width + padding > maximumWidth)
            {
                x = padding; y += rowHeight; rowHeight = 0;
            }
            placements.Add((material, x, y));
            x += material.Albedo.Width + padding * 2;
            rowHeight = Math.Max(rowHeight, requiredHeight);
            usedWidth = Math.Max(usedWidth, x);
        }
        int width = NextPowerOfTwo(Math.Min(maximumWidth, Math.Max(16, usedWidth)));
        int height = NextPowerOfTwo(Math.Max(16, y + rowHeight));
        byte[] albedo = NewTexture(width, height, new AssetColor(255, 0, 255));
        byte[] surface = NewTexture(width, height, new AssetColor(255, 0, 128));
        byte[] emissive = NewTexture(width, height, AssetColor.Black);
        var regions = new SortedDictionary<string, AtlasRegion>(StringComparer.Ordinal);
        foreach ((CompiledMaterialAsset material, int px, int py) in placements)
        {
            BlitWithPadding(material.Albedo.Rgba, material.Albedo.Width, material.Albedo.Height, albedo, width, height, px, py, padding);
            BlitWithPadding(material.Surface.Rgba, material.Surface.Width, material.Surface.Height, surface, width, height, px, py, padding);
            BlitWithPadding(material.Emissive.Rgba, material.Emissive.Width, material.Emissive.Height, emissive, width, height, px, py, padding);
            regions.Add(material.Id, new AtlasRegion(
                material.Id, px, py, material.Albedo.Width, material.Albedo.Height,
                new AssetVector2(px / (float)width, py / (float)height),
                new AssetVector2((px + material.Albedo.Width) / (float)width, (py + material.Albedo.Height) / (float)height)));
        }
        var writer = new CanonicalWriter();
        writer.WriteInt32(width); writer.WriteInt32(height); writer.WriteInt32(padding);
        writer.WriteCount(regions.Count);
        foreach ((string id, AtlasRegion region) in regions)
        {
            writer.WriteString(id); writer.WriteInt32(region.X); writer.WriteInt32(region.Y); writer.WriteInt32(region.Width); writer.WriteInt32(region.Height);
        }
        writer.WriteBytes(albedo); writer.WriteBytes(surface); writer.WriteBytes(emissive);
        return new CompiledMaterialAtlas(width, height, padding, writer.ComputeChecksum(), regions,
            new CompiledTextureChannel(width, height, albedo), new CompiledTextureChannel(width, height, surface), new CompiledTextureChannel(width, height, emissive));
    }

    private static byte[] NewTexture(int width, int height, AssetColor color)
    {
        byte[] result = new byte[checked(width * height * 4)];
        for (int offset = 0; offset < result.Length; offset += 4)
        {
            result[offset] = color.R; result[offset + 1] = color.G; result[offset + 2] = color.B; result[offset + 3] = color.A;
        }
        return result;
    }

    private static void BlitWithPadding(byte[] source, int sourceWidth, int sourceHeight, byte[] target, int targetWidth, int targetHeight, int x, int y, int padding)
    {
        for (int dy = -padding; dy < sourceHeight + padding; dy++)
        {
            int sy = Math.Clamp(dy, 0, sourceHeight - 1); int ty = y + dy;
            if (ty < 0 || ty >= targetHeight) continue;
            for (int dx = -padding; dx < sourceWidth + padding; dx++)
            {
                int sx = Math.Clamp(dx, 0, sourceWidth - 1); int tx = x + dx;
                if (tx < 0 || tx >= targetWidth) continue;
                int sourceOffset = (sy * sourceWidth + sx) * 4; int targetOffset = (ty * targetWidth + tx) * 4;
                Buffer.BlockCopy(source, sourceOffset, target, targetOffset, 4);
            }
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value) result = checked(result << 1);
        return result;
    }
}
