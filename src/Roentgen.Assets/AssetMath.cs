using System.Numerics;
using Roentgen.Core;

namespace Roentgen.Assets;

public readonly record struct AssetVector2(float X, float Y) : ICanonicalWritable
{
    public static readonly AssetVector2 Zero = new(0f, 0f);
    public Vector2 ToNumerics() => new(X, Y);
    public static AssetVector2 From(Vector2 value) => new(value.X, value.Y);
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteSingle(X); writer.WriteSingle(Y); }
}

public readonly record struct AssetVector3(float X, float Y, float Z) : ICanonicalWritable
{
    public static readonly AssetVector3 Zero = new(0f, 0f, 0f);
    public static readonly AssetVector3 One = new(1f, 1f, 1f);
    public Vector3 ToNumerics() => new(X, Y, Z);
    public static AssetVector3 From(Vector3 value) => new(value.X, value.Y, value.Z);
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteSingle(X); writer.WriteSingle(Y); writer.WriteSingle(Z); }
}

public readonly record struct AssetColor(byte R, byte G, byte B, byte A = 255) : ICanonicalWritable
{
    public static readonly AssetColor White = new(255, 255, 255, 255);
    public static readonly AssetColor Black = new(0, 0, 0, 255);
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteUInt32(R);
        writer.WriteUInt32(G);
        writer.WriteUInt32(B);
        writer.WriteUInt32(A);
    }

    public static AssetColor Lerp(AssetColor a, AssetColor b, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new AssetColor(
            (byte)Math.Clamp((int)MathF.Round(a.R + (b.R - a.R) * amount), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(a.G + (b.G - a.G) * amount), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(a.B + (b.B - a.B) * amount), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(a.A + (b.A - a.A) * amount), 0, 255));
    }
}

public readonly record struct AssetBounds(AssetVector3 Minimum, AssetVector3 Maximum) : ICanonicalWritable
{
    public AssetVector3 Center => new(
        (Minimum.X + Maximum.X) * 0.5f,
        (Minimum.Y + Maximum.Y) * 0.5f,
        (Minimum.Z + Maximum.Z) * 0.5f);

    public AssetVector3 Size => new(
        Maximum.X - Minimum.X,
        Maximum.Y - Minimum.Y,
        Maximum.Z - Minimum.Z);

    public void WriteCanonical(CanonicalWriter writer)
    {
        Minimum.WriteCanonical(writer);
        Maximum.WriteCanonical(writer);
    }
}
