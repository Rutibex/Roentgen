using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.World.Structures;

namespace Roentgen.Entities;

public sealed record DoorRuntimeState(InteractiveDoorDefinition Definition,DoorLifecycleState State,int OpenProgressPermille,long Revision)
{
    public bool BlocksMovement=>State is not (DoorLifecycleState.Missing or DoorLifecycleState.Destroyed) && (State is DoorLifecycleState.Locked or DoorLifecycleState.Jammed or DoorLifecycleState.Barricaded || OpenProgressPermille<850);
    public bool BlocksSight=>BlocksMovement;
}

public sealed record PlayerPresentationSnapshot(ulong Tick,ulong InterfaceTick,PlayerState Player,InteractionTargetDescriptor? FocusedTarget,IReadOnlyList<PortableObjectState> CarryItems,ContainerState? OpenContainer,IReadOnlyList<PortableObjectState> ContainerItems,string Notification,StateChecksum Checksum);
public sealed record ActorInteractionTarget(
    StableId Id,
    string DisplayName,
    FixedVector3 FeetPosition,
    int HeightMillimeters,
    int RadiusMillimeters,
    string PromptKey = "TALK",
    bool Enabled = true);

