using System.Collections.Concurrent;

namespace Roentgen.World;

public sealed record TerrainStreamingSnapshot(
    long Epoch,
    TerrainChunkAddress FocusChunk,
    int Requested,
    int Generating,
    int AwaitingUpload,
    int Resident,
    int Dormant,
    int Failed,
    int PendingQueue,
    long CpuResidentBytes);

public sealed class TerrainStreamingCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly RegionalTopology _topology;
    private readonly TerrainChunkCompiler _compiler;
    private readonly PriorityQueue<GenerationTicket, RequestPriority> _pending = new();
    private readonly Dictionary<TerrainChunkAddress, ChunkEntry> _entries = [];
    private readonly ConcurrentQueue<TerrainChunkGenerationResult> _completed = new();
    private readonly ConcurrentQueue<(TerrainChunkAddress Address, long Epoch)> _unloaded = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task[] _workers;
    private TerrainChunkAddress _focusChunk;
    private long _epoch;
    private long _sequence;
    private bool _disposed;

    public TerrainStreamingCoordinator(RegionalTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        topology.Profile.Validate();
        _topology = topology;
        _compiler = new TerrainChunkCompiler(topology);
        _workers = Enumerable.Range(0, topology.Profile.WorkerCount)
            .Select(_ => Task.Run(WorkerLoopAsync))
            .ToArray();
    }

    public long Epoch => Volatile.Read(ref _epoch);

    public TerrainStreamingSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return BuildSnapshotLocked();
            }
        }
    }

    public void Update(WorldPoint focus)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WorldGenerationProfile profile = _topology.Profile;
        TerrainChunkAddress nextFocus = TerrainChunkAddress.FromWorldCentimeters(focus.XCentimeters, focus.ZCentimeters, profile.ChunkSizeCentimeters);
        lock (_sync)
        {
            if (nextFocus != _focusChunk || _epoch == 0)
            {
                _focusChunk = nextFocus;
                _epoch++;
            }

            var desired = new HashSet<TerrainChunkAddress>();
            for (int dz = -profile.StreamingRadiusChunks; dz <= profile.StreamingRadiusChunks; dz++)
            {
                for (int dx = -profile.StreamingRadiusChunks; dx <= profile.StreamingRadiusChunks; dx++)
                {
                    var address = new TerrainChunkAddress(nextFocus.X + dx, nextFocus.Z + dz);
                    if (!_compiler.IsAddressInsideWorld(address))
                    {
                        continue;
                    }

                    desired.Add(address);
                    int distance = address.ChebyshevDistance(nextFocus);
                    if (!_entries.TryGetValue(address, out ChunkEntry? entry))
                    {
                        entry = new ChunkEntry(address);
                        _entries.Add(address, entry);
                    }

                    entry.Distance = distance;
                    if (entry.State is TerrainChunkLifecycleState.Unrequested or TerrainChunkLifecycleState.Cancelled or TerrainChunkLifecycleState.Failed)
                    {
                        QueueLocked(entry, _epoch, distance);
                    }
                    else if (entry.State == TerrainChunkLifecycleState.Dormant)
                    {
                        entry.State = TerrainChunkLifecycleState.Resident;
                    }
                }
            }

            foreach (ChunkEntry entry in _entries.Values.ToArray())
            {
                int distance = entry.Address.ChebyshevDistance(nextFocus);
                entry.Distance = distance;
                if (desired.Contains(entry.Address))
                {
                    continue;
                }

                if (distance > profile.DormantRadiusChunks)
                {
                    switch (entry.State)
                    {
                        case TerrainChunkLifecycleState.Requested:
                        case TerrainChunkLifecycleState.GeneratingCpu:
                            entry.State = TerrainChunkLifecycleState.Cancelled;
                            entry.Ticket?.Cancel();
                            _entries.Remove(entry.Address);
                            break;
                        case TerrainChunkLifecycleState.Resident:
                        case TerrainChunkLifecycleState.Dormant:
                        case TerrainChunkLifecycleState.AwaitingUpload:
                            entry.State = TerrainChunkLifecycleState.Unloading;
                            entry.Chunk = null;
                            _unloaded.Enqueue((entry.Address, entry.Epoch));
                            _entries.Remove(entry.Address);
                            break;
                        case TerrainChunkLifecycleState.Unrequested:
                        case TerrainChunkLifecycleState.Cancelled:
                        case TerrainChunkLifecycleState.Failed:
                        case TerrainChunkLifecycleState.Unloading:
                            _entries.Remove(entry.Address);
                            break;
                    }
                }
                else if (entry.State == TerrainChunkLifecycleState.Resident)
                {
                    entry.State = TerrainChunkLifecycleState.Dormant;
                }
            }

            EnforceCpuBudgetLocked();
        }
    }

    public bool TryDequeueCompleted(out TerrainChunkGenerationResult result)
    {
        if (_completed.TryDequeue(out TerrainChunkGenerationResult? item) && item is not null)
        {
            result = item;
            return true;
        }

        result = null!;
        return false;
    }

    public bool TryDequeueUnloaded(out TerrainChunkAddress address, out long epoch)
    {
        if (_unloaded.TryDequeue(out (TerrainChunkAddress Address, long Epoch) item))
        {
            address = item.Address;
            epoch = item.Epoch;
            return true;
        }

        address = default;
        epoch = 0;
        return false;
    }

    public bool AcknowledgeResident(TerrainChunkAddress address, long epoch)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(address, out ChunkEntry? entry) || entry.Epoch != epoch || entry.State != TerrainChunkLifecycleState.AwaitingUpload)
            {
                return false;
            }

            entry.State = TerrainChunkLifecycleState.Resident;
            return true;
        }
    }

    public bool IsCurrent(TerrainChunkAddress address, long epoch)
    {
        lock (_sync)
        {
            return _entries.TryGetValue(address, out ChunkEntry? entry) && entry.Epoch == epoch && entry.State != TerrainChunkLifecycleState.Cancelled;
        }
    }

    public IReadOnlyList<(TerrainChunkAddress Address, TerrainChunkLifecycleState State, int Distance, long Epoch)> InspectEntries()
    {
        lock (_sync)
        {
            return _entries.Values
                .OrderBy(static entry => entry.Address)
                .Select(static entry => (entry.Address, entry.State, entry.Distance, entry.Epoch))
                .ToArray();
        }
    }

    private void QueueLocked(ChunkEntry entry, long epoch, int distance)
    {
        if (_pending.Count >= _topology.Profile.MaximumPendingGenerationRequests)
        {
            return;
        }

        var ticket = new GenerationTicket(entry.Address, epoch);
        entry.State = TerrainChunkLifecycleState.Requested;
        entry.Epoch = epoch;
        entry.Ticket = ticket;
        long sequence = ++_sequence;
        _pending.Enqueue(ticket, new RequestPriority(distance, sequence, entry.Address));
        _signal.Release();
    }

    private async Task WorkerLoopAsync()
    {
        CancellationToken shutdownToken = _shutdown.Token;
        try
        {
            while (true)
            {
                await _signal.WaitAsync(shutdownToken).ConfigureAwait(false);
                GenerationTicket? candidate;
                lock (_sync)
                {
                    if (!_pending.TryDequeue(out candidate, out _) || candidate is null ||
                        !_entries.TryGetValue(candidate.Address, out ChunkEntry? entry) || !ReferenceEquals(entry.Ticket, candidate) ||
                        entry.Epoch != candidate.Epoch || entry.State != TerrainChunkLifecycleState.Requested)
                    {
                        candidate?.Dispose();
                        continue;
                    }

                    entry.State = TerrainChunkLifecycleState.GeneratingCpu;
                }

                GenerationTicket ticket = candidate ?? throw new InvalidOperationException("The dequeued generation ticket was unexpectedly null.");

                TerrainChunkData? chunk = null;
                Exception? error = null;
                bool cancelled = false;
                try
                {
                    chunk = _compiler.Compile(ticket.Address, ticket.Epoch, ticket.Token);
                }
                catch (OperationCanceledException) when (ticket.IsCancellationRequested || shutdownToken.IsCancellationRequested)
                {
                    cancelled = true;
                }
                catch (Exception exception)
                {
                    error = exception;
                }

                bool publish = false;
                lock (_sync)
                {
                    if (_entries.TryGetValue(ticket.Address, out ChunkEntry? entry) && ReferenceEquals(entry.Ticket, ticket) &&
                        entry.Epoch == ticket.Epoch && entry.State != TerrainChunkLifecycleState.Cancelled)
                    {
                        entry.Ticket = null;
                        if (cancelled)
                        {
                            entry.State = TerrainChunkLifecycleState.Cancelled;
                        }
                        else if (error is not null || chunk is null)
                        {
                            entry.State = TerrainChunkLifecycleState.Failed;
                            entry.Error = error?.Message ?? "Unknown terrain generation failure.";
                            publish = true;
                        }
                        else
                        {
                            entry.Chunk = chunk;
                            entry.State = TerrainChunkLifecycleState.AwaitingUpload;
                            publish = true;
                        }
                    }
                }

                ticket.Dispose();
                if (publish)
                {
                    _completed.Enqueue(new TerrainChunkGenerationResult(ticket.Address, ticket.Epoch, chunk, error));
                }
            }
        }
        catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
        {
        }
    }

    private void EnforceCpuBudgetLocked()
    {
        long bytes = _entries.Values.Sum(static entry => (long)(entry.Chunk?.EstimatedCpuBytes ?? 0));
        if (bytes <= _topology.Profile.MaximumCpuResidentBytes)
        {
            return;
        }

        foreach (ChunkEntry entry in _entries.Values
                     .Where(static entry => entry.State == TerrainChunkLifecycleState.Dormant && entry.Chunk is not null)
                     .OrderByDescending(static entry => entry.Distance)
                     .ThenBy(static entry => entry.Address)
                     .ToArray())
        {
            bytes -= entry.Chunk!.EstimatedCpuBytes;
            entry.Chunk = null;
            entry.State = TerrainChunkLifecycleState.Unloading;
            _unloaded.Enqueue((entry.Address, entry.Epoch));
            _entries.Remove(entry.Address);
            if (bytes <= _topology.Profile.MaximumCpuResidentBytes)
            {
                break;
            }
        }
    }

    private TerrainStreamingSnapshot BuildSnapshotLocked()
    {
        int Count(TerrainChunkLifecycleState state) => _entries.Values.Count(entry => entry.State == state);
        return new TerrainStreamingSnapshot(
            _epoch,
            _focusChunk,
            Count(TerrainChunkLifecycleState.Requested),
            Count(TerrainChunkLifecycleState.GeneratingCpu),
            Count(TerrainChunkLifecycleState.AwaitingUpload),
            Count(TerrainChunkLifecycleState.Resident),
            Count(TerrainChunkLifecycleState.Dormant),
            Count(TerrainChunkLifecycleState.Failed),
            _pending.Count,
            _entries.Values.Sum(static entry => (long)(entry.Chunk?.EstimatedCpuBytes ?? 0)));
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
            foreach (ChunkEntry entry in _entries.Values)
            {
                entry.Ticket?.Cancel();
            }
        }

        _shutdown.Cancel();
        _signal.Release(_workers.Length);
        try
        {
            Task.WaitAll(_workers, TimeSpan.FromSeconds(5));
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(static item => item is OperationCanceledException))
        {
        }
        finally
        {
            lock (_sync)
            {
                foreach (ChunkEntry entry in _entries.Values)
                {
                    entry.Ticket?.Dispose();
                    entry.Ticket = null;
                }

                while (_pending.TryDequeue(out GenerationTicket? ticket, out _))
                {
                    ticket?.Dispose();
                }
            }

            _shutdown.Dispose();
            _signal.Dispose();
        }
    }

    private sealed class ChunkEntry(TerrainChunkAddress address)
    {
        public TerrainChunkAddress Address { get; } = address;
        public TerrainChunkLifecycleState State { get; set; } = TerrainChunkLifecycleState.Unrequested;
        public long Epoch { get; set; }
        public int Distance { get; set; }
        public TerrainChunkData? Chunk { get; set; }
        public string? Error { get; set; }
        public GenerationTicket? Ticket { get; set; }
    }

    private sealed class GenerationTicket(TerrainChunkAddress address, long epoch) : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        public TerrainChunkAddress Address { get; } = address;
        public long Epoch { get; } = epoch;
        public CancellationToken Token => _cancellation.Token;
        public bool IsCancellationRequested => _cancellation.IsCancellationRequested;
        public void Cancel() => _cancellation.Cancel();
        public void Dispose() => _cancellation.Dispose();
    }

    private readonly record struct RequestPriority(int Distance, long Sequence, TerrainChunkAddress Address) : IComparable<RequestPriority>
    {
        public int CompareTo(RequestPriority other)
        {
            int result = Distance.CompareTo(other.Distance);
            if (result != 0) return result;
            result = Sequence.CompareTo(other.Sequence);
            return result != 0 ? result : Address.CompareTo(other.Address);
        }
    }
}
