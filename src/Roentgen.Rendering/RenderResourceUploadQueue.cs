using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

public enum RenderUploadPriority
{
    CriticalCollisionVisual = 0,
    NearTerrain = 10,
    WorldGeometry = 20,
    Decoration = 30,
    Background = 40
}

public sealed record RenderUploadRequest(
    string Id,
    long EstimatedBytes,
    RenderUploadPriority Priority,
    long Epoch,
    Action<GraphicsDevice> Execute,
    Func<bool>? IsCurrent = null,
    Action? Completed = null,
    Action<Exception>? Failed = null,
    Action? Skipped = null,
    CancellationToken CancellationToken = default);

public readonly record struct RenderUploadBudget(int MaximumOperations, long MaximumBytes, double MaximumMilliseconds)
{
    public void Validate()
    {
        if (MaximumOperations < 1 || MaximumBytes < 1 || MaximumMilliseconds <= 0d || !double.IsFinite(MaximumMilliseconds))
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumOperations), "Upload budgets must be positive and finite.");
        }
    }
}

public sealed record RenderUploadDrainResult(
    int Completed,
    int Skipped,
    int Failed,
    long UploadedBytes,
    double ElapsedMilliseconds);

public sealed record RenderUploadQueueSnapshot(
    int PendingCount,
    long PendingBytes,
    long AcceptedCount,
    long RejectedCount,
    long SkippedCount,
    long FailedCount);

public sealed class RenderResourceUploadQueue
{
    private readonly object _sync = new();
    private readonly PriorityQueue<QueuedUpload, UploadSortKey> _queue = new();
    private readonly int _maximumPendingOperations;
    private readonly long _maximumPendingBytes;
    private long _sequence;
    private long _pendingBytes;
    private long _accepted;
    private long _rejected;
    private long _skipped;
    private long _failed;

    public RenderResourceUploadQueue(int maximumPendingOperations = 512, long maximumPendingBytes = 256L * 1024L * 1024L)
    {
        if (maximumPendingOperations < 1 || maximumPendingBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPendingOperations));
        }

        _maximumPendingOperations = maximumPendingOperations;
        _maximumPendingBytes = maximumPendingBytes;
    }

    public int PendingCount
    {
        get
        {
            lock (_sync)
            {
                return _queue.Count;
            }
        }
    }

    public long PendingBytes
    {
        get
        {
            lock (_sync)
            {
                return _pendingBytes;
            }
        }
    }

    public RenderUploadQueueSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return new RenderUploadQueueSnapshot(_queue.Count, _pendingBytes, _accepted, _rejected, _skipped, _failed);
            }
        }
    }

    public bool TryEnqueue(RenderUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Id);
        ArgumentNullException.ThrowIfNull(request.Execute);
        if (request.EstimatedBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Estimated upload bytes cannot be negative.");
        }

        lock (_sync)
        {
            if (_queue.Count >= _maximumPendingOperations || _pendingBytes + request.EstimatedBytes > _maximumPendingBytes)
            {
                _rejected++;
                return false;
            }

            long sequence = ++_sequence;
            _queue.Enqueue(new QueuedUpload(request, Stopwatch.GetTimestamp()), new UploadSortKey((int)request.Priority, sequence));
            _pendingBytes += request.EstimatedBytes;
            _accepted++;
            return true;
        }
    }

    public void Enqueue(Action<GraphicsDevice> upload)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (!TryEnqueue(new RenderUploadRequest("legacy-upload", 1, RenderUploadPriority.WorldGeometry, 0, upload)))
        {
            throw new InvalidOperationException("The bounded render upload queue is full.");
        }
    }

    public int Drain(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, int maximumUploads)
    {
        RenderUploadDrainResult result = Drain(
            graphicsDevice,
            threadGuard,
            new RenderUploadBudget(maximumUploads, long.MaxValue, double.MaxValue));
        return result.Completed;
    }

    public RenderUploadDrainResult Drain(GraphicsDevice graphicsDevice, RenderThreadGuard threadGuard, RenderUploadBudget budget)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(threadGuard);
        budget.Validate();
        threadGuard.AssertAccess();
        var stopwatch = Stopwatch.StartNew();
        int completed = 0;
        int skipped = 0;
        int failed = 0;
        long uploadedBytes = 0;
        while (completed + skipped + failed < budget.MaximumOperations && stopwatch.Elapsed.TotalMilliseconds < budget.MaximumMilliseconds)
        {
            QueuedUpload queued;
            lock (_sync)
            {
                if (!_queue.TryPeek(out QueuedUpload peeked, out _))
                {
                    break;
                }

                if (completed + skipped + failed > 0 && uploadedBytes + peeked.Request.EstimatedBytes > budget.MaximumBytes)
                {
                    break;
                }

                _queue.Dequeue();
                _pendingBytes -= peeked.Request.EstimatedBytes;
                queued = peeked;
            }

            RenderUploadRequest request = queued.Request;
            if (request.CancellationToken.IsCancellationRequested || request.IsCurrent is not null && !request.IsCurrent())
            {
                skipped++;
                lock (_sync)
                {
                    _skipped++;
                }
                request.Skipped?.Invoke();
                continue;
            }

            try
            {
                request.Execute(graphicsDevice);
                uploadedBytes += request.EstimatedBytes;
                completed++;
                request.Completed?.Invoke();
            }
            catch (Exception exception)
            {
                failed++;
                lock (_sync)
                {
                    _failed++;
                }
                request.Failed?.Invoke(exception);
            }
        }

        stopwatch.Stop();
        return new RenderUploadDrainResult(completed, skipped, failed, uploadedBytes, stopwatch.Elapsed.TotalMilliseconds);
    }

    private sealed record QueuedUpload(RenderUploadRequest Request, long EnqueuedTimestamp);

    private readonly record struct UploadSortKey(int Priority, long Sequence) : IComparable<UploadSortKey>
    {
        public int CompareTo(UploadSortKey other)
        {
            int result = Priority.CompareTo(other.Priority);
            return result != 0 ? result : Sequence.CompareTo(other.Sequence);
        }
    }
}
