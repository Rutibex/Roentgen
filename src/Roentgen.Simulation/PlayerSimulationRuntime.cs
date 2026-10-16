using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Physics;
using Roentgen.World.Structures;

namespace Roentgen.Simulation;

public sealed class PlayerSimulationRuntime
{
    private readonly IPlayerCollisionWorld _collision;private readonly DynamicDoorCollisionRegistry _doorCollision;private readonly KinematicPlayerMotor _motor;private readonly InteractionWorldDefinition _world;
    private readonly SortedDictionary<StableId,DoorRuntimeState> _doors=[];private readonly SortedDictionary<StableId,ContainerState> _containers=[];private readonly SortedDictionary<StableId,PortableObjectState> _objects=[];private readonly SortedDictionary<StableId,ActorInteractionTarget> _actorTargets=[];private readonly PlayerCarryLedger _carry=new();private readonly DeterministicEventBus _events=new();private string _notification=string.Empty;private int _notificationTicks;
    public PlayerSimulationRuntime(PlayerState player,IPlayerCollisionWorld collision,DynamicDoorCollisionRegistry doorCollision,InteractionWorldDefinition world,KinematicPlayerMotor? motor=null)
    {
        Player=player;_collision=collision;_doorCollision=doorCollision;_world=world;_motor=motor??new KinematicPlayerMotor();
        foreach(InteractiveDoorDefinition door in world.Doors){var state=new DoorRuntimeState(door,door.Source.InitialState,door.Source.InitialState is DoorLifecycleState.Open or DoorLifecycleState.Missing or DoorLifecycleState.Destroyed?1000:0,0);_doors.Add(door.Id,state);UpdateDoorCollider(state);}
        foreach(InteractiveTargetDefinition target in world.Targets.Where(static value=>value.Kind==InteractiveTargetKind.Container))
        {
            StableId a=StableIdFactory.CreateChild(target.Id,"portable","a");StableId b=StableIdFactory.CreateChild(target.Id,"portable","b");var container=new ContainerState(target.Id,target.DisplayName,new[]{a,b});_containers.Add(target.Id,container);
            _objects.Add(a,new PortableObjectState(a,"medical-supply","Medical Supply Case",target.Id,1,false,0));_objects.Add(b,new PortableObjectState(b,"scrap-bundle","Scrap Bundle",target.Id,1,false,0));
        }
        foreach(InteractiveTargetDefinition target in world.Targets.Where(static value=>value.Kind==InteractiveTargetKind.Pickup))_objects[target.Id]=new PortableObjectState(target.Id,"diagnostic-component",target.DisplayName,StableId.Empty,1,true,0);
    }
    public StableId LastActivatedTargetId { get; private set; }
    public PlayerState Player{get;}public ulong Tick{get;private set;}public ulong InterfaceTick{get;private set;}public KinematicPlayerMotor Motor=>_motor;public IReadOnlyCollection<DoorRuntimeState> Doors=>_doors.Values;public IReadOnlyCollection<PortableObjectState> Objects=>_objects.Values;public IReadOnlyCollection<ActorInteractionTarget> ActorTargets=>_actorTargets.Values;

    /// <summary>Refreshes moving character interaction proxies without making them part of the save-game state.</summary>
    public void SyncActorTargets(IEnumerable<ActorInteractionTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);_actorTargets.Clear();
        foreach(ActorInteractionTarget target in targets.OrderBy(static value=>value.Id))
            if(!target.Id.IsEmpty&&target.Id!=Player.Id&&target.HeightMillimeters>0&&target.RadiusMillimeters>0)_actorTargets[target.Id]=target;
    }

    public bool TryGetDoor(StableId id,out DoorRuntimeState? door)=>_doors.TryGetValue(id,out door);
    public bool TryPrepareDoorForNavigation(StableId id,bool canUnlock,bool canForce,out bool ready,out string message)
    {
        ready=false;
        if(!_doors.TryGetValue(id,out DoorRuntimeState? door)||door is null){message="navigation door is unavailable";return false;}
        switch(door.State)
        {
            case DoorLifecycleState.Open:
                ready=door.OpenProgressPermille>850;message=ready?"door passage clear":"door opening";return true;
            case DoorLifecycleState.Missing:
            case DoorLifecycleState.Destroyed:
                ready=true;message="door passage clear";return true;
            case DoorLifecycleState.Closed:
                SetDoorState(id,DoorLifecycleState.Open);message="door opening";return true;
            case DoorLifecycleState.Locked when canUnlock:
                SetDoorState(id,DoorLifecycleState.Open);message="door unlocked and opening";return true;
            case DoorLifecycleState.Locked:
                message="door is locked";return false;
            case DoorLifecycleState.Jammed when canForce:
            case DoorLifecycleState.Barricaded when canForce:
                SetDoorState(id,DoorLifecycleState.Destroyed);ready=true;message="door forced open";return true;
            case DoorLifecycleState.Jammed:
                message="door is jammed";return false;
            case DoorLifecycleState.Barricaded:
                message="door is barricaded";return false;
            default:
                message="door cannot be traversed";return false;
        }
    }
    public PlayerPresentationSnapshot Step(PlayerIntentFrame intent)
    {
        LastActivatedTargetId=StableId.Empty;
        InterfaceTick++;
        if(_notificationTicks>0)_notificationTicks--;else _notification=string.Empty;
        if(Player.ControlMode==PlayerControlMode.Container)
        {
            if(intent.Buttons.HasFlag(PlayerIntentButtons.CancelPressed)){CloseContainer();return Snapshot(null);}
            if(intent.Buttons.HasFlag(PlayerIntentButtons.TransferAllPressed)||intent.Buttons.HasFlag(PlayerIntentButtons.InteractPressed))TransferAll();
            return Snapshot(null);
        }
        if(intent.Buttons.HasFlag(PlayerIntentButtons.PausePressed)){Player.ControlMode=Player.ControlMode==PlayerControlMode.Paused?PlayerControlMode.Exploration:PlayerControlMode.Paused;Player.Revision++;}
        if(Player.ControlMode==PlayerControlMode.Paused)return Snapshot(null);
        Tick++;
        _motor.Tick(Player,intent,_collision,60);AnimateDoors();InteractionTargetDescriptor? focused=SelectTarget();Player.FocusedTargetId=focused?.TargetId??StableId.Empty;
        if(intent.Buttons.HasFlag(PlayerIntentButtons.InteractPressed)&&focused is not null)Execute(focused);
        return Snapshot(focused);
    }

    private InteractionTargetDescriptor? SelectTarget()
    {
        FixedVector3 eye=EyePosition();FixedVector3 forward=ForwardVector(1000000);InteractionTargetDescriptor? best=null;
        foreach(InteractiveTargetDefinition target in _world.Targets)
        {
            if(target.Kind==InteractiveTargetKind.Pickup&&_objects.TryGetValue(target.Id,out PortableObjectState? obj)&&!obj.WorldVisible)continue;
            int padding=target.Kind==InteractiveTargetKind.Pickup?160:target.Kind==InteractiveTargetKind.Door?60:100;
            if(!TryRayAabb(eye,forward,(long)target.WorldBounds.Minimum.XCentimeters*10-padding,(long)target.WorldBounds.Minimum.YCentimeters*10-padding,(long)target.WorldBounds.Minimum.ZCentimeters*10-padding,(long)target.WorldBounds.Maximum.XCentimeters*10+padding,(long)target.WorldBounds.Maximum.YCentimeters*10+padding,(long)target.WorldBounds.Maximum.ZCentimeters*10+padding,3000,out int distance))continue;
            bool occluded=IsOccluded(eye,forward,distance,target.Id);
            var descriptor=new InteractionTargetDescriptor(target.Id,target.DisplayName,new[]{BuildOption(target)},distance,ScoreTarget(target.Id,distance),occluded);
            if(!occluded&&IsBetter(descriptor,best))best=descriptor;
        }
        foreach(ActorInteractionTarget actor in _actorTargets.Values)
        {
            int padding=80;long r=actor.RadiusMillimeters+padding;
            if(!TryRayAabb(eye,forward,actor.FeetPosition.XMillimeters-r,actor.FeetPosition.YMillimeters-padding,actor.FeetPosition.ZMillimeters-r,actor.FeetPosition.XMillimeters+r,actor.FeetPosition.YMillimeters+actor.HeightMillimeters+padding,actor.FeetPosition.ZMillimeters+r,3500,out int distance))continue;
            bool occluded=IsOccluded(eye,forward,distance,StableId.Empty);
            InteractionOption option=new(InteractionVerb.Activate,actor.PromptKey,actor.Enabled,actor.Enabled?InteractionFailureReason.None:InteractionFailureReason.InvalidState,0,110);
            var descriptor=new InteractionTargetDescriptor(actor.Id,actor.DisplayName,new[]{option},distance,ScoreTarget(actor.Id,distance),occluded);
            if(!occluded&&IsBetter(descriptor,best))best=descriptor;
        }
        return best;
    }

    private bool IsOccluded(FixedVector3 eye,FixedVector3 forward,int targetDistance,StableId targetId)
    {
        int castDistance=Math.Max(1,targetDistance-70);
        if(!_collision.Raycast(eye,forward,castDistance,CollisionQueryMask.All,out PlayerCollisionHit hit))return false;
        return targetId.IsEmpty||hit.EntityId!=targetId;
    }
    private int ScoreTarget(StableId id,int distance)=>1_000_000-distance+(id==Player.FocusedTargetId?90:0);
    private static bool IsBetter(InteractionTargetDescriptor candidate,InteractionTargetDescriptor? best)=>best is null||candidate.Score>best.Score||(candidate.Score==best.Score&&candidate.TargetId.CompareTo(best.TargetId)<0);

    private static bool TryRayAabb(FixedVector3 origin,FixedVector3 direction,long minX,long minY,long minZ,long maxX,long maxY,long maxZ,int maximumDistance,out int distance)
    {
        double length=Math.Sqrt((double)direction.XMillimeters*direction.XMillimeters+(double)direction.YMillimeters*direction.YMillimeters+(double)direction.ZMillimeters*direction.ZMillimeters);
        if(length<=0.0001||maximumDistance<=0){distance=0;return false;}
        double dx=direction.XMillimeters/length,dy=direction.YMillimeters/length,dz=direction.ZMillimeters/length;
        double near=double.NegativeInfinity,far=double.PositiveInfinity;
        if(!Slab(origin.XMillimeters,dx,minX,maxX,ref near,ref far)||!Slab(origin.YMillimeters,dy,minY,maxY,ref near,ref far)||!Slab(origin.ZMillimeters,dz,minZ,maxZ,ref near,ref far)||far<0){distance=0;return false;}
        double hit=near>=0?near:far;if(hit<0||hit>maximumDistance){distance=0;return false;}distance=(int)Math.Round(hit);return true;
    }
    private static bool Slab(long origin,double direction,long minimum,long maximum,ref double near,ref double far)
    {
        if(Math.Abs(direction)<1e-12)return origin>=minimum&&origin<=maximum;
        double a=(minimum-origin)/direction,b=(maximum-origin)/direction;if(a>b)(a,b)=(b,a);near=Math.Max(near,a);far=Math.Min(far,b);return near<=far;
    }

    private InteractionOption BuildOption(InteractiveTargetDefinition target)
    {
        if(target.Kind==InteractiveTargetKind.Door&&_doors.TryGetValue(target.Id,out DoorRuntimeState? door))return door.State switch{DoorLifecycleState.Open=>new InteractionOption(InteractionVerb.Close,"CLOSE",true,InteractionFailureReason.None,0,100),DoorLifecycleState.Locked=>new InteractionOption(InteractionVerb.Open,"LOCKED",false,InteractionFailureReason.Locked,0,100),DoorLifecycleState.Jammed=>new InteractionOption(InteractionVerb.Open,"JAMMED",false,InteractionFailureReason.Jammed,0,100),DoorLifecycleState.Barricaded=>new InteractionOption(InteractionVerb.Open,"BARRICADED",false,InteractionFailureReason.Barricaded,0,100),DoorLifecycleState.Missing or DoorLifecycleState.Destroyed=>new InteractionOption(InteractionVerb.Inspect,"INSPECT",true,InteractionFailureReason.None,0,10),_=>new InteractionOption(InteractionVerb.Open,"OPEN",true,InteractionFailureReason.None,0,100)};
        if(target.Kind==InteractiveTargetKind.Pickup)return new InteractionOption(InteractionVerb.Take,"TAKE",true,InteractionFailureReason.None,0,100);
        if(target.Kind==InteractiveTargetKind.Container)return new InteractionOption(InteractionVerb.Search,"SEARCH",true,_containers[target.Id].ObjectIds.Count==0?InteractionFailureReason.Empty:InteractionFailureReason.None,0,80);
        if(target.Kind==InteractiveTargetKind.Ladder)return new InteractionOption(InteractionVerb.Climb,"CLIMB",true,InteractionFailureReason.None,0,90);
        return new InteractionOption(target.Verbs[0],target.Verbs[0].ToString().ToUpperInvariant(),true,InteractionFailureReason.None,0,10);
    }
    private void Execute(InteractionTargetDescriptor descriptor)
    {
        InteractionOption option=descriptor.Options[0];if(!option.Enabled){Notify(option.FailureReason.ToString().ToUpperInvariant());Publish(new InteractionRejectedEvent(Player.Id,descriptor.TargetId,option.Verb,option.FailureReason));return;}
        if(_actorTargets.TryGetValue(descriptor.TargetId,out ActorInteractionTarget? actor))
        {
            LastActivatedTargetId=actor.Id;Notify($"{actor.PromptKey}: {actor.DisplayName.ToUpperInvariant()}");Publish(new InteractionSucceededEvent(Player.Id,actor.Id,InteractionVerb.Activate));return;
        }
        InteractiveTargetDefinition target=_world.Targets.First(value=>value.Id==descriptor.TargetId);
        switch(target.Kind)
        {
            case InteractiveTargetKind.Door:ToggleDoor(target.Id);break;
            case InteractiveTargetKind.Container:OpenContainer(target.Id);break;
            case InteractiveTargetKind.Pickup:TakePickup(target.Id);break;
            case InteractiveTargetKind.Ladder:BeginLadder(target);break;
            case InteractiveTargetKind.Terminal:LastActivatedTargetId=target.Id;Notify("CONSOLE ACTIVATED");break;
            default:Notify($"{option.Verb.ToString().ToUpperInvariant()}: {target.DisplayName}");break;
        }
        Publish(new InteractionSucceededEvent(Player.Id,target.Id,option.Verb));
    }
    private void ToggleDoor(StableId id){DoorRuntimeState door=_doors[id];DoorLifecycleState next=door.State==DoorLifecycleState.Open?DoorLifecycleState.Closed:DoorLifecycleState.Open;SetDoorState(id,next);Notify(next==DoorLifecycleState.Open?"DOOR OPENING":"DOOR CLOSING");}
    private void SetDoorState(StableId id,DoorLifecycleState state){DoorRuntimeState door=_doors[id];door=door with{State=state,Revision=door.Revision+1};_doors[id]=door;UpdateDoorCollider(door);}
    private void AnimateDoors(){foreach(StableId id in _doors.Keys.ToArray()){DoorRuntimeState state=_doors[id];int target=state.State is DoorLifecycleState.Open or DoorLifecycleState.Missing or DoorLifecycleState.Destroyed?1000:0;int next=state.OpenProgressPermille<target?Math.Min(target,state.OpenProgressPermille+80):Math.Max(target,state.OpenProgressPermille-80);if(next!=state.OpenProgressPermille){state=state with{OpenProgressPermille=next,Revision=state.Revision+1};_doors[id]=state;UpdateDoorCollider(state);}}}
    private void UpdateDoorCollider(DoorRuntimeState state){StructureBox bounds=state.Definition.ClosedBounds;if(state.OpenProgressPermille>850||!state.BlocksMovement){_doorCollision.Remove(state.Definition.Id);return;}_doorCollision.Set(new DynamicDoorCollider(state.Definition.Id,bounds,state.OpenProgressPermille,state.BlocksMovement,state.BlocksSight));}
    private void OpenContainer(StableId id){ContainerState container=_containers[id];container.SetOpen(true);Player.OpenContainerId=id;Player.ControlMode=PlayerControlMode.Container;Notify($"OPENED {container.DisplayName.ToUpperInvariant()}");}
    private void CloseContainer()=>CloseOpenContainer();
    private void TransferAll()=>TransferAllOpenContainerItems();

    public void CloseOpenContainer()
    {
        if(!Player.OpenContainerId.IsEmpty&&_containers.TryGetValue(Player.OpenContainerId,out ContainerState? container))container.SetOpen(false);
        Player.OpenContainerId=StableId.Empty;Player.ControlMode=PlayerControlMode.Exploration;
    }

    public bool TransferOpenContainerItem(StableId objectId)
    {
        if(Player.OpenContainerId.IsEmpty||!_containers.TryGetValue(Player.OpenContainerId,out ContainerState? container)||container is null||!container.Remove(objectId)||!_objects.TryGetValue(objectId,out PortableObjectState? item))return false;
        _carry.Add(objectId);_objects[objectId]=item.Transfer(Player.Id,false);Notify($"TOOK {item.DisplayName.ToUpperInvariant()}");return true;
    }

    public int TransferAllOpenContainerItems()
    {
        if(Player.OpenContainerId.IsEmpty||!_containers.TryGetValue(Player.OpenContainerId,out ContainerState? container)||container is null)return 0;
        int count=0;foreach(StableId id in container.ObjectIds.ToArray()){if(TransferOpenContainerItem(id))count++;}
        Notify(count==0?"CONTAINER EMPTY":$"TOOK {count} OBJECTS");return count;
    }

    public PlayerPresentationSnapshot BuildPresentationSnapshot()=>Snapshot(null);
    private void TakePickup(StableId id){PortableObjectState obj=_objects[id];if(!obj.WorldVisible)return;_objects[id]=obj.Transfer(Player.Id,false);_carry.Add(id);Notify($"TAKEN: {obj.DisplayName.ToUpperInvariant()}");}
    private void BeginLadder(InteractiveTargetDefinition target){long lower=(long)target.WorldBounds.Minimum.YCentimeters*10;long upper=(long)target.WorldBounds.Maximum.YCentimeters*10;long destination=Math.Abs(Player.FeetPosition.YMillimeters-lower)<Math.Abs(Player.FeetPosition.YMillimeters-upper)?upper:lower;_motor.BeginLadder(Player,new FixedVector3((long)target.WorldPosition.XCentimeters*10,destination,(long)target.WorldPosition.ZCentimeters*10));Notify("CLIMBING");}
    private PlayerPresentationSnapshot Snapshot(InteractionTargetDescriptor? focused){StateChecksum checksum=ComputeChecksum();ContainerState? open=Player.OpenContainerId.IsEmpty?null:_containers[Player.OpenContainerId];IReadOnlyList<PortableObjectState> containerItems=open is null?[]:open.ObjectIds.Select(id=>_objects[id]).OrderBy(static value=>value.Id).ToArray();return new PlayerPresentationSnapshot(Tick,InterfaceTick,Clone(Player),focused,_carry.ObjectIds.Select(id=>_objects[id]).OrderBy(static value=>value.Id).ToArray(),open,containerItems,_notification,checksum);}
    private StateChecksum ComputeChecksum(){var writer=new CanonicalWriter();Player.WriteCanonical(writer);_carry.WriteCanonical(writer);writer.WriteCount(_doors.Count);foreach(DoorRuntimeState door in _doors.Values){writer.WriteStableId(door.Definition.Id);writer.WriteInt32((int)door.State);writer.WriteInt32(door.OpenProgressPermille);writer.WriteInt64(door.Revision);}writer.WriteCount(_containers.Count);foreach(ContainerState c in _containers.Values)c.WriteCanonical(writer);writer.WriteCount(_objects.Count);foreach(PortableObjectState o in _objects.Values){writer.WriteStableId(o.Id);writer.WriteString(o.PrototypeKey);writer.WriteString(o.DisplayName);writer.WriteStableId(o.OwnerId);writer.WriteInt32(o.Quantity);writer.WriteBoolean(o.WorldVisible);writer.WriteInt64(o.Revision);}return writer.ComputeChecksum();}
    public byte[] CapturePhase20State()
    {
        var writer=new CanonicalWriter();writer.WriteString("roentgen.player-world.phase20");writer.WriteInt32(20);writer.WriteUInt64(Tick);writer.WriteUInt64(InterfaceTick);
        writer.WriteStableId(Player.Id);WriteVector(writer,Player.FeetPosition);WriteVector(writer,Player.Velocity);writer.WriteInt32(Player.BodyYawUnits);writer.WriteInt32(Player.ViewPitchUnits);writer.WriteInt32((int)Player.Stance);writer.WriteInt32((int)Player.Locomotion);writer.WriteBoolean(Player.Grounded);writer.WriteStableId(Player.GroundEntityId);writer.WriteUInt64(Player.Revision);
        writer.WriteCount(_carry.ObjectIds.Count);foreach(StableId id in _carry.ObjectIds.OrderBy(static value=>value))writer.WriteStableId(id);
        writer.WriteCount(_doors.Count);foreach(DoorRuntimeState door in _doors.Values){writer.WriteStableId(door.Definition.Id);writer.WriteInt32((int)door.State);writer.WriteInt32(door.OpenProgressPermille);writer.WriteInt64(door.Revision);}
        writer.WriteCount(_containers.Count);foreach(ContainerState container in _containers.Values){writer.WriteStableId(container.Id);writer.WriteBoolean(container.Locked);writer.WriteCount(container.ObjectIds.Count);foreach(StableId id in container.ObjectIds.OrderBy(static value=>value))writer.WriteStableId(id);}
        writer.WriteCount(_objects.Count);foreach(PortableObjectState value in _objects.Values){writer.WriteStableId(value.Id);writer.WriteString(value.PrototypeKey);writer.WriteString(value.DisplayName);writer.WriteStableId(value.OwnerId);writer.WriteInt32(value.Quantity);writer.WriteBoolean(value.WorldVisible);writer.WriteInt64(value.Revision);}
        return writer.ToArray();
    }
    public void RestorePhase20State(ReadOnlyMemory<byte> payload)
    {
        var reader=new CanonicalReader(payload);if(!string.Equals(reader.ReadRequiredString(),"roentgen.player-world.phase20",StringComparison.Ordinal))throw new InvalidDataException("Player-world save marker is invalid.");int schema=reader.ReadInt32();if(schema!=20)throw new NotSupportedException($"Player-world schema {schema} is not supported.");
        Tick=reader.ReadUInt64();InterfaceTick=reader.ReadUInt64();StableId playerId=reader.ReadStableId();if(playerId!=Player.Id)throw new InvalidDataException("Player-world identity does not match the active campaign.");
        Player.FeetPosition=ReadVector(reader);Player.Velocity=ReadVector(reader);Player.BodyYawUnits=reader.ReadInt32();Player.ViewPitchUnits=reader.ReadInt32();Player.Stance=(PlayerStance)reader.ReadInt32();Player.Locomotion=(LocomotionState)reader.ReadInt32();Player.Grounded=reader.ReadBoolean();Player.GroundEntityId=reader.ReadStableId();Player.Revision=reader.ReadUInt64();Player.FocusedTargetId=StableId.Empty;Player.OpenContainerId=StableId.Empty;Player.ControlMode=PlayerControlMode.Exploration;Player.Traversal=TraversalState.None;Player.TraversalProgressPermille=0;
        _carry.Clear();int carryCount=reader.ReadCount();for(int index=0;index<carryCount;index++)_carry.Add(reader.ReadStableId());
        int doorCount=reader.ReadCount();if(doorCount!=_doors.Count)throw new InvalidDataException("Player-world door count does not match generated content.");for(int index=0;index<doorCount;index++){StableId id=reader.ReadStableId();if(!_doors.TryGetValue(id,out DoorRuntimeState? existing)||existing is null)throw new InvalidDataException($"Player-world door '{id}' is unavailable.");DoorLifecycleState state=(DoorLifecycleState)reader.ReadInt32();int progress=reader.ReadInt32();long revision=reader.ReadInt64();var restored=existing with{State=state,OpenProgressPermille=progress,Revision=revision};_doors[id]=restored;UpdateDoorCollider(restored);}
        int containerCount=reader.ReadCount();if(containerCount!=_containers.Count)throw new InvalidDataException("Player-world container count does not match generated content.");for(int index=0;index<containerCount;index++){StableId id=reader.ReadStableId();if(!_containers.TryGetValue(id,out ContainerState? container)||container is null)throw new InvalidDataException($"Player-world container '{id}' is unavailable.");container.Locked=reader.ReadBoolean();int itemCount=reader.ReadCount();var ids=new StableId[itemCount];for(int itemIndex=0;itemIndex<itemCount;itemIndex++)ids[itemIndex]=reader.ReadStableId();container.ReplaceContents(ids);container.SetOpen(false);}
        _objects.Clear();int objectCount=reader.ReadCount();for(int index=0;index<objectCount;index++){StableId id=reader.ReadStableId();_objects.Add(id,new PortableObjectState(id,reader.ReadRequiredString(),reader.ReadRequiredString(),reader.ReadStableId(),reader.ReadInt32(),reader.ReadBoolean(),reader.ReadInt64()));}
        reader.EnsureComplete();_notification=string.Empty;_notificationTicks=0;
    }
    private static void WriteVector(CanonicalWriter writer,FixedVector3 value){writer.WriteInt64(value.XMillimeters);writer.WriteInt64(value.YMillimeters);writer.WriteInt64(value.ZMillimeters);}
    private static FixedVector3 ReadVector(CanonicalReader reader)=>new(reader.ReadInt64(),reader.ReadInt64(),reader.ReadInt64());

    private FixedVector3 EyePosition(){int h=Player.Stance==PlayerStance.Standing?_motor.Settings.StandingEyeHeightMillimeters:_motor.Settings.CrouchedEyeHeightMillimeters;return Player.FeetPosition+new FixedVector3(0,h,0);}
    private FixedVector3 ForwardVector(long length){double yaw=Player.BodyYawUnits/10000.0*Math.PI/180.0,pitch=Player.ViewPitchUnits/10000.0*Math.PI/180.0;double cp=Math.Cos(pitch);return new FixedVector3((long)Math.Round(Math.Sin(yaw)*cp*length),(long)Math.Round(Math.Sin(pitch)*length),(long)Math.Round(-Math.Cos(yaw)*cp*length));}
    private void Publish(ISimulationEvent @event){_events.Publish(Player.Id,Tick,@event);_events.Flush(new SimulationTickContext(Tick,new CampaignSeed(0x524F454E5447454EUL),_events));}
    private void Notify(string text){_notification=text;_notificationTicks=150;}
    private static PlayerState Clone(PlayerState source)=>new(source.Id,source.FeetPosition){Velocity=source.Velocity,BodyYawUnits=source.BodyYawUnits,ViewPitchUnits=source.ViewPitchUnits,Stance=source.Stance,Locomotion=source.Locomotion,Grounded=source.Grounded,GroundEntityId=source.GroundEntityId,FocusedTargetId=source.FocusedTargetId,OpenContainerId=source.OpenContainerId,ControlMode=source.ControlMode,Traversal=source.Traversal,TraversalProgressPermille=source.TraversalProgressPermille,TraversalStart=source.TraversalStart,TraversalEnd=source.TraversalEnd,Revision=source.Revision};
}
