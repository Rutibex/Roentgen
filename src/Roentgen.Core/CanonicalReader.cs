using System.Buffers.Binary;
using System.Text;
using Roentgen.Contracts;

namespace Roentgen.Core;

public sealed class CanonicalReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly ReadOnlyMemory<byte> _memory;
    private int _offset;

    public CanonicalReader(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
    }

    public int Position => _offset;

    public int Remaining => _memory.Length - _offset;

    public bool IsComplete => _offset == _memory.Length;

    public void ReadNull() => RequireTag(0x00);

    public bool ReadBoolean()
    {
        RequireTag(0x01);
        byte value = ReadRawByte();
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidDataException($"Invalid canonical Boolean value {value} at offset {_offset - 1}.")
        };
    }

    public int ReadInt32()
    {
        RequireTag(0x02);
        return BinaryPrimitives.ReadInt32BigEndian(ReadRaw(sizeof(int)));
    }

    public uint ReadUInt32()
    {
        RequireTag(0x03);
        return BinaryPrimitives.ReadUInt32BigEndian(ReadRaw(sizeof(uint)));
    }

    public long ReadInt64()
    {
        RequireTag(0x04);
        return BinaryPrimitives.ReadInt64BigEndian(ReadRaw(sizeof(long)));
    }

    public ulong ReadUInt64()
    {
        RequireTag(0x05);
        return BinaryPrimitives.ReadUInt64BigEndian(ReadRaw(sizeof(ulong)));
    }

    public float ReadSingle()
    {
        RequireTag(0x06);
        float value = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32BigEndian(ReadRaw(sizeof(uint))));
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException("Canonical floating-point state cannot contain NaN or infinity.");
        }

        return value == 0.0f ? 0.0f : value;
    }

    public double ReadDouble()
    {
        RequireTag(0x07);
        double value = BitConverter.UInt64BitsToDouble(BinaryPrimitives.ReadUInt64BigEndian(ReadRaw(sizeof(ulong))));
        if (!double.IsFinite(value))
        {
            throw new InvalidDataException("Canonical floating-point state cannot contain NaN or infinity.");
        }

        return value == 0.0 ? 0.0 : value;
    }

    public string? ReadString()
    {
        if (PeekTag() == 0x00)
        {
            _offset++;
            return null;
        }

        RequireTag(0x08);
        int length = ReadLength();
        string value = StrictUtf8.GetString(ReadRaw(length));
        string normalized = value.Normalize(NormalizationForm.FormC);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Canonical strings must be encoded in Unicode NFC form.");
        }

        return value;
    }

    public string ReadRequiredString() => ReadString() ?? throw new InvalidDataException("A required canonical string was null.");

    public byte[] ReadBytes()
    {
        RequireTag(0x09);
        int length = ReadLength();
        return ReadRaw(length).ToArray();
    }

    public StableId ReadStableId()
    {
        RequireTag(0x0A);
        ReadOnlySpan<byte> bytes = ReadRaw(16);
        return new StableId(
            BinaryPrimitives.ReadUInt64BigEndian(bytes[0..8]),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[8..16]));
    }

    public StateChecksum ReadChecksum()
    {
        RequireTag(0x0B);
        return StateChecksum.FromBytes(ReadRaw(StateChecksum.ByteLength));
    }

    public int ReadCount()
    {
        RequireTag(0x0C);
        return ReadLength();
    }

    public void EnsureComplete()
    {
        if (!IsComplete)
        {
            throw new InvalidDataException($"Canonical payload contains {Remaining} unread bytes.");
        }
    }

    private byte PeekTag()
    {
        if (_offset >= _memory.Length)
        {
            throw new EndOfStreamException("Canonical payload ended before a type tag was available.");
        }

        return _memory.Span[_offset];
    }

    private void RequireTag(byte expected)
    {
        byte actual = ReadRawByte();
        if (actual != expected)
        {
            throw new InvalidDataException($"Expected canonical tag 0x{expected:x2}, found 0x{actual:x2} at offset {_offset - 1}.");
        }
    }

    private byte ReadRawByte()
    {
        if (_offset >= _memory.Length)
        {
            throw new EndOfStreamException("Canonical payload ended unexpectedly.");
        }

        return _memory.Span[_offset++];
    }

    private int ReadLength()
    {
        int value = BinaryPrimitives.ReadInt32BigEndian(ReadRaw(sizeof(int)));
        if (value < 0)
        {
            throw new InvalidDataException($"Canonical length cannot be negative: {value}.");
        }

        return value;
    }

    private ReadOnlySpan<byte> ReadRaw(int length)
    {
        if (length < 0 || length > Remaining)
        {
            throw new EndOfStreamException($"Canonical payload requested {length} bytes with only {Remaining} remaining.");
        }

        ReadOnlySpan<byte> result = _memory.Span.Slice(_offset, length);
        _offset += length;
        return result;
    }
}
