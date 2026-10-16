namespace Roentgen.Assets;

public static class CollisionProxyBuilder
{
    public static CollisionProxyAsset Build(CollisionProxyKind kind, AssetBounds bounds, IReadOnlyList<AssetBounds> partBounds)
    {
        AssetVector3 size = bounds.Size;
        float radius = MathF.Max(size.X, size.Z) * 0.5f;
        float height = size.Y;
        IReadOnlyList<AssetBounds> boxes = kind == CollisionProxyKind.CompoundBoxes
            ? MergeContainedBoxes(partBounds)
            : Array.Empty<AssetBounds>();
        return new CollisionProxyAsset(kind, bounds, radius, height, boxes);
    }

    private static IReadOnlyList<AssetBounds> MergeContainedBoxes(IReadOnlyList<AssetBounds> source)
    {
        var result = new List<AssetBounds>();
        foreach (AssetBounds candidate in source)
        {
            bool contained = source.Any(other => other != candidate && Contains(other, candidate));
            if (!contained) result.Add(candidate);
        }
        return result.OrderBy(static item => item.Minimum.X)
            .ThenBy(static item => item.Minimum.Y)
            .ThenBy(static item => item.Minimum.Z)
            .ToArray();
    }

    private static bool Contains(AssetBounds outer, AssetBounds inner) =>
        outer.Minimum.X <= inner.Minimum.X && outer.Minimum.Y <= inner.Minimum.Y && outer.Minimum.Z <= inner.Minimum.Z &&
        outer.Maximum.X >= inner.Maximum.X && outer.Maximum.Y >= inner.Maximum.Y && outer.Maximum.Z >= inner.Maximum.Z;
}
