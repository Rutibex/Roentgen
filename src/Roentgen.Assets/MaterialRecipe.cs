using Roentgen.Core;

namespace Roentgen.Assets;

public sealed record MaterialLayerRecipe(
    string Id,
    MaterialLayerKind Kind,
    MaterialChannel Channel,
    AssetColor Primary,
    AssetColor Secondary,
    float Opacity,
    float Scale,
    float Threshold,
    float AngleDegrees,
    ulong Seed) : ICanonicalWritable
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        if (!float.IsFinite(Opacity) || Opacity is < 0f or > 1f || !float.IsFinite(Scale) || Scale <= 0f ||
            !float.IsFinite(Threshold) || !float.IsFinite(AngleDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(Opacity));
        }
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteInt32((int)Kind);
        writer.WriteInt32((int)Channel);
        Primary.WriteCanonical(writer);
        Secondary.WriteCanonical(writer);
        writer.WriteSingle(Opacity);
        writer.WriteSingle(Scale);
        writer.WriteSingle(Threshold);
        writer.WriteSingle(AngleDegrees);
        writer.WriteUInt64(Seed);
    }
}

public sealed record MaterialRecipe(
    string Id,
    int SchemaVersion,
    int Width,
    int Height,
    float Roughness,
    float Metallic,
    float EmissiveStrength,
    bool Tileable,
    IReadOnlyList<MaterialLayerRecipe> Layers,
    IReadOnlyDictionary<string, string> Metadata) : ICanonicalWritable
{
    public const int CurrentSchemaVersion = 1;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentNullException.ThrowIfNull(Layers);
        ArgumentNullException.ThrowIfNull(Metadata);
        if (SchemaVersion != CurrentSchemaVersion || Width is < 4 or > 2048 || Height is < 4 or > 2048 ||
            (Width & (Width - 1)) != 0 || (Height & (Height - 1)) != 0 ||
            !float.IsFinite(Roughness) || Roughness is < 0f or > 1f ||
            !float.IsFinite(Metallic) || Metallic is < 0f or > 1f ||
            !float.IsFinite(EmissiveStrength) || EmissiveStrength < 0f || Layers.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MaterialLayerRecipe layer in Layers)
        {
            ArgumentNullException.ThrowIfNull(layer);
            layer.Validate();
            if (!ids.Add(layer.Id)) throw new InvalidOperationException($"Duplicate material layer ID '{layer.Id}'.");
        }

        foreach ((string key, string value) in Metadata)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
        }
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteInt32(SchemaVersion);
        writer.WriteInt32(Width);
        writer.WriteInt32(Height);
        writer.WriteSingle(Roughness);
        writer.WriteSingle(Metallic);
        writer.WriteSingle(EmissiveStrength);
        writer.WriteBoolean(Tileable);
        writer.WriteCount(Layers.Count);
        foreach (MaterialLayerRecipe layer in Layers) layer.WriteCanonical(writer);
        writer.WriteCount(Metadata.Count);
        foreach ((string key, string value) in Metadata.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteString(key);
            writer.WriteString(value);
        }
    }
}
