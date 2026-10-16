using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Entities;

public readonly record struct FixedVector3(long XMillimeters,long YMillimeters,long ZMillimeters)
{
    public static FixedVector3 Zero => new(0,0,0);
    public static FixedVector3 operator +(FixedVector3 a,FixedVector3 b)=>new(checked(a.XMillimeters+b.XMillimeters),checked(a.YMillimeters+b.YMillimeters),checked(a.ZMillimeters+b.ZMillimeters));
    public static FixedVector3 operator -(FixedVector3 a,FixedVector3 b)=>new(checked(a.XMillimeters-b.XMillimeters),checked(a.YMillimeters-b.YMillimeters),checked(a.ZMillimeters-b.ZMillimeters));
}

public sealed class PlayerState : ICanonicalWritable
{
    public PlayerState(StableId id,FixedVector3 feetPosition)
    {
        if(id.IsEmpty)throw new ArgumentException("Player ID cannot be empty.",nameof(id));
        Id=id;FeetPosition=feetPosition;Stance=PlayerStance.Standing;Locomotion=LocomotionState.Idle;
    }
    public StableId Id{get;}
    public FixedVector3 FeetPosition{get;set;}
    public FixedVector3 Velocity{get;set;}
    public int BodyYawUnits{get;set;}
    public int ViewPitchUnits{get;set;}
    public PlayerStance Stance{get;set;}
    public LocomotionState Locomotion{get;set;}
    public bool Grounded{get;set;}
    public StableId GroundEntityId{get;set;}
    public StableId FocusedTargetId{get;set;}
    public StableId OpenContainerId{get;set;}
    public PlayerControlMode ControlMode{get;set;}
    public TraversalState Traversal{get;set;}
    public int TraversalProgressPermille{get;set;}
    public FixedVector3 TraversalStart{get;set;}
    public FixedVector3 TraversalEnd{get;set;}
    public ulong Revision{get;set;}
    public StateChecksum ComputeChecksum(){var writer=new CanonicalWriter();WriteCanonical(writer);return writer.ComputeChecksum();}
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id);Write(writer,FeetPosition);Write(writer,Velocity);writer.WriteInt32(BodyYawUnits);writer.WriteInt32(ViewPitchUnits);
        writer.WriteInt32((int)Stance);writer.WriteInt32((int)Locomotion);writer.WriteBoolean(Grounded);writer.WriteStableId(GroundEntityId);
        writer.WriteStableId(FocusedTargetId);writer.WriteStableId(OpenContainerId);writer.WriteInt32((int)ControlMode);writer.WriteInt32((int)Traversal);
        writer.WriteInt32(TraversalProgressPermille);Write(writer,TraversalStart);Write(writer,TraversalEnd);writer.WriteUInt64(Revision);
    }
    private static void Write(CanonicalWriter writer,FixedVector3 value){writer.WriteInt64(value.XMillimeters);writer.WriteInt64(value.YMillimeters);writer.WriteInt64(value.ZMillimeters);}
}

public sealed record PortableObjectState(StableId Id,string PrototypeKey,string DisplayName,StableId OwnerId,int Quantity,bool WorldVisible,long Revision)
{
    public PortableObjectState Transfer(StableId ownerId,bool worldVisible)=>this with{OwnerId=ownerId,WorldVisible=worldVisible,Revision=checked(Revision+1)};
}

public sealed class ContainerState : ICanonicalWritable
{
    private readonly SortedSet<StableId> _objectIds=[];
    public ContainerState(StableId id,string displayName,IEnumerable<StableId>? objectIds=null){if(id.IsEmpty)throw new ArgumentException("Container ID cannot be empty.");Id=id;DisplayName=displayName;foreach(StableId value in objectIds??[])_objectIds.Add(value);}
    public StableId Id{get;}public string DisplayName{get;}public bool IsOpen{get;private set;}public bool Locked{get;set;}public long Revision{get;private set;}public IReadOnlyCollection<StableId> ObjectIds=>_objectIds;
    public void SetOpen(bool value){if(IsOpen==value)return;IsOpen=value;Revision=checked(Revision+1);}
    public bool Add(StableId id){bool changed=_objectIds.Add(id);if(changed)Revision=checked(Revision+1);return changed;}
    public bool Remove(StableId id){bool changed=_objectIds.Remove(id);if(changed)Revision=checked(Revision+1);return changed;}
    public void ReplaceContents(IEnumerable<StableId> ids){ArgumentNullException.ThrowIfNull(ids);_objectIds.Clear();foreach(StableId id in ids)_objectIds.Add(id);Revision=checked(Revision+1);}
    public void WriteCanonical(CanonicalWriter writer){writer.WriteStableId(Id);writer.WriteString(DisplayName);writer.WriteBoolean(IsOpen);writer.WriteBoolean(Locked);writer.WriteInt64(Revision);writer.WriteCount(_objectIds.Count);foreach(StableId id in _objectIds)writer.WriteStableId(id);}
}

public sealed class PlayerCarryLedger : ICanonicalWritable
{
    private readonly SortedSet<StableId> _objects=[];
    public IReadOnlyCollection<StableId> ObjectIds=>_objects;
    public long Revision{get;private set;}
    public bool Add(StableId id){bool changed=_objects.Add(id);if(changed)Revision=checked(Revision+1);return changed;}
    public bool Remove(StableId id){bool changed=_objects.Remove(id);if(changed)Revision=checked(Revision+1);return changed;}
    public void Clear(){if(_objects.Count==0)return;_objects.Clear();Revision=checked(Revision+1);}
    public void WriteCanonical(CanonicalWriter writer){writer.WriteInt64(Revision);writer.WriteCount(_objects.Count);foreach(StableId id in _objects)writer.WriteStableId(id);}
}
