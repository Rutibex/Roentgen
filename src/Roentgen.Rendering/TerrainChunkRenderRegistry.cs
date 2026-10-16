using Microsoft.Xna.Framework;
using Roentgen.World;

namespace Roentgen.Rendering;

public sealed class TerrainChunkRenderRegistry : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<TerrainChunkAddress, ResidentTerrainMesh> _resident = [];
    private readonly Dictionary<TerrainChunkAddress, PendingTerrainMesh> _pending = [];
    private readonly MaterialDefinition _material = new("terrain-vertex-palette", Color.White, 0.96f, DoubleSided: true);
    private bool _disposed;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _resident.Count;
            }
        }
    }

    public bool TryGetResidentLod(TerrainChunkAddress address, out int lod)
    {
        lock (_sync)
        {
            if (_resident.TryGetValue(address, out ResidentTerrainMesh? resident))
            {
                lod = resident.Lod;
                return true;
            }
        }

        lod = -1;
        return false;
    }

    public bool HasPendingUpload(TerrainChunkAddress address)
    {
        lock (_sync)
        {
            return _pending.ContainsKey(address);
        }
    }

    public bool TryQueueUpload(
        RendererFoundation renderer,
        TerrainStreamingCoordinator streaming,
        TerrainChunkGenerationResult result,
        int lod) => QueueUpload(renderer, streaming, result, lod, acknowledgeInitialResidency: true);

    public bool TryQueueLodReplacement(
        RendererFoundation renderer,
        TerrainStreamingCoordinator streaming,
        TerrainChunkGenerationResult result,
        int lod) => QueueUpload(renderer, streaming, result, lod, acknowledgeInitialResidency: false);

    private bool QueueUpload(
        RendererFoundation renderer,
        TerrainStreamingCoordinator streaming,
        TerrainChunkGenerationResult result,
        int lod,
        bool acknowledgeInitialResidency)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(streaming);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded)
        {
            return true;
        }

        if (result.Chunk is not TerrainChunkData chunk)
        {
            return true;
        }

        lock (_sync)
        {
            if (_pending.ContainsKey(result.Address))
            {
                return true;
            }

            if (_resident.TryGetValue(result.Address, out ResidentTerrainMesh? resident) && resident.Epoch == result.Epoch && resident.Lod == lod)
            {
                return true;
            }

            _pending.Add(result.Address, new PendingTerrainMesh(result.Epoch, lod));
        }

        MeshData meshData;
        try
        {
            meshData = TerrainRenderingBridge.ToMeshData(chunk, lod);
        }
        catch
        {
            ClearPending(result.Address, result.Epoch, lod);
            throw;
        }

        void Clear() => ClearPending(result.Address, result.Epoch, lod);
        var request = new RenderUploadRequest(
            $"terrain:{result.Address}:e{result.Epoch}:lod{lod}",
            TerrainRenderingBridge.EstimateGpuUploadBytes(meshData),
            lod == 0 ? RenderUploadPriority.NearTerrain : RenderUploadPriority.WorldGeometry,
            result.Epoch,
            device =>
            {
                GpuMesh uploaded = GpuMesh.Upload(device, renderer.ThreadGuard, meshData);
                lock (_sync)
                {
                    if (_resident.Remove(result.Address, out ResidentTerrainMesh? previous))
                    {
                        previous.Mesh.Dispose();
                    }

                    _resident[result.Address] = new ResidentTerrainMesh(uploaded, result.Epoch, lod);
                    _pending.Remove(result.Address);
                }
            },
            () => streaming.IsCurrent(result.Address, result.Epoch),
            acknowledgeInitialResidency ? () => streaming.AcknowledgeResident(result.Address, result.Epoch) : null,
            _ => Clear(),
            Clear);
        if (renderer.UploadQueue.TryEnqueue(request))
        {
            return true;
        }

        Clear();
        return false;
    }

    public bool TryQueueUnload(RendererFoundation renderer, TerrainChunkAddress address, long epoch)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.UploadQueue.TryEnqueue(new RenderUploadRequest(
            $"terrain-unload:{address}:e{epoch}",
            0,
            RenderUploadPriority.CriticalCollisionVisual,
            epoch,
            _ =>
            {
                lock (_sync)
                {
                    _pending.Remove(address);
                    if (_resident.Remove(address, out ResidentTerrainMesh? resident))
                    {
                        resident.Mesh.Dispose();
                    }
                }
            }));
    }

    public IReadOnlyList<RenderItem> BuildRenderItems(WorldGenerationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        lock (_sync)
        {
            return _resident
                .OrderBy(static pair => pair.Key)
                .Select(pair =>
                {
                    WorldPoint origin = pair.Key.Origin(profile.ChunkSizeCentimeters);
                    Matrix world = Matrix.CreateTranslation(
                        WorldUnits.CentimetersToMeters(origin.XCentimeters),
                        0f,
                        WorldUnits.CentimetersToMeters(origin.ZCentimeters));
                    return new RenderItem($"terrain-{pair.Key}", pair.Value.Mesh, _material, world, CastsShadow: true, ReceivesShadow: true);
                })
                .ToArray();
        }
    }

    private void ClearPending(TerrainChunkAddress address, long epoch, int lod)
    {
        lock (_sync)
        {
            if (_pending.TryGetValue(address, out PendingTerrainMesh? pending) && pending.Epoch == epoch && pending.Lod == lod)
            {
                _pending.Remove(address);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            foreach (ResidentTerrainMesh resident in _resident.Values)
            {
                resident.Mesh.Dispose();
            }

            _resident.Clear();
            _pending.Clear();
        }
    }

    private sealed record ResidentTerrainMesh(GpuMesh Mesh, long Epoch, int Lod);
    private sealed record PendingTerrainMesh(long Epoch, int Lod);
}
