using System.Buffers.Binary;
using System.Globalization;

namespace Roentgen.Core;

public readonly record struct StateChecksum(ulong A, ulong B, ulong C, ulong D) : IComparable<StateChecksum>
{
    public const int ByteLength = 32;

    public static StateChecksum Zero => default;

    public bool IsZero => A == 0UL && B == 0UL && C == 0UL && D == 0UL;

    public int CompareTo(StateChecksum other)
    {
        int result = A.CompareTo(other.A);
        if (result != 0) return result;
        result = B.CompareTo(other.B);
        if (result != 0) return result;
        result = C.CompareTo(other.C);
        return result != 0 ? result : D.CompareTo(other.D);
    }

    public override string ToString() => string.Concat(
        A.ToString("x16", CultureInfo.InvariantCulture),
        B.ToString("x16", CultureInfo.InvariantCulture),
        C.ToString("x16", CultureInfo.InvariantCulture),
        D.ToString("x16", CultureInfo.InvariantCulture));

    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
        {
            throw new ArgumentException($"Destination must contain at least {ByteLength} bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination[0..8], A);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..16], B);
        BinaryPrimitives.WriteUInt64BigEndian(destination[16..24], C);
        BinaryPrimitives.WriteUInt64BigEndian(destination[24..32], D);
    }

    public static StateChecksum FromBytes(ReadOnlySpan<byte> source)
    {
        if (source.Length != ByteLength)
        {
            throw new ArgumentException($"A state checksum must contain exactly {ByteLength} bytes.", nameof(source));
        }

        return new StateChecksum(
            BinaryPrimitives.ReadUInt64BigEndian(source[0..8]),
            BinaryPrimitives.ReadUInt64BigEndian(source[8..16]),
            BinaryPrimitives.ReadUInt64BigEndian(source[16..24]),
            BinaryPrimitives.ReadUInt64BigEndian(source[24..32]));
    }

    public static StateChecksum Parse(string value)
    {
        if (!TryParse(value, out StateChecksum result))
        {
            throw new FormatException("A state checksum must contain exactly 64 hexadecimal characters.");
        }

        return result;
    }

    public static bool TryParse(string? value, out StateChecksum result)
    {
        result = default;
        if (value is null || value.Length != 64)
        {
            return false;
        }

        Span<ulong> words = stackalloc ulong[4];
        for (int index = 0; index < words.Length; index++)
        {
            if (!ulong.TryParse(value.AsSpan(index * 16, 16), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out words[index]))
            {
                return false;
            }
        }

        result = new StateChecksum(words[0], words[1], words[2], words[3]);
        return true;
    }
}
