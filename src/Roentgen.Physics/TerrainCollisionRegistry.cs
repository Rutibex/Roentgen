using Roentgen.World;

namespace Roentgen.Physics;

public sealed class TerrainCollisionRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<TerrainChunkAddress, TerrainCollisionTile> _tiles = [];
    private readonly int _chunkSizeCentimeters;

    public TerrainCollisionRegistry(int chunkSizeCentimeters)
    {
        if (chunkSizeCentimeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSizeCentimeters));
        }

        _chunkSizeCentimeters = chunkSizeCentimeters;
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _tiles.Count;
            }
        }
    }

    public long EstimatedBytes
    {
        get
        {
            lock (_sync)
            {
                return _tiles.Values.Sum(static tile => (long)tile.EstimatedBytes);
            }
        }
    }

    public void Register(TerrainChunkData chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var tile = new TerrainCollisionTile(chunk.Address, _chunkSizeCentimeters, chunk.GetLod(0));
        lock (_sync)
        {
            _tiles[chunk.Address] = tile;
        }
    }

    public bool Remove(TerrainChunkAddress address)
    {
        lock (_sync)
        {
            return _tiles.Remove(address);
        }
    }

    public bool TrySample(int worldXCentimeters, int worldZCentimeters, out TerrainSurfaceSample sample)
    {
        TerrainChunkAddress address = TerrainChunkAddress.FromWorldCentimeters(worldXCentimeters, worldZCentimeters, _chunkSizeCentimeters);
        lock (_sync)
        {
            if (_tiles.TryGetValue(address, out TerrainCollisionTile? tile) && tile.TrySample(worldXCentimeters, worldZCentimeters, out sample))
            {
                return true;
            }

            // Exact shared-edge coordinates normally resolve to the chunk on the positive side.
            // Fall back to negative-side neighbors so collision remains continuous while only
            // one of the two visual chunks is resident.
            bool onXEdge = WorldUnits.PositiveModulo(worldXCentimeters, _chunkSizeCentimeters) == 0;
            bool onZEdge = WorldUnits.PositiveModulo(worldZCentimeters, _chunkSizeCentimeters) == 0;
            if (onXEdge && TrySampleLocked(new TerrainChunkAddress(address.X - 1, address.Z), worldXCentimeters, worldZCentimeters, out sample))
            {
                return true;
            }

            if (onZEdge && TrySampleLocked(new TerrainChunkAddress(address.X, address.Z - 1), worldXCentimeters, worldZCentimeters, out sample))
            {
                return true;
            }

            if (onXEdge && onZEdge && TrySampleLocked(new TerrainChunkAddress(address.X - 1, address.Z - 1), worldXCentimeters, worldZCentimeters, out sample))
            {
                return true;
            }
        }

        sample = default;
        return false;
    }

    private bool TrySampleLocked(TerrainChunkAddress address, int worldXCentimeters, int worldZCentimeters, out TerrainSurfaceSample sample)
    {
        if (_tiles.TryGetValue(address, out TerrainCollisionTile? tile))
        {
            return tile.TrySample(worldXCentimeters, worldZCentimeters, out sample);
        }

        sample = default;
        return false;
    }

    public bool TryRaycastDown(int worldXCentimeters, int originHeightCentimeters, int worldZCentimeters, int maximumDistanceCentimeters, out TerrainRaycastHit hit)
    {
        if (maximumDistanceCentimeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistanceCentimeters));
        }

        if (TrySample(worldXCentimeters, worldZCentimeters, out TerrainSurfaceSample sample))
        {
            int travel = originHeightCentimeters - sample.HeightCentimeters;
            if (travel >= 0 && travel <= maximumDistanceCentimeters)
            {
                hit = new TerrainRaycastHit(
                    new WorldPoint(worldXCentimeters, worldZCentimeters),
                    sample.HeightCentimeters,
                    sample.Normal,
                    sample.Surface,
                    sample.ChunkAddress,
                    travel);
                return true;
            }
        }

        hit = default;
        return false;
    }

    public bool TryResolveCapsuleGround(
        int worldXCentimeters,
        int capsuleBottomCentimeters,
        int worldZCentimeters,
        int stepHeightCentimeters,
        out int correctedBottomCentimeters,
        out TerrainSurfaceSample sample)
    {
        if (stepHeightCentimeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stepHeightCentimeters));
        }

        if (!TrySample(worldXCentimeters, worldZCentimeters, out sample))
        {
            correctedBottomCentimeters = capsuleBottomCentimeters;
            return false;
        }

        int difference = sample.HeightCentimeters - capsuleBottomCentimeters;
        if (difference > stepHeightCentimeters)
        {
            correctedBottomCentimeters = capsuleBottomCentimeters;
            return false;
        }

        correctedBottomCentimeters = Math.Max(capsuleBottomCentimeters, sample.HeightCentimeters);
        return true;
    }
}
