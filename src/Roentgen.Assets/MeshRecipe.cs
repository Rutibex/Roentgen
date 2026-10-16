using Roentgen.Core;

namespace Roentgen.Assets;

public sealed record AssetTransform(
    AssetVector3 Translation,
    AssetVector3 RotationDegrees,
    AssetVector3 Scale) : ICanonicalWritable
{
    public static AssetTransform Identity { get; } = new(AssetVector3.Zero, AssetVector3.Zero, AssetVector3.One);

    public void Validate()
    {
        ValidateFinite(Translation, nameof(Translation));
        ValidateFinite(RotationDegrees, nameof(RotationDegrees));
        ValidateFinite(Scale, nameof(Scale));
        if (Scale.X <= 0f || Scale.Y <= 0f || Scale.Z <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Scale), "Transform scale must be positive on every axis.");
        }
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        Translation.WriteCanonical(writer);
        RotationDegrees.WriteCanonical(writer);
        Scale.WriteCanonical(writer);
    }

    private static void ValidateFinite(AssetVector3 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(name, "Transform values must be finite.");
        }
    }
}

public sealed record MeshModifierRecipe(
    MeshModifierKind Kind,
    AssetVector3 Vector,
    float Amount = 0f,
    float Secondary = 0f,
    ulong Seed = 0) : ICanonicalWritable
{
    public void Validate()
    {
        if (!float.IsFinite(Amount) || !float.IsFinite(Secondary) ||
            !float.IsFinite(Vector.X) || !float.IsFinite(Vector.Y) || !float.IsFinite(Vector.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Modifier values must be finite.");
        }

        if (Kind == MeshModifierKind.Scale && (Vector.X <= 0f || Vector.Y <= 0f || Vector.Z <= 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(Vector), "Scale modifier values must be positive on every axis.");
        }

        if (Kind == MeshModifierKind.NoiseDisplace && Secondary <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Secondary), "Noise displacement frequency must be positive.");
        }
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)Kind);
        Vector.WriteCanonical(writer);
        writer.WriteSingle(Amount);
        writer.WriteSingle(Secondary);
        writer.WriteUInt64(Seed);
    }
}

public sealed record MeshPartRecipe(
    string Id,
    MeshPrimitiveKind Primitive,
    AssetVector3 Size,
    float Radius,
    float Height,
    int Segments,
    int Rings,
    IReadOnlyList<AssetVector2> Profile,
    IReadOnlyList<AssetVector2> Polygon,
    IReadOnlyList<AssetVector3> Path,
    AssetTransform Transform,
    AssetColor Color,
    int MaterialSlot,
    int MinimumLod,
    UvProjectionKind UvProjection,
    IReadOnlyList<MeshModifierRecipe> Modifiers) : ICanonicalWritable
{
    public static MeshPartRecipe Box(string id, AssetVector3 size, AssetColor color, int materialSlot = 0, int minimumLod = 0) =>
        new(id, MeshPrimitiveKind.Box, size, 0f, 0f, 4, 2, [], [], [], AssetTransform.Identity, color, materialSlot, minimumLod, UvProjectionKind.Primitive, []);

    public static MeshPartRecipe Cylinder(string id, float radius, float height, int segments, AssetColor color, int materialSlot = 0, int minimumLod = 0) =>
        new(id, MeshPrimitiveKind.Cylinder, AssetVector3.One, radius, height, segments, 2, [], [], [], AssetTransform.Identity, color, materialSlot, minimumLod, UvProjectionKind.Primitive, []);

    public static MeshPartRecipe Cone(string id, float radius, float height, int segments, AssetColor color, int materialSlot = 0, int minimumLod = 0) =>
        new(id, MeshPrimitiveKind.Cone, AssetVector3.One, radius, height, segments, 2, [], [], [], AssetTransform.Identity, color, materialSlot, minimumLod, UvProjectionKind.Primitive, []);

    public MeshPartRecipe At(AssetVector3 translation) => this with { Transform = Transform with { Translation = translation } };
    public MeshPartRecipe Rotate(AssetVector3 degrees) => this with { Transform = Transform with { RotationDegrees = degrees } };
    public MeshPartRecipe ScaleBy(AssetVector3 scale) => this with { Transform = Transform with { Scale = scale } };
    public MeshPartRecipe WithModifiers(params MeshModifierRecipe[] modifiers) => this with { Modifiers = modifiers };

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentNullException.ThrowIfNull(Profile);
        ArgumentNullException.ThrowIfNull(Polygon);
        ArgumentNullException.ThrowIfNull(Path);
        ArgumentNullException.ThrowIfNull(Transform);
        ArgumentNullException.ThrowIfNull(Modifiers);
        if (MaterialSlot is < 0 or > 65535 || MinimumLod is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(MaterialSlot));
        }

        if (Segments < 3 || Rings < 2 || !float.IsFinite(Radius) || !float.IsFinite(Height))
        {
            throw new ArgumentOutOfRangeException(nameof(Segments));
        }

        ValidateFinite(Size, nameof(Size));
        foreach (AssetVector2 point in Profile) ValidateFinite(point, nameof(Profile));
        foreach (AssetVector2 point in Polygon) ValidateFinite(point, nameof(Polygon));
        foreach (AssetVector3 point in Path) ValidateFinite(point, nameof(Path));
        Transform.Validate();
        foreach (MeshModifierRecipe modifier in Modifiers)
        {
            ArgumentNullException.ThrowIfNull(modifier);
            modifier.Validate();
        }
        switch (Primitive)
        {
            case MeshPrimitiveKind.Box or MeshPrimitiveKind.Wedge:
                if (Size.X <= 0f || Size.Y <= 0f || Size.Z <= 0f) throw new ArgumentOutOfRangeException(nameof(Size));
                break;
            case MeshPrimitiveKind.Cylinder or MeshPrimitiveKind.Cone or MeshPrimitiveKind.UvSphere:
                if (Radius <= 0f || Height <= 0f) throw new ArgumentOutOfRangeException(nameof(Radius));
                break;
            case MeshPrimitiveKind.Lathe:
                if (Profile.Count < 2 || Profile.Any(static point => point.X < 0f))
                {
                    throw new ArgumentException("Lathe requires at least two profile points with non-negative radii.", nameof(Profile));
                }
                break;
            case MeshPrimitiveKind.Extrusion:
                if (Polygon.Count < 3 || Height <= 0f) throw new ArgumentException("Extrusion requires a polygon and positive height.", nameof(Polygon));
                break;
            case MeshPrimitiveKind.Tube:
                if (Path.Count < 2 || Radius <= 0f) throw new ArgumentException("Tube requires a path and positive radius.", nameof(Path));
                for (int index = 1; index < Path.Count; index++)
                {
                    AssetVector3 previous = Path[index - 1];
                    AssetVector3 current = Path[index];
                    float dx = current.X - previous.X;
                    float dy = current.Y - previous.Y;
                    float dz = current.Z - previous.Z;
                    if (dx * dx + dy * dy + dz * dz <= 1e-10f)
                    {
                        throw new ArgumentException("Tube path cannot contain consecutive duplicate points.", nameof(Path));
                    }
                }
                break;
        }
    }

    private static void ValidateFinite(AssetVector2 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
        {
            throw new ArgumentOutOfRangeException(name, "Mesh values must be finite.");
        }
    }

    private static void ValidateFinite(AssetVector3 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(name, "Mesh values must be finite.");
        }
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteInt32((int)Primitive);
        Size.WriteCanonical(writer);
        writer.WriteSingle(Radius);
        writer.WriteSingle(Height);
        writer.WriteInt32(Segments);
        writer.WriteInt32(Rings);
        WriteList(writer, Profile, static (item, target) => item.WriteCanonical(target));
        WriteList(writer, Polygon, static (item, target) => item.WriteCanonical(target));
        WriteList(writer, Path, static (item, target) => item.WriteCanonical(target));
        Transform.WriteCanonical(writer);
        Color.WriteCanonical(writer);
        writer.WriteInt32(MaterialSlot);
        writer.WriteInt32(MinimumLod);
        writer.WriteInt32((int)UvProjection);
        WriteList(writer, Modifiers, static (item, target) => item.WriteCanonical(target));
    }

    private static void WriteList<T>(CanonicalWriter writer, IReadOnlyList<T> values, Action<T, CanonicalWriter> write)
    {
        writer.WriteCount(values.Count);
        foreach (T value in values) write(value, writer);
    }
}

public sealed record MeshRecipe(
    string Id,
    int SchemaVersion,
    IReadOnlyList<MeshPartRecipe> Parts,
    CollisionProxyKind Collision,
    int LodCount,
    IReadOnlyDictionary<string, string> Metadata) : ICanonicalWritable
{
    public const int CurrentSchemaVersion = 1;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentNullException.ThrowIfNull(Parts);
        ArgumentNullException.ThrowIfNull(Metadata);
        if (SchemaVersion != CurrentSchemaVersion || Parts.Count == 0 || LodCount is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MeshPartRecipe part in Parts)
        {
            ArgumentNullException.ThrowIfNull(part);
            part.Validate();
            if (!ids.Add(part.Id)) throw new InvalidOperationException($"Duplicate mesh part ID '{part.Id}'.");
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
        writer.WriteInt32((int)Collision);
        writer.WriteInt32(LodCount);
        writer.WriteCount(Parts.Count);
        foreach (MeshPartRecipe part in Parts.OrderBy(static part => part.Id, StringComparer.Ordinal)) part.WriteCanonical(writer);
        writer.WriteCount(Metadata.Count);
        foreach ((string key, string value) in Metadata.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteString(key);
            writer.WriteString(value);
        }
    }
}
