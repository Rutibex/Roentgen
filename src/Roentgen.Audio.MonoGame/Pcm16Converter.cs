using System.Buffers.Binary;

namespace Roentgen.Audio.MonoGame;

public static class Pcm16Converter
{
    public static void Convert(ReadOnlySpan<float> source, Span<byte> destination, out int nonFinite, out int clipped)
    {
        int required = checked(source.Length * sizeof(short));
        if (destination.Length < required) throw new ArgumentException("Destination is too small.", nameof(destination));
        nonFinite = 0;
        clipped = 0;
        for (int i = 0; i < source.Length; i++)
        {
            float value = source[i];
            if (!float.IsFinite(value)) { value = 0f; nonFinite++; }
            if (value is < -1f or > 1f) clipped++;
            value = Math.Clamp(value, -1f, 1f);
            short sample = value >= 0f
                ? (short)Math.Min(short.MaxValue, MathF.Round(value * short.MaxValue))
                : (short)Math.Max(short.MinValue, MathF.Round(value * -short.MinValue));
            BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(i * sizeof(short), sizeof(short)), sample);
        }
    }
}
