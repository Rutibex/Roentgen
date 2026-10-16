using System.Numerics;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Animation;

public readonly record struct SkinInfluences(byte Bone0, byte Bone1, byte Bone2, byte Bone3, Vector4 Weights)
{
    public SkinInfluences Normalized()
    {
        Vector4 clamped = Vector4.Max(Weights, Vector4.Zero);
        float total = clamped.X + clamped.Y + clamped.Z + clamped.W;
        return total <= 0.000001f
            ? new SkinInfluences(Bone0, 0, 0, 0, Vector4.UnitX)
            : new SkinInfluences(Bone0, Bone1, Bone2, Bone3, clamped / total);
    }

    public IEnumerable<(byte Bone, float Weight)> Enumerate()
    {
        yield return (Bone0, Weights.X);
        yield return (Bone1, Weights.Y);
        yield return (Bone2, Weights.Z);
        yield return (Bone3, Weights.W);
    }
}

public readonly record struct CharacterVertex(
    Vector3 Position,
    Vector3 Normal,
    Vector2 TextureCoordinate,
    Vector4 Color,
    SkinInfluences Skin);

public sealed class CharacterMeshProduct : ICanonicalWritable
{
    public CharacterMeshProduct(string name, IReadOnlyList<CharacterVertex> vertices, IReadOnlyList<int> indices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        if (vertices.Count < 3 || indices.Count == 0 || indices.Count % 3 != 0)
        {
            throw new ArgumentException("A character mesh requires vertices and triangle-list indices.");
        }

        for (int index = 0; index < indices.Count; index++)
        {
            if (indices[index] < 0 || indices[index] >= vertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(indices));
            }
        }

        Name = name;
        Vertices = vertices.Select(static value => value with { Skin = value.Skin.Normalized() }).ToArray();
        Indices = indices.ToArray();
        Bounds = CharacterBounds.FromVertices(Vertices);
    }

    public string Name { get; }
    public CharacterVertex[] Vertices { get; }
    public int[] Indices { get; }
    public CharacterBounds Bounds { get; }
    public int PrimitiveCount => Indices.Length / 3;

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Name);
        writer.WriteCount(Vertices.Length);
        foreach (CharacterVertex vertex in Vertices)
        {
            WriteVector3(writer, vertex.Position);
            WriteVector3(writer, vertex.Normal);
            writer.WriteSingle(vertex.TextureCoordinate.X);
            writer.WriteSingle(vertex.TextureCoordinate.Y);
            writer.WriteSingle(vertex.Color.X);
            writer.WriteSingle(vertex.Color.Y);
            writer.WriteSingle(vertex.Color.Z);
            writer.WriteSingle(vertex.Color.W);
            writer.WriteInt32(vertex.Skin.Bone0);
            writer.WriteInt32(vertex.Skin.Bone1);
            writer.WriteInt32(vertex.Skin.Bone2);
            writer.WriteInt32(vertex.Skin.Bone3);
            writer.WriteSingle(vertex.Skin.Weights.X);
            writer.WriteSingle(vertex.Skin.Weights.Y);
            writer.WriteSingle(vertex.Skin.Weights.Z);
            writer.WriteSingle(vertex.Skin.Weights.W);
        }

        writer.WriteCount(Indices.Length);
        foreach (int index in Indices)
        {
            writer.WriteInt32(index);
        }
    }

    private static void WriteVector3(CanonicalWriter writer, Vector3 value)
    {
        writer.WriteSingle(value.X);
        writer.WriteSingle(value.Y);
        writer.WriteSingle(value.Z);
    }
}

public readonly record struct CharacterBounds(Vector3 Minimum, Vector3 Maximum)
{
    public static CharacterBounds FromVertices(IReadOnlyList<CharacterVertex> vertices)
    {
        Vector3 minimum = vertices[0].Position;
        Vector3 maximum = minimum;
        for (int index = 1; index < vertices.Count; index++)
        {
            minimum = Vector3.Min(minimum, vertices[index].Position);
            maximum = Vector3.Max(maximum, vertices[index].Position);
        }

        return new CharacterBounds(minimum, maximum);
    }
}

public sealed record CharacterLodProduct(CharacterMeshProduct Body, CharacterMeshProduct? Clothing, int LodLevel);

public sealed record EquipmentPoseProfile(
    string ProfileId,
    EquipmentGripKind GripKind,
    Vector3 PrimaryGrip,
    Vector3 SecondaryGrip,
    Vector3 ShoulderContact,
    Vector3 SightLine,
    Vector3 MuzzleSocket,
    Vector3 MagazineSocket,
    IReadOnlyList<AnimationMarker> Markers);

public readonly record struct AnimationMarker(string MarkerId, int ProgressPermille);

public sealed record GeneratedCharacterProduct(
    StableId CharacterId,
    CharacterAppearanceDescriptor Appearance,
    SkeletonDefinition Skeleton,
    IReadOnlyList<CharacterLodProduct> Lods,
    CharacterMeshProduct? FirstPersonArms,
    IReadOnlyList<CharacterTargetPart> TargetParts,
    IReadOnlyDictionary<string, EquipmentPoseProfile> EquipmentProfiles,
    GenerationProvenance Provenance);
