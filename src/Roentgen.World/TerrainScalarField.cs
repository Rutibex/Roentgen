using Roentgen.Core;

namespace Roentgen.World;

public sealed class TerrainScalarField : ICanonicalWritable
{
    private readonly int[] _values;

    public TerrainScalarField(string id, int width, int depth, IReadOnlyList<int> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(values);
        if (width < 2 || depth < 2 || values.Count != checked(width * depth))
        {
            throw new ArgumentException("Scalar-field dimensions do not match the supplied values.", nameof(values));
        }

        Id = id;
        Width = width;
        Depth = depth;
        _values = values.ToArray();
    }

    public string Id { get; }
    public int Width { get; }
    public int Depth { get; }
    public IReadOnlyList<int> Values => _values;
    public int this[int x, int z] => _values[checked(z * Width + x)];

    public int SampleClamped(int x, int z) => this[Math.Clamp(x, 0, Width - 1), Math.Clamp(z, 0, Depth - 1)];

    public int SampleBilinear(int worldXCentimeters, int worldZCentimeters, WorldGenerationProfile profile)
    {
        int localX = worldXCentimeters - profile.MinimumWorldXCentimeters;
        int localZ = worldZCentimeters - profile.MinimumWorldZCentimeters;
        int cell = profile.TopologyCellCentimeters;
        int x0 = Math.Clamp(WorldUnits.FloorDivide(localX, cell), 0, Width - 2);
        int z0 = Math.Clamp(WorldUnits.FloorDivide(localZ, cell), 0, Depth - 2);
        int fractionX = Math.Clamp(localX - x0 * cell, 0, cell);
        int fractionZ = Math.Clamp(localZ - z0 * cell, 0, cell);
        long a = Lerp(this[x0, z0], this[x0 + 1, z0], fractionX, cell);
        long b = Lerp(this[x0, z0 + 1], this[x0 + 1, z0 + 1], fractionX, cell);
        return checked((int)Lerp(a, b, fractionZ, cell));
    }

    public StateChecksum ComputeChecksum()
    {
        var writer = new CanonicalWriter();
        WriteCanonical(writer);
        return writer.ComputeChecksum();
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString(Id);
        writer.WriteInt32(Width);
        writer.WriteInt32(Depth);
        writer.WriteCount(_values.Length);
        foreach (int value in _values)
        {
            writer.WriteInt32(value);
        }
    }

    private static long Lerp(long left, long right, int numerator, int denominator) =>
        left + ((right - left) * numerator + denominator / 2) / denominator;
}
