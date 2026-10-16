using System.Collections.Concurrent;
using System.Threading.Channels;
using Roentgen.Contracts;

namespace Roentgen.World.Structures;

public sealed record StructureStreamingOptions(int WorkerCount=2,int RequestCapacity=64,int ResultCapacity=64,long MaximumResidentCpuBytes=128L*1024L*1024L)
{
    public void Validate(){if(WorkerCount is <1 or >16||RequestCapacity<1||ResultCapacity<1||MaximumResidentCpuBytes<1024*1024)throw new InvalidOperationException("Structure streaming options are invalid.");}
}

public sealed record StructureStreamingRequest(StableId StructureId,StructureCellLayer Layer,int Priority,long Epoch,CancellationToken CancellationToken);
public sealed record StructureStreamingProduct(StableId StructureId,StructureCellLayer Layer,long Epoch,IReadOnlyList<StructureStreamingCell> Cells,IReadOnlyList<StructurePrimitive> Primitives,IReadOnlyList<DoorDefinition> Doors,long EstimatedCpuBytes);
public sealed record StructureStreamingResult(StableId StructureId,StructureCellLayer Layer,long Epoch,StructureStreamingProduct? Product,Exception? Error,bool Cancelled)
{
    public bool Succeeded=>Product is not null&&Error is null&&!Cancelled;
}
public sealed record StructureStreamingTelemetry(int QueuedRequests,int QueuedResults,int ActiveWorkers,int ResidentProducts,long ResidentCpuBytes,long RejectedRequests,long CancelledRequests,long StaleResults);

public sealed class StructureStreamingCoordinator:IAsyncDisposable
{
    private readonly IReadOnlyDictionary<StableId,StructureBlueprint> _blueprints;
    private readonly StructureStreamingOptions _options;
    private readonly Channel<StructureStreamingRequest>[] _requestQueues;
    private readonly SemaphoreSlim _requestSignal = new(0);
    private readonly SemaphoreSlim _requestCapacity;
    private readonly object _requestSubmissionGate = new();
    private readonly Channel<StructureStreamingResult> _results;
    private readonly ConcurrentDictionary<(StableId,StructureCellLayer),long> _epochs=new();
    private readonly ConcurrentDictionary<(StableId,StructureCellLayer),StructureStreamingProduct> _resident=new();
    private readonly CancellationTokenSource _shutdown=new();
    private readonly Task[] _workers;
    private long _epochCounter,_residentBytes,_rejected,_cancelled,_stale; private int _queuedRequests,_queuedResults,_activeWorkers;

    public StructureStreamingCoordinator(IEnumerable<StructureBlueprint> blueprints,StructureStreamingOptions? options=null)
    {
        ArgumentNullException.ThrowIfNull(blueprints); _blueprints=blueprints.ToDictionary(static blueprint=>blueprint.Id); _options=options??new StructureStreamingOptions();_options.Validate();
        _requestCapacity=new SemaphoreSlim(_options.RequestCapacity,_options.RequestCapacity);
        _requestQueues=Enumerable.Range(0,3).Select(_=>Channel.CreateBounded<StructureStreamingRequest>(new BoundedChannelOptions(_options.RequestCapacity){FullMode=BoundedChannelFullMode.Wait,SingleReader=false,SingleWriter=false})).ToArray();
        _results=Channel.CreateBounded<StructureStreamingResult>(new BoundedChannelOptions(_options.ResultCapacity){FullMode=BoundedChannelFullMode.Wait,SingleReader=false,SingleWriter=false});
        _workers=Enumerable.Range(0,_options.WorkerCount).Select(_=>Task.Run(WorkerLoopAsync)).ToArray();
    }

    public bool TryRequest(StableId structureId,StructureCellLayer layer,int priority,CancellationToken cancellationToken,out long epoch)
    {
        ObjectDisposedException.ThrowIf(_shutdown.IsCancellationRequested,this); if(!_blueprints.ContainsKey(structureId))throw new KeyNotFoundException($"Unknown structure {structureId}.");
        lock(_requestSubmissionGate)
        {
            if(!_requestCapacity.Wait(0)){epoch=0;Interlocked.Increment(ref _rejected);return false;}
            var key=(structureId,layer); bool hadPrevious=_epochs.TryGetValue(key,out long previousEpoch);
            epoch=Interlocked.Increment(ref _epochCounter); _epochs[key]=epoch; var request=new StructureStreamingRequest(structureId,layer,priority,epoch,cancellationToken);
            Channel<StructureStreamingRequest> queue=_requestQueues[GetPriorityQueueIndex(priority)];
            if(!queue.Writer.TryWrite(request))
            {
                if(hadPrevious)_epochs[key]=previousEpoch;else _epochs.TryRemove(key,out _);
                _requestCapacity.Release();Interlocked.Increment(ref _rejected);return false;
            }
            Interlocked.Increment(ref _queuedRequests);_requestSignal.Release();return true;
        }
    }

    public bool TryReadResult(out StructureStreamingResult? result)
    {
        while (true)
        {
            if(!_results.Reader.TryRead(out result))return false;Interlocked.Decrement(ref _queuedResults);
            if(_epochs.TryGetValue((result.StructureId,result.Layer),out long current)&&current==result.Epoch) break;
            Interlocked.Increment(ref _stale);
        }
        if(result.Succeeded&&result.Product is not null)TryMakeResident(result.Product);
        return true;
    }

    public bool TryGetResident(StableId structureId,StructureCellLayer layer,out StructureStreamingProduct? product)=>_resident.TryGetValue((structureId,layer),out product);
    public bool IsCurrent(StableId structureId,StructureCellLayer layer,long epoch)=>_epochs.TryGetValue((structureId,layer),out long current)&&current==epoch;

    public void UnloadLayer(StableId structureId,StructureCellLayer layer)
    {
        var key=(structureId,layer);if(_resident.TryRemove(key,out StructureStreamingProduct? product))Interlocked.Add(ref _residentBytes,-product.EstimatedCpuBytes);_epochs.TryRemove(key,out _);
    }

    public void Unload(StableId structureId)
    {
        foreach(var key in _resident.Keys.Where(key=>key.Item1==structureId).ToArray())if(_resident.TryRemove(key,out StructureStreamingProduct? product))Interlocked.Add(ref _residentBytes,-product.EstimatedCpuBytes);
        foreach(var key in _epochs.Keys.Where(key=>key.Item1==structureId).ToArray())_epochs.TryRemove(key,out _);
    }

    public StructureStreamingTelemetry CaptureTelemetry()=>new(Volatile.Read(ref _queuedRequests),Volatile.Read(ref _queuedResults),Volatile.Read(ref _activeWorkers),_resident.Count,Interlocked.Read(ref _residentBytes),Interlocked.Read(ref _rejected),Interlocked.Read(ref _cancelled),Interlocked.Read(ref _stale));

    private async Task WorkerLoopAsync()
    {
        try
        {
            while(true)
            {
                await _requestSignal.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                if(!TryDequeueHighestPriority(out StructureStreamingRequest? request)||request is null)continue;
                _requestCapacity.Release();Interlocked.Decrement(ref _queuedRequests);Interlocked.Increment(ref _activeWorkers);StructureStreamingResult result;
                try
                {
                    if(request.CancellationToken.IsCancellationRequested||!IsCurrent(request.StructureId,request.Layer,request.Epoch)){Interlocked.Increment(ref _cancelled);result=new StructureStreamingResult(request.StructureId,request.Layer,request.Epoch,null,null,true);}
                    else result=Compile(request);
                }
                catch(Exception exception){result=new StructureStreamingResult(request.StructureId,request.Layer,request.Epoch,null,exception,false);}
                finally{Interlocked.Decrement(ref _activeWorkers);}
                await _results.Writer.WriteAsync(result,_shutdown.Token).ConfigureAwait(false);Interlocked.Increment(ref _queuedResults);
            }
        }
        catch(OperationCanceledException)when(_shutdown.IsCancellationRequested){}
    }

    private bool TryDequeueHighestPriority(out StructureStreamingRequest? request)
    {
        foreach(Channel<StructureStreamingRequest> queue in _requestQueues)if(queue.Reader.TryRead(out request))return true;
        request=null;return false;
    }

    // Higher values receive earlier service while all queues share one global bounded capacity.
    private static int GetPriorityQueueIndex(int priority)=>priority>=100?0:priority>=0?1:2;

    private StructureStreamingResult Compile(StructureStreamingRequest request)
    {
        StructureBlueprint blueprint=_blueprints[request.StructureId];
        StructureStreamingCell[] cells=blueprint.StreamingCells.Where(cell=>cell.Layer==request.Layer).OrderBy(static cell=>cell.Id).ToArray();
        HashSet<StableId> cellIds=cells.Select(static cell=>cell.Id).ToHashSet();
        StructurePrimitive[] primitives=blueprint.Primitives.Where(primitive=>cellIds.Contains(primitive.StreamingCellId)&&!primitive.RemovedByDamage).OrderBy(static primitive=>primitive.Id).ToArray();
        DoorDefinition[] doors=blueprint.Doors.Where(door=>cells.Any(cell=>cell.PortalDoorIds.Contains(door.Id))).OrderBy(static door=>door.Id).ToArray();
        long bytes=checked(cells.Length*192L+primitives.Length*160L+doors.Length*128L);
        var product=new StructureStreamingProduct(request.StructureId,request.Layer,request.Epoch,cells,primitives,doors,bytes);
        return new StructureStreamingResult(request.StructureId,request.Layer,request.Epoch,product,null,false);
    }

    private void TryMakeResident(StructureStreamingProduct product)
    {
        var key=(product.StructureId,product.Layer); if(_resident.TryGetValue(key,out StructureStreamingProduct? previous))Interlocked.Add(ref _residentBytes,-previous.EstimatedCpuBytes);
        long after=Interlocked.Add(ref _residentBytes,product.EstimatedCpuBytes); if(after>_options.MaximumResidentCpuBytes){Interlocked.Add(ref _residentBytes,-product.EstimatedCpuBytes);if(previous is not null)Interlocked.Add(ref _residentBytes,previous.EstimatedCpuBytes);return;}_resident[key]=product;
    }

    public async ValueTask DisposeAsync()
    {
        if(_shutdown.IsCancellationRequested)return;_shutdown.Cancel();foreach(Channel<StructureStreamingRequest> queue in _requestQueues)queue.Writer.TryComplete();try{await Task.WhenAll(_workers).ConfigureAwait(false);}catch(OperationCanceledException){} _results.Writer.TryComplete();_requestSignal.Dispose();_requestCapacity.Dispose();_shutdown.Dispose();
    }
}
