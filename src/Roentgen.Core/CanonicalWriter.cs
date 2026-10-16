using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Roentgen.Contracts;

namespace Roentgen.Core;

public interface ICanonicalWritable
{
    void WriteCanonical(CanonicalWriter writer);
}

public sealed class CanonicalWriter
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly ArrayBufferWriter<byte> _buffer = new();

    public int Length => _buffer.WrittenCount;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    public void WriteNull() => WriteTag(0x00);

    public void WriteBoolean(bool value)
    {
        WriteTag(0x01);
        WriteRawByte(value ? (byte)1 : (byte)0);
    }

    public void WriteInt32(int value)
    {
        WriteTag(0x02);
        Span<byte> bytes = _buffer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        _buffer.Advance(sizeof(int));
    }

    public void WriteUInt32(uint value)
    {
        WriteTag(0x03);
        Span<byte> bytes = _buffer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        _buffer.Advance(sizeof(uint));
    }

    public void WriteInt64(long value)
    {
        WriteTag(0x04);
        Span<byte> bytes = _buffer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        _buffer.Advance(sizeof(long));
    }

    public void WriteUInt64(ulong value)
    {
        WriteTag(0x05);
        Span<byte> bytes = _buffer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        _buffer.Advance(sizeof(ulong));
    }

    public void WriteSingle(float value)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Canonical state does not permit NaN or infinity.");
        }

        WriteTag(0x06);
        WriteUInt32Raw(BitConverter.SingleToUInt32Bits(value == 0.0f ? 0.0f : value));
    }

    public void WriteDouble(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Canonical state does not permit NaN or infinity.");
        }

        WriteTag(0x07);
        WriteUInt64Raw(BitConverter.DoubleToUInt64Bits(value == 0.0 ? 0.0 : value));
    }

    public void WriteString(string? value)
    {
        if (value is null)
        {
            WriteNull();
            return;
        }

        WriteTag(0x08);
        string normalized = value.Normalize(NormalizationForm.FormC);
        int byteCount = StrictUtf8.GetByteCount(normalized);
        WriteLength(byteCount);
        Span<byte> bytes = _buffer.GetSpan(byteCount);
        int written = StrictUtf8.GetBytes(normalized, bytes);
        _buffer.Advance(written);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteTag(0x09);
        WriteLength(value.Length);
        value.CopyTo(_buffer.GetSpan(value.Length));
        _buffer.Advance(value.Length);
    }

    public void WriteStableId(StableId value)
    {
        WriteTag(0x0A);
        WriteUInt64Raw(value.High);
        WriteUInt64Raw(value.Low);
    }

    public void WriteChecksum(StateChecksum value)
    {
        WriteTag(0x0B);
        Span<byte> bytes = stackalloc byte[StateChecksum.ByteLength];
        value.WriteBytes(bytes);
        bytes.CopyTo(_buffer.GetSpan(bytes.Length));
        _buffer.Advance(bytes.Length);
    }

    public void WriteCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        WriteTag(0x0C);
        WriteLength(count);
    }

    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();

    public StateChecksum ComputeChecksum() => StateChecksum.FromBytes(SHA256.HashData(_buffer.WrittenSpan));

    private void WriteTag(byte tag) => WriteRawByte(tag);

    private void WriteRawByte(byte value)
    {
        Span<byte> target = _buffer.GetSpan(1);
        target[0] = value;
        _buffer.Advance(1);
    }

    private void WriteLength(int value)
    {
        Span<byte> bytes = _buffer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        _buffer.Advance(sizeof(int));
    }

    private void WriteUInt32Raw(uint value)
    {
        Span<byte> bytes = _buffer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        _buffer.Advance(sizeof(uint));
    }

    private void WriteUInt64Raw(ulong value)
    {
        Span<byte> bytes = _buffer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        _buffer.Advance(sizeof(ulong));
    }
}
