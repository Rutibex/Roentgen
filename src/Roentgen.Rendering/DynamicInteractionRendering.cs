using Microsoft.Xna.Framework;
using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Rendering;

public sealed class DynamicInteractionRendering:IDisposable
{
    private readonly GpuMesh _doorMesh;private readonly GpuMesh _containerMesh;private readonly GpuMesh _pickupMesh;private bool _disposed;
    private static readonly MaterialDefinition DoorMaterial=new("interactive-door",new Color(103,87,68),0.82f);
    private static readonly MaterialDefinition ContainerMaterial=new("interactive-container",new Color(104,116,99),0.85f);
    private static readonly MaterialDefinition PickupMaterial=new("interactive-pickup",new Color(163,193,161),0.6f);
    private static readonly MaterialDefinition HighlightMaterial=new("interactive-highlight",new Color(238,225,124),0.38f,0f,0.72f);
    public DynamicInteractionRendering(RendererFoundation renderer)
    {
        _doorMesh=renderer.UploadNow(ProceduralMeshFactory.CreateBox("phase8-door",new Vector3(1f,1f,0.12f),new Color(103,87,68)));
        _containerMesh=renderer.UploadNow(ProceduralMeshFactory.CreateBox("phase8-container",new Vector3(0.75f,0.48f,0.48f),new Color(104,116,99)));
        _pickupMesh=renderer.UploadNow(ProceduralMeshFactory.CreateBox("phase8-pickup",new Vector3(0.28f,0.18f,0.28f),new Color(163,193,161)));
    }
    public IReadOnlyList<RenderItem> Build(InteractionWorldDefinition world,IEnumerable<DoorRuntimeState> doors,IEnumerable<PortableObjectState> objects,StableId highlightedTargetId)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);var items=new List<RenderItem>();Dictionary<StableId,DoorRuntimeState> states=doors.ToDictionary(static value=>value.Definition.Id);
        foreach(InteractiveDoorDefinition door in world.Doors)
        {
            DoorRuntimeState state=states[door.Id];if(state.State is DoorLifecycleState.Missing or DoorLifecycleState.Destroyed)continue;float width=WorldUnits.CentimetersToMeters(door.Source.WidthCentimeters),height=WorldUnits.CentimetersToMeters(door.Source.HeightCentimeters);float progress=state.OpenProgressPermille/1000f;float sign=door.HingePositive?1f:-1f;float angle=MathHelper.ToRadians(door.OpenAngleDegrees)*progress*sign;Vector3 center=new(WorldUnits.CentimetersToMeters(door.WorldCenter.XCentimeters),WorldUnits.CentimetersToMeters(door.WorldCenter.YCentimeters),WorldUnits.CentimetersToMeters(door.WorldCenter.ZCentimeters));Matrix orientation=door.Source.AlongX?Matrix.Identity:Matrix.CreateRotationY(MathHelper.PiOver2);Matrix worldMatrix=Matrix.CreateScale(width,height,1f)*Matrix.CreateTranslation(new Vector3(width*sign*0.5f,0,0))*Matrix.CreateRotationY(angle)*Matrix.CreateTranslation(new Vector3(-width*sign*0.5f,0,0))*orientation*Matrix.CreateTranslation(center);items.Add(new RenderItem($"door-{door.Id}",_doorMesh,door.Id==highlightedTargetId?HighlightMaterial:DoorMaterial,worldMatrix));
        }
        Dictionary<StableId,InteractiveTargetDefinition> targets=world.Targets.ToDictionary(static value=>value.Id);
        foreach(InteractiveTargetDefinition target in world.Targets.Where(static value=>value.Kind==InteractiveTargetKind.Container))
        {
            Vector3 p=ToMeters(target.WorldPosition);items.Add(new RenderItem($"container-{target.Id}",_containerMesh,target.Id==highlightedTargetId?HighlightMaterial:ContainerMaterial,Matrix.CreateTranslation(p)));
        }
        foreach(PortableObjectState obj in objects.Where(static value=>value.WorldVisible))if(targets.TryGetValue(obj.Id,out InteractiveTargetDefinition? target)){Vector3 p=ToMeters(target.WorldPosition);items.Add(new RenderItem($"pickup-{obj.Id}",_pickupMesh,obj.Id==highlightedTargetId?HighlightMaterial:PickupMaterial,Matrix.CreateTranslation(p)));}
        return items;
    }
    private static Vector3 ToMeters(StructurePoint p)=>new(WorldUnits.CentimetersToMeters(p.XCentimeters),WorldUnits.CentimetersToMeters(p.YCentimeters),WorldUnits.CentimetersToMeters(p.ZCentimeters));
    public void Dispose(){if(_disposed)return;_disposed=true;_doorMesh.Dispose();_containerMesh.Dispose();_pickupMesh.Dispose();}
}
