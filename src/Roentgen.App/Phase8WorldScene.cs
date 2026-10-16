using Microsoft.Xna.Framework;
using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Physics;
using Roentgen.Rendering;
using Roentgen.Simulation;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.App;

internal sealed class Phase8WorldScene:IDisposable
{
    private readonly RendererFoundation _renderer;private readonly RegionalTopology _topology;private readonly TerrainStreamingCoordinator _terrainStreaming;private readonly TerrainChunkRenderRegistry _terrainRendering=new();private readonly TerrainCollisionRegistry _terrainCollision;private readonly GeneratedStructureWorld _structureWorld;private readonly StructureGpuRegistry _structureRendering=new();private readonly StructureCollisionRegistry _structureCollision=new();private readonly DynamicDoorCollisionRegistry _doorCollision=new();private readonly StructureStreamingCoordinator _structureStreaming;private readonly Queue<TerrainChunkGenerationResult> _uploadBacklog=new();private readonly Queue<(TerrainChunkAddress,long)> _unloadBacklog=new();private readonly Dictionary<TerrainChunkAddress,TerrainChunkGenerationResult> _cpuChunks=[];private readonly Dictionary<StableId,int> _structureResidency=[];private readonly List<GpuMesh> _ownedMeshes=[];private readonly RenderItem _distantTerrain;private readonly InteractionWorldDefinition _interactionWorld;private readonly DynamicInteractionRendering _dynamicRendering;private bool _disposed;
    private readonly Dictionary<(StableId,StructureCellLayer),long> _requestedLayers = [];
    private readonly Dictionary<(StableId,StructureCellLayer),StructureStreamingResult> _structureUploads = [];
    private readonly HashSet<(StableId,StructureCellLayer)> _structureUnloads = [];
    private long _unloadEpoch;

    public Phase8WorldScene(RendererFoundation renderer, CampaignSeed? campaignSeed = null, WorldGenerationProfile? profile = null)
    {
        _renderer=renderer;var seed=campaignSeed ?? new CampaignSeed(0x524F454E5447454EUL);_topology=new RegionalTopologyGenerator(seed).Generate(profile ?? WorldGenerationProfile.MicroRegion);_terrainStreaming=new TerrainStreamingCoordinator(_topology);_terrainCollision=new TerrainCollisionRegistry(_topology.Profile.ChunkSizeCentimeters);_structureWorld=new MicroRegionStructureCompiler(_topology).Compile();foreach(StructureBlueprint structure in _structureWorld.Structures)_structureCollision.Register(new StructureCollisionCompiler().Compile(structure));_interactionWorld=CreateInteractionWorld();_structureStreaming=new StructureStreamingCoordinator(_structureWorld.Structures);_dynamicRendering=new DynamicInteractionRendering(renderer);GpuMesh distant=Own(renderer.UploadNow(TerrainRenderingBridge.CreateDistantMesh(_topology)));_distantTerrain=new RenderItem("distant-regional-topology",distant,new MaterialDefinition("distant-terrain",Color.White,1f),Matrix.Identity,false,true);LandmarkReservation start=_topology.Reservations.First(static value=>value.Purpose==LandmarkPurpose.Settlement);_terrainStreaming.Update(start.Center);
    }
    private InteractionWorldDefinition CreateInteractionWorld()
    {
        var original=new InteractionWorldCompiler().Compile(_structureWorld.Structures);
        var targets=original.Targets.ToList();
        Add(Roentgen.Narrative.Phase16Content.ShipmentManifestId,BuildingPurpose.IndustrialIntake,InteractiveTargetKind.Pickup,"Shipment manifest",InteractionVerb.Take);
        Add(Roentgen.Narrative.Phase16Content.MeridianRelayId,BuildingPurpose.UtilityRelay,InteractiveTargetKind.Terminal,"Quarantine routing console",InteractionVerb.Activate);
        return new InteractionWorldDefinition(targets,original.Doors);
        void Add(StableId id,BuildingPurpose purpose,InteractiveTargetKind kind,string name,InteractionVerb verb)
        {
            var structure=_structureWorld.Structures.First(s=>s.Grammar.Purpose==purpose);
            var room=structure.Rooms.First(r=>r.Floor==0&&r.Archetype is not (RoomArchetype.Corridor or RoomArchetype.Stairwell));
            int x=structure.WorldOrigin.XCentimeters+room.Bounds.CenterXCentimeters;
            int z=structure.WorldOrigin.ZCentimeters+room.Bounds.CenterZCentimeters+100;
            int y=structure.WorldOrigin.YCentimeters+95;
            var center=new StructurePoint(x,y,z);var bounds=new StructureBox(new(x-25,y-35,z-25),new(x+25,y+35,z+25));
            targets.Add(new InteractiveTargetDefinition(id,structure.Id,kind,name,center,bounds,id,new[]{verb,InteractionVerb.Inspect},true));
        }
    }
    public RegionalTopology Topology=>_topology;public IReadOnlyList<StructureBlueprint> Structures=>_structureWorld.Structures;public TerrainStreamingSnapshot StreamingSnapshot=>_terrainStreaming.Snapshot;public InteractionWorldDefinition InteractionWorld=>_interactionWorld;public DynamicDoorCollisionRegistry DoorCollision=>_doorCollision;public StructureCollisionRegistry StructureCollision=>_structureCollision;public TerrainCollisionRegistry TerrainCollision=>_terrainCollision;
    public FixedVector3 SpawnPosition{get{LandmarkReservation start=_topology.Reservations.First(static value=>value.Purpose==LandmarkPurpose.Settlement);int h=new TerrainHeightEvaluator(_topology).SampleHeightCentimeters(start.Center.XCentimeters,start.Center.ZCentimeters);return new FixedVector3((long)start.Center.XCentimeters*10,(long)(h+10)*10,(long)(start.Center.ZCentimeters+2200)*10);}}
    public void Update(FixedVector3 playerPosition)
    {
        WorldPoint focus=new((int)(playerPosition.XMillimeters/10),(int)(playerPosition.ZMillimeters/10));_terrainStreaming.Update(focus);DrainTerrain();ReconcileResidentLods();UpdateStructures(playerPosition);
    }
    private void DrainTerrain()
    {
        while(_terrainStreaming.TryDequeueCompleted(out TerrainChunkGenerationResult result)){if(!result.Succeeded||!_terrainStreaming.IsCurrent(result.Address,result.Epoch)||result.Chunk is not TerrainChunkData chunk)continue;_terrainCollision.Register(chunk);_cpuChunks[result.Address]=result;_uploadBacklog.Enqueue(result);}while(_terrainStreaming.TryDequeueUnloaded(out TerrainChunkAddress address,out long epoch)){_terrainCollision.Remove(address);_cpuChunks.Remove(address);_unloadBacklog.Enqueue((address,epoch));}
        int attempts=_uploadBacklog.Count;for(int i=0;i<attempts;i++){TerrainChunkGenerationResult r=_uploadBacklog.Peek();int d=r.Address.ChebyshevDistance(StreamingSnapshot.FocusChunk);int lod=d<=1?0:d<=2?1:2;if(!_terrainRendering.TryQueueUpload(_renderer,_terrainStreaming,r,lod))break;_uploadBacklog.Dequeue();}
        attempts=_unloadBacklog.Count;for(int i=0;i<attempts;i++){var value=_unloadBacklog.Peek();if(!_terrainRendering.TryQueueUnload(_renderer,value.Item1,value.Item2))break;_unloadBacklog.Dequeue();}
    }
    private void ReconcileResidentLods()
    {
        foreach(var pair in _cpuChunks.OrderBy(p=>p.Key))
        {
            if(!_terrainStreaming.IsCurrent(pair.Key,pair.Value.Epoch)||_terrainRendering.HasPendingUpload(pair.Key))continue;
            int distance=pair.Key.ChebyshevDistance(StreamingSnapshot.FocusChunk);
            int lod=distance<=1?0:distance<=2?1:2;
            if(_terrainRendering.TryGetResidentLod(pair.Key,out int resident)&&resident!=lod)
                if(!_terrainRendering.TryQueueLodReplacement(_renderer,_terrainStreaming,pair.Value,lod))break;
        }
    }
    private void UpdateStructures(FixedVector3 player)
    {
        foreach(StructureBlueprint structure in _structureWorld.Structures)
        {
            // Distance to the actual footprint, not a shared settlement origin.
            double x=player.XMillimeters/10.0-structure.WorldOrigin.XCentimeters;
            double z=player.ZMillimeters/10.0-structure.WorldOrigin.ZCentimeters;
            double dx=Math.Max(0,Math.Max(structure.Footprint.MinimumXCentimeters-x,x-structure.Footprint.MaximumXCentimeters));
            double dz=Math.Max(0,Math.Max(structure.Footprint.MinimumZCentimeters-z,z-structure.Footprint.MaximumZCentimeters));
            double distance=Math.Sqrt(dx*dx+dz*dz);
            int desired=distance<4000?3:distance<10000?2:distance<30000?1:0;
            for(int level=0;level<3;level++)
            {
                var layer=(StructureCellLayer)level;var key=(structure.Id,layer);
                if(level<desired)
                {
                    _structureUnloads.Remove(key);
                    if(!_requestedLayers.ContainsKey(key)&&_structureStreaming.TryRequest(structure.Id,layer,100-level*20,CancellationToken.None,out long epoch))
                        _requestedLayers[key]=epoch;
                }
                else if(_requestedLayers.Remove(key))
                {
                    _structureStreaming.UnloadLayer(structure.Id,layer);_structureUploads.Remove(key);_structureUnloads.Add(key);
                }
            }
        }
        while(_structureStreaming.TryReadResult(out StructureStreamingResult? result)&&result is not null)
        {
            var key=(result.StructureId,result.Layer);
            if(!_requestedLayers.TryGetValue(key,out long epoch)||epoch!=result.Epoch)continue;
            if(!result.Succeeded){_requestedLayers.Remove(key);continue;}
            _structureUploads[key]=result;
        }
        foreach(var pair in _structureUnloads.ToArray())
        {
            if(_structureRendering.TryQueueUnloadLayer(_renderer,pair.Item1,pair.Item2,++_unloadEpoch,()=>!_requestedLayers.ContainsKey(pair)))_structureUnloads.Remove(pair);
            else break;
        }
        foreach(var pair in _structureUploads.ToArray())
        {
            StructureStreamingResult result=pair.Value;
            StructureBlueprint blueprint=_structureWorld.Structures.First(v=>v.Id==result.StructureId);
            if(_structureRendering.TryQueueUpload(_renderer,blueprint,result.Layer,result.Epoch,()=>_structureStreaming.IsCurrent(result.StructureId,result.Layer,result.Epoch)))_structureUploads.Remove(pair.Key);
            else break;
        }
    }
    public RenderFrameSnapshot Build(long tick,CameraState camera,bool showDebug,PlayerSimulationRuntime runtime)
    {
        var items=new List<RenderItem>{_distantTerrain};items.AddRange(_terrainRendering.BuildRenderItems(_topology.Profile));items.AddRange(_structureRendering.BuildRenderItems(_structureWorld.Structures));items.AddRange(_dynamicRendering.Build(_interactionWorld,runtime.Doors,runtime.Objects,runtime.Player.FocusedTargetId));IReadOnlyList<DebugLine> baseLines=showDebug?BuildDebug(runtime):Array.Empty<DebugLine>();IReadOnlyList<DebugLine> lines=baseLines.Concat(BuildInteractionHighlight(runtime.Player.FocusedTargetId)).ToArray();return new RenderFrameSnapshot(tick,camera,items,new DirectionalLight(Vector3.Normalize(new Vector3(-0.45f,-1f,-0.3f)),new Color(255,238,202),1.08f),new FogSettings(new Color(68,78,74),110f,620f),new Color(47,58,59),lines);
    }
    private IReadOnlyList<DebugLine> BuildInteractionHighlight(StableId focusedId)
    {
        InteractiveTargetDefinition? target=_interactionWorld.Targets.FirstOrDefault(value=>value.Id==focusedId);
        if(target is null)return Array.Empty<DebugLine>();
        const float pad=0.04f;StructureBox b=target.WorldBounds;
        Vector3 min=new(WorldUnits.CentimetersToMeters(b.Minimum.XCentimeters)-pad,WorldUnits.CentimetersToMeters(b.Minimum.YCentimeters)-pad,WorldUnits.CentimetersToMeters(b.Minimum.ZCentimeters)-pad);
        Vector3 max=new(WorldUnits.CentimetersToMeters(b.Maximum.XCentimeters)+pad,WorldUnits.CentimetersToMeters(b.Maximum.YCentimeters)+pad,WorldUnits.CentimetersToMeters(b.Maximum.ZCentimeters)+pad);
        Color c=new(244,224,112);Vector3 p000=new(min.X,min.Y,min.Z),p001=new(min.X,min.Y,max.Z),p010=new(min.X,max.Y,min.Z),p011=new(min.X,max.Y,max.Z);Vector3 p100=new(max.X,min.Y,min.Z),p101=new(max.X,min.Y,max.Z),p110=new(max.X,max.Y,min.Z),p111=new(max.X,max.Y,max.Z);
        return new DebugLine[]{new(p000,p001,c),new(p000,p010,c),new(p000,p100,c),new(p111,p110,c),new(p111,p101,c),new(p111,p011,c),new(p001,p011,c),new(p001,p101,c),new(p010,p011,c),new(p010,p110,c),new(p100,p101,c),new(p100,p110,c)};
    }
    private IReadOnlyList<DebugLine> BuildDebug(PlayerSimulationRuntime runtime)
    {
        var lines=new List<DebugLine>();PlayerState p=runtime.Player;Vector3 feet=new(p.FeetPosition.XMillimeters/1000f,p.FeetPosition.YMillimeters/1000f,p.FeetPosition.ZMillimeters/1000f);float radius=runtime.Motor.Settings.CapsuleRadiusMillimeters/1000f,height=(p.Stance==PlayerStance.Standing?runtime.Motor.Settings.StandingHeightMillimeters:runtime.Motor.Settings.CrouchedHeightMillimeters)/1000f;Color c=new(116,229,194);lines.Add(new DebugLine(feet+new Vector3(-radius,0,0),feet+new Vector3(radius,0,0),c));lines.Add(new DebugLine(feet+new Vector3(0,0,-radius),feet+new Vector3(0,0,radius),c));lines.Add(new DebugLine(feet,feet+Vector3.Up*height,c));if(runtime.Motor.Telemetry.LastHit is PlayerCollisionHit hit){Vector3 h=new(hit.Position.XMillimeters/1000f,hit.Position.YMillimeters/1000f,hit.Position.ZMillimeters/1000f);lines.Add(new DebugLine(h,h+new Vector3((float)hit.NormalX,(float)hit.NormalY,(float)hit.NormalZ)*2f,Color.Red));}return lines;
    }
    private GpuMesh Own(GpuMesh mesh){_ownedMeshes.Add(mesh);return mesh;}
    public void Dispose(){if(_disposed)return;_disposed=true;_terrainStreaming.Dispose();_structureStreaming.DisposeAsync().AsTask().GetAwaiter().GetResult();_terrainRendering.Dispose();_structureRendering.Dispose();_dynamicRendering.Dispose();foreach(GpuMesh mesh in _ownedMeshes)mesh.Dispose();}
}
