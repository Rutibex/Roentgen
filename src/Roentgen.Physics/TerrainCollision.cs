using Roentgen.World;

namespace Roentgen.Physics;

public readonly record struct TerrainNormal(double X, double Y, double Z)
{
    public static TerrainNormal Up => new(0d, 1d, 0d);
}

public readonly record struct TerrainSurfaceSample(
    int HeightCentimeters,
    TerrainNormal Normal,
    TerrainSurfaceKind Surface,
    TerrainChunkAddress ChunkAddress);

public readonly record struct TerrainRaycastHit(
    WorldPoint Position,
    int HeightCentimeters,
    TerrainNormal Normal,
    TerrainSurfaceKind Surface,
    TerrainChunkAddress ChunkAddress,
    int TravelCentimeters);

public sealed class TerrainCollisionTile
{
    private readonly int[] _heights;
    private readonly TerrainSurfaceKind[] _surfaces;

    public TerrainCollisionTile(TerrainChunkAddress address, int chunkSizeCentimeters, TerrainChunkLodData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Lod != 0 || source.SpacingCentimeters * (source.SampleCount - 1) != chunkSizeCentimeters)
        {
            throw new ArgumentException("Terrain collision must be built from the complete LOD 0 height field.", nameof(source));
        }

        Address = address;
        ChunkSizeCentimeters = chunkSizeCentimeters;
        SampleCount = source.SampleCount;
        SpacingCentimeters = source.SpacingCentimeters;
        _heights = source.Heights.ToArray();
        _surfaces = source.Surfaces.ToArray();
    }

    public TerrainChunkAddress Address { get; }
    public int ChunkSizeCentimeters { get; }
    public int SampleCount { get; }
    public int SpacingCentimeters { get; }
    public int EstimatedBytes => checked(_heights.Length * sizeof(int) + _surfaces.Length);

    public bool TrySample(int worldXCentimeters, int worldZCentimeters, out TerrainSurfaceSample sample)
    {
        WorldPoint origin = Address.Origin(ChunkSizeCentimeters);
        int localX = worldXCentimeters - origin.XCentimeters;
        int localZ = worldZCentimeters - origin.ZCentimeters;
        if (localX < 0 || localZ < 0 || localX > ChunkSizeCentimeters || localZ > ChunkSizeCentimeters)
        {
            sample = default;
            return false;
        }

        int x0 = Math.Min(SampleCount - 2, localX / SpacingCentimeters);
        int z0 = Math.Min(SampleCount - 2, localZ / SpacingCentimeters);
        int fractionX = localX - x0 * SpacingCentimeters;
        int fractionZ = localZ - z0 * SpacingCentimeters;
        int a = HeightAt(x0, z0);
        int b = HeightAt(x0 + 1, z0);
        int c = HeightAt(x0, z0 + 1);
        int d = HeightAt(x0 + 1, z0 + 1);
        long top = Lerp(a, b, fractionX, SpacingCentimeters);
        long bottom = Lerp(c, d, fractionX, SpacingCentimeters);
        int height = checked((int)Lerp(top, bottom, fractionZ, SpacingCentimeters));
        int centerX = Math.Clamp((localX + SpacingCentimeters / 2) / SpacingCentimeters, 0, SampleCount - 1);
        int centerZ = Math.Clamp((localZ + SpacingCentimeters / 2) / SpacingCentimeters, 0, SampleCount - 1);
        int left = HeightAt(Math.Max(0, centerX - 1), centerZ);
        int right = HeightAt(Math.Min(SampleCount - 1, centerX + 1), centerZ);
        int back = HeightAt(centerX, Math.Max(0, centerZ - 1));
        int forward = HeightAt(centerX, Math.Min(SampleCount - 1, centerZ + 1));
        double nx = left - right;
        double ny = SpacingCentimeters * 2d;
        double nz = back - forward;
        double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        TerrainNormal normal = length <= double.Epsilon ? TerrainNormal.Up : new TerrainNormal(nx / length, ny / length, nz / length);
        sample = new TerrainSurfaceSample(height, normal, SurfaceAt(centerX, centerZ), Address);
        return true;
    }

    private int HeightAt(int x, int z) => _heights[checked(z * SampleCount + x)];
    private TerrainSurfaceKind SurfaceAt(int x, int z) => _surfaces[checked(z * SampleCount + x)];
    private static long Lerp(long left, long right, int numerator, int denominator) => left + ((right - left) * numerator + denominator / 2) / denominator;
}
