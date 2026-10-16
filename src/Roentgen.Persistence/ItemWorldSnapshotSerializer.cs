using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Items;

namespace Roentgen.Persistence;

/// <summary>Versioned, checksummed persistence for the authoritative Phase 13 item world.</summary>
public static class ItemWorldSnapshotSerializer
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RGITEM01");
    public const int FormatVersion = 1;

    public static void Write(Stream stream, ItemWorldState world)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(world);
        if (!stream.CanWrite) throw new ArgumentException("Item snapshot stream must be writable.", nameof(stream));
        byte[] payload = world.ToCanonicalPayload();
        byte[] checksum = SHA256.HashData(payload);
        stream.Write(Magic);
        Span<byte> version = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(version, FormatVersion);
        stream.Write(version);
        Span<byte> length = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(length, payload.LongLength);
        stream.Write(length);
        stream.Write(payload);
        stream.Write(checksum);
    }

    public static ItemWorldState Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("Item snapshot stream must be readable.", nameof(stream));
        Span<byte> magic = stackalloc byte[Magic.Length]; ReadExactly(stream, magic);
        if (!magic.SequenceEqual(Magic)) throw new InvalidDataException("Item snapshot magic header is invalid.");
        Span<byte> version = stackalloc byte[sizeof(int)]; ReadExactly(stream, version);
        int format = BinaryPrimitives.ReadInt32BigEndian(version);
        if (format != FormatVersion) throw new NotSupportedException($"Item snapshot format {format} is not supported.");
        Span<byte> lengthBytes = stackalloc byte[sizeof(long)]; ReadExactly(stream, lengthBytes);
        long length = BinaryPrimitives.ReadInt64BigEndian(lengthBytes);
        if (length < 0 || length > 64L * 1024L * 1024L) throw new InvalidDataException($"Item snapshot payload length {length} is outside the supported range.");
        byte[] payload = new byte[checked((int)length)]; ReadExactly(stream, payload);
        Span<byte> declared = stackalloc byte[StateChecksum.ByteLength]; ReadExactly(stream, declared);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Item snapshot contains trailing data.");
        Span<byte> actual = stackalloc byte[StateChecksum.ByteLength]; SHA256.HashData(payload, actual);
        if (!actual.SequenceEqual(declared)) throw new InvalidDataException("Item snapshot checksum is invalid.");
        return ItemWorldState.ReadCanonical(payload);
    }

    public static byte[] WriteToBytes(ItemWorldState world)
    {
        using var stream = new MemoryStream(); Write(stream, world); return stream.ToArray();
    }

    public static ItemWorldState ReadFromBytes(ReadOnlyMemory<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray(), writable: false); return Read(stream);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            int read = stream.Read(destination);
            if (read <= 0) throw new EndOfStreamException("Item snapshot ended unexpectedly.");
            destination = destination[read..];
        }
    }
}
