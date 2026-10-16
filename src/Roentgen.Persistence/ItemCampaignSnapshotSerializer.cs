using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Items;

namespace Roentgen.Persistence;

/// <summary>
/// Complete Phase 13 campaign-item snapshot.  It packages the authoritative item world
/// together with learned recipes and the small set of runtime selections that cannot be
/// reconstructed from item ownership alone.
/// </summary>
public sealed record ItemCampaignSnapshot(
    ItemWorldState World,
    RecipeKnowledgeState RecipeKnowledge,
    StableId ActiveWeaponId,
    StableId ThrowableStackId);

public static class ItemCampaignSnapshotSerializer
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RGITCM01");
    public const int FormatVersion = 1;
    private const int MaximumPayloadBytes = 64 * 1024 * 1024;

    public static byte[] WriteToBytes(ItemCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var writer = new CanonicalWriter();
        writer.WriteBytes(snapshot.World.ToCanonicalPayload());
        snapshot.RecipeKnowledge.WriteCanonical(writer);
        writer.WriteStableId(snapshot.ActiveWeaponId);
        writer.WriteStableId(snapshot.ThrowableStackId);
        byte[] payload = writer.ToArray();
        byte[] checksum = SHA256.HashData(payload);

        using var stream = new MemoryStream();
        stream.Write(Magic);
        Span<byte> version = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(version, FormatVersion);
        stream.Write(version);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        stream.Write(length);
        stream.Write(payload);
        stream.Write(checksum);
        return stream.ToArray();
    }

    public static ItemCampaignSnapshot ReadFromBytes(ReadOnlyMemory<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        Span<byte> magic = stackalloc byte[Magic.Length];
        ReadExactly(stream, magic);
        if (!magic.SequenceEqual(Magic)) throw new InvalidDataException("Item campaign snapshot magic header is invalid.");
        Span<byte> version = stackalloc byte[sizeof(int)];
        ReadExactly(stream, version);
        int format = BinaryPrimitives.ReadInt32BigEndian(version);
        if (format != FormatVersion) throw new NotSupportedException($"Item campaign snapshot format {format} is not supported.");
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        ReadExactly(stream, lengthBytes);
        int length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
        if (length < 0 || length > MaximumPayloadBytes) throw new InvalidDataException($"Item campaign snapshot payload length {length} is outside the supported range.");
        byte[] payload = new byte[length];
        ReadExactly(stream, payload);
        Span<byte> declared = stackalloc byte[StateChecksum.ByteLength];
        ReadExactly(stream, declared);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Item campaign snapshot contains trailing data.");
        Span<byte> actual = stackalloc byte[StateChecksum.ByteLength];
        SHA256.HashData(payload, actual);
        if (!actual.SequenceEqual(declared)) throw new InvalidDataException("Item campaign snapshot checksum is invalid.");

        var reader = new CanonicalReader(payload);
        ItemWorldState world = ItemWorldState.ReadCanonical(reader.ReadBytes());
        RecipeKnowledgeState knowledge = RecipeKnowledgeState.ReadCanonical(reader);
        StableId activeWeaponId = reader.ReadStableId();
        StableId throwableStackId = reader.ReadStableId();
        reader.EnsureComplete();
        return new ItemCampaignSnapshot(world, knowledge, activeWeaponId, throwableStackId);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            int read = stream.Read(destination);
            if (read <= 0) throw new EndOfStreamException("Item campaign snapshot ended unexpectedly.");
            destination = destination[read..];
        }
    }
}
