using System.Globalization;

namespace Roentgen.World;

public static class WorldUnits
{
    public const int CentimetersPerMeter = 100;

    public static int MetersToCentimeters(int meters) => checked(meters * CentimetersPerMeter);

    public static int MetersToCentimeters(float meters)
    {
        if (!float.IsFinite(meters))
        {
            throw new ArgumentOutOfRangeException(nameof(meters));
        }

        return checked((int)MathF.Round(meters * CentimetersPerMeter, MidpointRounding.AwayFromZero));
    }

    public static float CentimetersToMeters(int centimeters) => centimeters / (float)CentimetersPerMeter;

    public static int FloorDivide(int value, int divisor)
    {
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor));
        }

        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    public static int PositiveModulo(int value, int divisor)
    {
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor));
        }

        int remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}

public readonly record struct WorldPoint(int XCentimeters, int ZCentimeters)
{
    public static WorldPoint FromMeters(float x, float z) =>
        new(WorldUnits.MetersToCentimeters(x), WorldUnits.MetersToCentimeters(z));

    public long DistanceSquared(WorldPoint other)
    {
        long dx = (long)XCentimeters - other.XCentimeters;
        long dz = (long)ZCentimeters - other.ZCentimeters;
        return checked(dx * dx + dz * dz);
    }

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"({WorldUnits.CentimetersToMeters(XCentimeters):F2} m, {WorldUnits.CentimetersToMeters(ZCentimeters):F2} m)");
}

public readonly record struct WorldRect(int MinimumXCentimeters, int MinimumZCentimeters, int MaximumXCentimeters, int MaximumZCentimeters)
{
    public int WidthCentimeters => checked(MaximumXCentimeters - MinimumXCentimeters);
    public int DepthCentimeters => checked(MaximumZCentimeters - MinimumZCentimeters);

    public WorldPoint Center => new(
        MinimumXCentimeters + WidthCentimeters / 2,
        MinimumZCentimeters + DepthCentimeters / 2);

    public bool Contains(WorldPoint point) =>
        point.XCentimeters >= MinimumXCentimeters && point.XCentimeters <= MaximumXCentimeters &&
        point.ZCentimeters >= MinimumZCentimeters && point.ZCentimeters <= MaximumZCentimeters;

    public void Validate()
    {
        if (MaximumXCentimeters <= MinimumXCentimeters || MaximumZCentimeters <= MinimumZCentimeters)
        {
            throw new InvalidOperationException("A world rectangle must have positive area.");
        }
    }
}

public readonly record struct TerrainChunkAddress(int X, int Z) : IComparable<TerrainChunkAddress>
{
    public int CompareTo(TerrainChunkAddress other)
    {
        int z = Z.CompareTo(other.Z);
        return z != 0 ? z : X.CompareTo(other.X);
    }

    public static TerrainChunkAddress FromWorldCentimeters(int xCentimeters, int zCentimeters, int chunkSizeCentimeters) =>
        new(
            WorldUnits.FloorDivide(xCentimeters, chunkSizeCentimeters),
            WorldUnits.FloorDivide(zCentimeters, chunkSizeCentimeters));

    public WorldPoint Origin(int chunkSizeCentimeters) =>
        new(checked(X * chunkSizeCentimeters), checked(Z * chunkSizeCentimeters));

    public WorldPoint Center(int chunkSizeCentimeters)
    {
        WorldPoint origin = Origin(chunkSizeCentimeters);
        return new WorldPoint(origin.XCentimeters + chunkSizeCentimeters / 2, origin.ZCentimeters + chunkSizeCentimeters / 2);
    }

    public int ChebyshevDistance(TerrainChunkAddress other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Z - other.Z));

    public override string ToString() => $"{X},{Z}";
}
