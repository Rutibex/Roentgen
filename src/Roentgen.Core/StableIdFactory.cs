using System.Buffers.Binary;
using System.Security.Cryptography;
using Roentgen.Contracts;

namespace Roentgen.Core;

public static class StableIdFactory
{
    public const int SchemaVersion = 1;

    public static StableId Create(string namespaceId, string localKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localKey);

        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.stable-id");
        writer.WriteInt32(SchemaVersion);
        writer.WriteString(namespaceId);
        writer.WriteString(localKey);
        return FromCanonicalBytes(writer.WrittenSpan);
    }

    public static StableId CreateCampaignRoot(CampaignSeed seed) => Create("campaign", seed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static StableId CreateChild(StableId parentId, string childKind, string localKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(localKey);

        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.stable-id.child");
        writer.WriteInt32(SchemaVersion);
        writer.WriteStableId(parentId);
        writer.WriteString(childKind);
        writer.WriteString(localKey);
        return FromCanonicalBytes(writer.WrittenSpan);
    }

    public static StableId CreateChild(StableId parentId, string childKind, long ordinal) =>
        CreateChild(parentId, childKind, ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static StableId FromCanonicalBytes(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return new StableId(
            BinaryPrimitives.ReadUInt64BigEndian(hash[0..8]),
            BinaryPrimitives.ReadUInt64BigEndian(hash[8..16]));
    }
}
