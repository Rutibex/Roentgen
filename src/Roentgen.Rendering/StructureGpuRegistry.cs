using Microsoft.Xna.Framework;
using Roentgen.Contracts;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Rendering;

public sealed class StructureGpuRegistry:IDisposable
{
    private readonly object _sync=new();private readonly Dictionary<(StableId,StructureCellLayer,StructureMaterialKind),ResidentBatch> _resident=new();private readonly HashSet<string> _pending=new(StringComparer.Ordinal);private bool _disposed;
    public int ResidentBatchCount{get{lock(_sync)return _resident.Count;}}

    public bool TryQueueUpload(RendererFoundation renderer,StructureBlueprint blueprint,StructureCellLayer layer,long epoch,Func<bool>? isCurrent=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(renderer);ArgumentNullException.ThrowIfNull(blueprint);IReadOnlyList<StructureRenderBatchData> batches=StructureRenderingBridge.CompileBatches(blueprint,layer);bool all=true;
        foreach(StructureRenderBatchData batch in batches)
        {
            string pendingId=$"{blueprint.Id}:{layer}:{batch.Material}:e{epoch}";lock(_sync){if(_resident.TryGetValue((blueprint.Id,layer,batch.Material),out var live)&&live.Epoch==epoch)continue;if(!_pending.Add(pendingId))continue;}
            var request=new RenderUploadRequest($"structure:{pendingId}",batch.EstimatedGpuBytes,layer==StructureCellLayer.Decoration?RenderUploadPriority.Decoration:RenderUploadPriority.WorldGeometry,epoch,device=>
            {
                GpuMesh mesh=GpuMesh.Upload(device,renderer.ThreadGuard,batch.Mesh);lock(_sync){var key=(blueprint.Id,layer,batch.Material);if(_resident.Remove(key,out ResidentBatch? previous))previous.Mesh.Dispose();_resident[key]=new ResidentBatch(mesh,StructureRenderingBridge.MaterialFor(batch.Material),epoch);_pending.Remove(pendingId);}
            },isCurrent,Skipped:()=>Clear(pendingId),Failed:_=>Clear(pendingId));
            if(!renderer.UploadQueue.TryEnqueue(request)){Clear(pendingId);all=false;break;}
        }
        return all;
    }

    public bool TryQueueUnloadLayer(RendererFoundation renderer,StableId structureId,StructureCellLayer layer,long epoch,Func<bool>? isCurrent=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(renderer);return renderer.UploadQueue.TryEnqueue(new RenderUploadRequest($"structure-unload:{structureId}:{layer}:e{epoch}",0,RenderUploadPriority.CriticalCollisionVisual,epoch,_=>{lock(_sync){foreach(var key in _resident.Keys.Where(key=>key.Item1==structureId&&key.Item2==layer).ToArray())if(_resident.Remove(key,out ResidentBatch? batch))batch.Mesh.Dispose();}},isCurrent));
    }

    public bool TryQueueUnload(RendererFoundation renderer,StableId structureId,long epoch)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);ArgumentNullException.ThrowIfNull(renderer);return renderer.UploadQueue.TryEnqueue(new RenderUploadRequest($"structure-unload:{structureId}:e{epoch}",0,RenderUploadPriority.CriticalCollisionVisual,epoch,_=>{lock(_sync){foreach(var key in _resident.Keys.Where(key=>key.Item1==structureId).ToArray())if(_resident.Remove(key,out ResidentBatch? batch))batch.Mesh.Dispose();}}));
    }

    public IReadOnlyList<RenderItem> BuildRenderItems(IEnumerable<StructureBlueprint> blueprints)
    {
        var origins=blueprints.ToDictionary(static blueprint=>blueprint.Id,static blueprint=>blueprint.WorldOrigin);lock(_sync){return _resident.OrderBy(static pair=>pair.Key.Item1).ThenBy(static pair=>pair.Key.Item2).ThenBy(static pair=>pair.Key.Item3).Where(pair=>origins.ContainsKey(pair.Key.Item1)).Select(pair=>{StructurePoint origin=origins[pair.Key.Item1];Matrix world=Matrix.CreateTranslation(WorldUnits.CentimetersToMeters(origin.XCentimeters),WorldUnits.CentimetersToMeters(origin.YCentimeters),WorldUnits.CentimetersToMeters(origin.ZCentimeters));return new RenderItem($"structure-{pair.Key.Item1}-{pair.Key.Item2}-{pair.Key.Item3}",pair.Value.Mesh,pair.Value.Material,world,true,true);}).ToArray();}}
    private void Clear(string id){lock(_sync)_pending.Remove(id);}
    public void Dispose(){if(_disposed)return;_disposed=true;lock(_sync){foreach(ResidentBatch batch in _resident.Values)batch.Mesh.Dispose();_resident.Clear();_pending.Clear();}}
    private sealed record ResidentBatch(GpuMesh Mesh,MaterialDefinition Material,long Epoch);
}
