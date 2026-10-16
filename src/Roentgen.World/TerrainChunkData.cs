using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World;

public sealed class TerrainChunkLodData : ICanonicalWritable
{
    private readonly int[] _heights;
    private readonly TerrainSurfaceKind[] _surfaces;

    public TerrainChunkLodData(int lod, int sampleCount, int spacingCentimeters, IReadOnlyList<int> heights, IReadOnlyList<TerrainSurfaceKind> surfaces)
    {
        ArgumentNullException.ThrowIfNull(heights);
        ArgumentNullException.ThrowIfNull(surfaces);
        if (lod < 0 || sampleCount < 2 || spacingCentimeters <= 0 || heights.Count != sampleCount * sampleCount || surfaces.Count != heights.Count)
        {
            throw new ArgumentException("Terrain LOD dimensions are invalid.");
        }

        Lod = lod;
        SampleCount = sampleCount;
        SpacingCentimeters = spacingCentimeters;
        _heights = heights.ToArray();
        _surfaces = surfaces.ToArray();
    }

    public int Lod { get; }
    public int SampleCount { get; }
    public int SpacingCentimeters { get; }
    public IReadOnlyList<int> Heights => _heights;
    public IReadOnlyList<TerrainSurfaceKind> Surfaces => _surfaces;
    public int EstimatedBytes => checked(_heights.Length * sizeof(int) + _surfaces.Length);
    public int HeightAt(int x, int z) => _heights[checked(z * SampleCount + x)];
    public TerrainSurfaceKind SurfaceAt(int x, int z) => _surfaces[checked(z * SampleCount + x)];

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32(Lod);
        writer.WriteInt32(SampleCount);
        writer.WriteInt32(SpacingCentimeters);
        writer.WriteCount(_heights.Length);
        for (int index = 0; index < _heights.Length; index++)
        {
            writer.WriteInt32(_heights[index]);
            writer.WriteInt32((int)_surfaces[index]);
        }
    }
}

public sealed class TerrainChunkData : ICanonicalWritable
{
    public TerrainChunkData(
        StableId id,
        TerrainChunkAddress address,
        IReadOnlyList<TerrainChunkLodData> lods,
        GenerationProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(lods);
        ArgumentNullException.ThrowIfNull(provenance);
        if (id.IsEmpty || lods.Count == 0)
        {
            throw new ArgumentException("Terrain chunk requires an ID and at least one LOD.");
        }

        Id = id;
        Address = address;
        Lods = lods.OrderBy(static lod => lod.Lod).ToArray();
        Provenance = provenance;
        ContentChecksum = ComputeChecksum();
    }

    public StableId Id { get; }
    public TerrainChunkAddress Address { get; }
    public IReadOnlyList<TerrainChunkLodData> Lods { get; }
    public GenerationProvenance Provenance { get; }
    public StateChecksum ContentChecksum { get; }
    public int EstimatedCpuBytes => Lods.Sum(static lod => lod.EstimatedBytes);

    public TerrainChunkLodData GetLod(int lod)
    {
        if (lod < 0 || lod >= Lods.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(lod));
        }

        return Lods[lod];
    }

    public StateChecksum ComputeChecksum()
    {
        var writer = new CanonicalWriter();
        WriteCanonical(writer);
        return writer.ComputeChecksum();
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);
        writer.WriteInt32(Address.X);
        writer.WriteInt32(Address.Z);
        writer.WriteCount(Lods.Count);
        foreach (TerrainChunkLodData lod in Lods)
        {
            lod.WriteCanonical(writer);
        }
    }
}

public sealed record TerrainChunkGenerationRequest(TerrainChunkAddress Address, long Epoch, int Priority = 0);

public sealed record TerrainChunkGenerationResult(TerrainChunkAddress Address, long Epoch, TerrainChunkData? Chunk, Exception? Error)
{
    public bool Succeeded => Chunk is not null && Error is null;
}
