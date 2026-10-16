using System.Buffers.Binary;
using System.Security.Cryptography;
using Roentgen.Narrative;

namespace Roentgen.Persistence;

public static class NarrativeWorldSnapshotSerializer
{
    private static ReadOnlySpan<byte> Magic => "RGNAR16\0"u8;
    public static byte[] WriteToBytes(NarrativeWorldState world)
    {
        ArgumentNullException.ThrowIfNull(world); byte[] payload = world.ToCanonicalPayload(); byte[] checksum = SHA256.HashData(payload); byte[] result = new byte[Magic.Length + 4 + checksum.Length + payload.Length]; Magic.CopyTo(result); BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(Magic.Length, 4), payload.Length); checksum.CopyTo(result, Magic.Length + 4); payload.CopyTo(result, Magic.Length + 4 + checksum.Length); return result;
    }
    public static NarrativeWorldState ReadFromBytes(ReadOnlyMemory<byte> bytes)
    {
        int header = Magic.Length + 4 + 32; if (bytes.Length < header || !bytes.Span[..Magic.Length].SequenceEqual(Magic)) throw new InvalidDataException("Narrative snapshot magic is invalid."); int length = BinaryPrimitives.ReadInt32BigEndian(bytes.Span.Slice(Magic.Length, 4)); if (length < 0 || bytes.Length != header + length) throw new InvalidDataException("Narrative snapshot length is invalid."); ReadOnlyMemory<byte> expected = bytes.Slice(Magic.Length + 4, 32); ReadOnlyMemory<byte> payload = bytes.Slice(header, length); byte[] actual = SHA256.HashData(payload.Span); if (!CryptographicOperations.FixedTimeEquals(expected.Span, actual)) throw new InvalidDataException("Narrative snapshot checksum is invalid."); return NarrativeWorldState.ReadCanonical(payload);
    }
    public static void WriteAtomic(string path, NarrativeWorldState world)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path); string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); string temporary = full + ".tmp"; File.WriteAllBytes(temporary, WriteToBytes(world)); File.Move(temporary, full, overwrite: true);
    }
    public static NarrativeWorldState Read(string path) => ReadFromBytes(File.ReadAllBytes(path));
}
