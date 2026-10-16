using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Persistence;

public sealed record CampaignSnapshotV16(
    CampaignSeed CampaignSeed,
    ulong SimulationTick,
    int GenerationSchemaVersion,
    string ContentFingerprint,
    byte[] ItemSnapshot,
    byte[] SocietySnapshot,
    byte[] NarrativeSnapshot);

public static class CampaignSnapshotV16Serializer
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RGCAMP16");
    public const int FormatVersion = 16;
    private const int MaximumSectionBytes = 128 * 1024 * 1024;

    public static byte[] WriteToBytes(CampaignSnapshotV16 snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var writer = new CanonicalWriter(); writer.WriteUInt64(snapshot.CampaignSeed.Value); writer.WriteUInt64(snapshot.SimulationTick); writer.WriteInt32(snapshot.GenerationSchemaVersion); writer.WriteString(snapshot.ContentFingerprint); writer.WriteBytes(snapshot.ItemSnapshot); writer.WriteBytes(snapshot.SocietySnapshot); writer.WriteBytes(snapshot.NarrativeSnapshot); byte[] payload = writer.ToArray(); byte[] checksum = SHA256.HashData(payload);
        using var stream = new MemoryStream(); stream.Write(Magic); Span<byte> version = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(version, FormatVersion); stream.Write(version); Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(length, payload.Length); stream.Write(length); stream.Write(checksum); stream.Write(payload); return stream.ToArray();
    }

    public static CampaignSnapshotV16 ReadFromBytes(ReadOnlyMemory<byte> bytes)
    {
        int header = Magic.Length + 4 + 4 + StateChecksum.ByteLength; if (bytes.Length < header || !bytes.Span[..Magic.Length].SequenceEqual(Magic)) throw new InvalidDataException("Campaign snapshot magic is invalid."); int version = BinaryPrimitives.ReadInt32BigEndian(bytes.Span.Slice(Magic.Length, 4)); if (version != FormatVersion) throw new NotSupportedException($"Campaign snapshot format {version} is not supported."); int length = BinaryPrimitives.ReadInt32BigEndian(bytes.Span.Slice(Magic.Length + 4, 4)); if (length < 0 || length > MaximumSectionBytes * 3 || bytes.Length != header + length) throw new InvalidDataException("Campaign snapshot length is invalid."); ReadOnlyMemory<byte> expected = bytes.Slice(Magic.Length + 8, StateChecksum.ByteLength); ReadOnlyMemory<byte> payload = bytes.Slice(header, length); byte[] actual = SHA256.HashData(payload.Span); if (!CryptographicOperations.FixedTimeEquals(expected.Span, actual)) throw new InvalidDataException("Campaign snapshot checksum is invalid."); var reader = new CanonicalReader(payload); var seed = new CampaignSeed(reader.ReadUInt64()); ulong tick = reader.ReadUInt64(); int schema = reader.ReadInt32(); string fingerprint = reader.ReadRequiredString(); byte[] items = ReadBounded(reader, "items"); byte[] society = ReadBounded(reader, "society"); byte[] narrative = ReadBounded(reader, "narrative"); reader.EnsureComplete(); return new CampaignSnapshotV16(seed, tick, schema, fingerprint, items, society, narrative);
    }

    public static void WriteAtomic(string path, CampaignSnapshotV16 snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path); string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); string temporary = full + ".tmp"; File.WriteAllBytes(temporary, WriteToBytes(snapshot)); File.Move(temporary, full, overwrite: true);
    }

    private static byte[] ReadBounded(CanonicalReader reader, string name) { byte[] value = reader.ReadBytes(); if (value.Length > MaximumSectionBytes) throw new InvalidDataException($"Campaign {name} section exceeds the supported limit."); return value; }
}
