using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World.Structures;

public sealed record DoorState(StableId DoorId,DoorLifecycleState State,bool PlayerModified,long Revision)
{
    public DoorState Transition(DoorLifecycleState state,bool playerModified)=>this with{State=state,PlayerModified=PlayerModified||playerModified,Revision=checked(Revision+1)};
}
public sealed record StructureChangeSet(StableId StructureId,IReadOnlyList<DoorState> DoorStates,IReadOnlySet<StableId> DestroyedPrimitiveIds,IReadOnlySet<StableId> LootedReservationIds,long Revision);

public sealed class StructureRuntimeState:ICanonicalWritable
{
    private readonly SortedDictionary<StableId,DoorState> _doors=new();
    private readonly SortedSet<StableId> _destroyedPrimitives=new();
    private readonly SortedSet<StableId> _lootedReservations=new();
    public StructureRuntimeState(StructureBlueprint blueprint){ArgumentNullException.ThrowIfNull(blueprint);StructureId=blueprint.Id;foreach(DoorDefinition door in blueprint.Doors)_doors.Add(door.Id,new DoorState(door.Id,door.InitialState,false,0));}
    public StableId StructureId{get;}public long Revision{get;private set;}public IReadOnlyCollection<DoorState> Doors=>_doors.Values;
    public bool TryGetDoor(StableId doorId,out DoorState? state)=>_doors.TryGetValue(doorId,out state);
    public DoorState SetDoor(StableId doorId,DoorLifecycleState state,bool playerModified=true){if(!_doors.TryGetValue(doorId,out DoorState? current))throw new KeyNotFoundException($"Unknown door {doorId}.");DoorState changed=current.Transition(state,playerModified);_doors[doorId]=changed;Revision=checked(Revision+1);return changed;}
    public bool MarkPrimitiveDestroyed(StableId primitiveId){bool changed=_destroyedPrimitives.Add(primitiveId);if(changed)Revision=checked(Revision+1);return changed;}
    public bool MarkReservationLooted(StableId reservationId){bool changed=_lootedReservations.Add(reservationId);if(changed)Revision=checked(Revision+1);return changed;}
    public StructureChangeSet Capture()=>new(StructureId,_doors.Values.ToArray(),new HashSet<StableId>(_destroyedPrimitives),new HashSet<StableId>(_lootedReservations),Revision);
    public void WriteCanonical(CanonicalWriter writer){writer.WriteStableId(StructureId);writer.WriteInt64(Revision);writer.WriteCount(_doors.Count);foreach(DoorState door in _doors.Values){writer.WriteStableId(door.DoorId);writer.WriteInt32((int)door.State);writer.WriteBoolean(door.PlayerModified);writer.WriteInt64(door.Revision);}writer.WriteCount(_destroyedPrimitives.Count);foreach(StableId id in _destroyedPrimitives)writer.WriteStableId(id);writer.WriteCount(_lootedReservations.Count);foreach(StableId id in _lootedReservations)writer.WriteStableId(id);}
}
