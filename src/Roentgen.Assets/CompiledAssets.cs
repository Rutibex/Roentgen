using Roentgen.Core;

namespace Roentgen.Assets;

public readonly record struct CompiledAssetVertex(
    AssetVector3 Position,
    AssetVector3 Normal,
    AssetVector2 TextureCoordinate,
    AssetColor Color,
    ushort MaterialSlot) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        Position.WriteCanonical(writer);
        Normal.WriteCanonical(writer);
        TextureCoordinate.WriteCanonical(writer);
        Color.WriteCanonical(writer);
        writer.WriteUInt32(MaterialSlot);
    }
}

public sealed record CompiledMeshLod(
    int Level,
    IReadOnlyList<CompiledAssetVertex> Vertices,
    IReadOnlyList<int> Indices,
    AssetBounds Bounds) : ICanonicalWritable
{
    public int PrimitiveCount => Indices.Count / 3;
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32(Level);
        writer.WriteCount(Vertices.Count);
        foreach (CompiledAssetVertex vertex in Vertices) vertex.WriteCanonical(writer);
        writer.WriteCount(Indices.Count);
        foreach (int index in Indices) writer.WriteInt32(index);
        Bounds.WriteCanonical(writer);
    }
}

public sealed record CollisionProxyAsset(
    CollisionProxyKind Kind,
    AssetBounds Bounds,
    float Radius,
    float Height,
    IReadOnlyList<AssetBounds> Boxes) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)Kind);
        Bounds.WriteCanonical(writer);
        writer.WriteSingle(Radius);
        writer.WriteSingle(Height);
        writer.WriteCount(Boxes.Count);
        foreach (AssetBounds box in Boxes) box.WriteCanonical(writer);
    }
}

public sealed record CompiledMeshAsset(
    string Id,
    int SchemaVersion,
    string CompilerId,
    StateChecksum RecipeFingerprint,
    StateChecksum CompilationKey,
    StateChecksum ContentChecksum,
    IReadOnlyList<CompiledMeshLod> Lods,
    CollisionProxyAsset Collision) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteInt32(SchemaVersion);
        writer.WriteString(CompilerId);
        writer.WriteChecksum(RecipeFingerprint);
        writer.WriteChecksum(CompilationKey);
        writer.WriteCount(Lods.Count);
        foreach (CompiledMeshLod lod in Lods) lod.WriteCanonical(writer);
        Collision.WriteCanonical(writer);
    }
}

public sealed record CompiledTextureChannel(int Width, int Height, byte[] Rgba) : ICanonicalWritable
{
    public int ByteSize => Rgba.Length;
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32(Width);
        writer.WriteInt32(Height);
        writer.WriteBytes(Rgba);
    }
}

public sealed record CompiledMaterialAsset(
    string Id,
    int SchemaVersion,
    string CompilerId,
    StateChecksum RecipeFingerprint,
    StateChecksum CompilationKey,
    StateChecksum ContentChecksum,
    float Roughness,
    float Metallic,
    float EmissiveStrength,
    CompiledTextureChannel Albedo,
    CompiledTextureChannel Surface,
    CompiledTextureChannel Emissive) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id);
        writer.WriteInt32(SchemaVersion);
        writer.WriteString(CompilerId);
        writer.WriteChecksum(RecipeFingerprint);
        writer.WriteChecksum(CompilationKey);
        writer.WriteSingle(Roughness);
        writer.WriteSingle(Metallic);
        writer.WriteSingle(EmissiveStrength);
        Albedo.WriteCanonical(writer);
        Surface.WriteCanonical(writer);
        Emissive.WriteCanonical(writer);
    }
}
