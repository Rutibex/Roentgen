using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World.Structures;

public enum InteractiveTargetKind { Door = 0, Container = 1, Pickup = 2, Ladder = 3, Terminal = 4 }
public enum DoorMotionKind { Swinging = 0, Sliding = 1, Rolling = 2, HingedHatch = 3 }

public sealed record InteractiveTargetDefinition(
    StableId Id,
    StableId StructureId,
    InteractiveTargetKind Kind,
    string DisplayName,
    StructurePoint WorldPosition,
    StructureBox WorldBounds,
    StableId SourceId,
    IReadOnlyList<InteractionVerb> Verbs,
    bool QuestCritical);

public sealed record InteractiveDoorDefinition(
    StableId Id,
    StableId StructureId,
    DoorDefinition Source,
    StructurePoint WorldCenter,
    StructureBox ClosedBounds,
    DoorMotionKind Motion,
    int OpenAngleDegrees,
    bool HingePositive);

public sealed class InteractionWorldDefinition
{
    public InteractionWorldDefinition(IReadOnlyList<InteractiveTargetDefinition> targets,IReadOnlyList<InteractiveDoorDefinition> doors)
    {
        Targets=targets.OrderBy(static value=>value.Id).ToArray();Doors=doors.OrderBy(static value=>value.Id).ToArray();
    }
    public IReadOnlyList<InteractiveTargetDefinition> Targets{get;}
    public IReadOnlyList<InteractiveDoorDefinition> Doors{get;}
}

public sealed class InteractionWorldCompiler
{
    public InteractionWorldDefinition Compile(IEnumerable<StructureBlueprint> blueprints)
    {
        var targets=new List<InteractiveTargetDefinition>();var doors=new List<InteractiveDoorDefinition>();
        foreach(StructureBlueprint structure in blueprints.OrderBy(static value=>value.Id))
        {
            foreach(DoorDefinition door in structure.Doors.OrderBy(static value=>value.Id))
            {
                StructurePoint center=Translate(door.Center,structure.WorldOrigin);
                int halfWidth=door.WidthCentimeters/2;int halfThickness=10;
                StructureBox bounds=door.AlongX
                    ?new StructureBox(new StructurePoint(center.XCentimeters-halfWidth,center.YCentimeters-door.HeightCentimeters/2,center.ZCentimeters-halfThickness),new StructurePoint(center.XCentimeters+halfWidth,center.YCentimeters+door.HeightCentimeters/2,center.ZCentimeters+halfThickness))
                    :new StructureBox(new StructurePoint(center.XCentimeters-halfThickness,center.YCentimeters-door.HeightCentimeters/2,center.ZCentimeters-halfWidth),new StructurePoint(center.XCentimeters+halfThickness,center.YCentimeters+door.HeightCentimeters/2,center.ZCentimeters+halfWidth));
                var definition=new InteractiveDoorDefinition(door.Id,structure.Id,door,center,bounds,DoorMotionKind.Swinging,90,HingePositive:(door.Id.Low&1UL)==0UL);
                doors.Add(definition);
                targets.Add(new InteractiveTargetDefinition(door.Id,structure.Id,InteractiveTargetKind.Door,$"{structure.Name} Door",center,bounds,door.Id,new[]{InteractionVerb.Open,InteractionVerb.Close,InteractionVerb.Inspect},door.QuestCritical));
            }
            foreach(VerticalLinkDefinition link in structure.VerticalLinks.Where(static link=>link.Kind==VerticalLinkKind.Ladder).OrderBy(static value=>value.Id))
            {
                StructureBox bounds=Translate(link.Bounds,structure.WorldOrigin);targets.Add(new InteractiveTargetDefinition(link.Id,structure.Id,InteractiveTargetKind.Ladder,$"{structure.Name} Ladder",bounds.Center,bounds,link.Id,new[]{InteractionVerb.Climb},link.QuestCritical));
            }
            foreach(StructureReservation reservation in structure.Reservations.OrderBy(static value=>value.Id))
            {
                StructureBox bounds=Translate(reservation.Bounds,structure.WorldOrigin);StructurePoint center=bounds.Center;
                if(reservation.Kind==ReservationKind.LootContainer)
                    targets.Add(new InteractiveTargetDefinition(reservation.Id,structure.Id,InteractiveTargetKind.Container,NameFor(reservation,"Supply Container"),center,bounds,reservation.Id,new[]{InteractionVerb.Search,InteractionVerb.Inspect},false));
                else if(reservation.Kind is ReservationKind.CriticalItem or ReservationKind.Evidence)
                    targets.Add(new InteractiveTargetDefinition(reservation.Id,structure.Id,InteractiveTargetKind.Pickup,NameFor(reservation,"Diagnostic Component"),center,bounds,reservation.Id,new[]{InteractionVerb.Take,InteractionVerb.Inspect},reservation.Kind==ReservationKind.CriticalItem));
                else if(reservation.Kind==ReservationKind.Terminal)
                    targets.Add(new InteractiveTargetDefinition(reservation.Id,structure.Id,InteractiveTargetKind.Terminal,"Diagnostic Terminal",center,bounds,reservation.Id,new[]{InteractionVerb.Activate,InteractionVerb.Inspect},false));
            }
        }
        return new InteractionWorldDefinition(targets,doors);
    }
    private static string NameFor(StructureReservation reservation,string fallback){string? tag=reservation.Tags.FirstOrDefault(value=>value.StartsWith("name:",StringComparison.OrdinalIgnoreCase));return tag is null?fallback:tag[5..].Replace('-',' ');}
    private static StructurePoint Translate(StructurePoint point,StructurePoint origin)=>new(checked(point.XCentimeters+origin.XCentimeters),checked(point.YCentimeters+origin.YCentimeters),checked(point.ZCentimeters+origin.ZCentimeters));
    private static StructureBox Translate(StructureBox box,StructurePoint origin)=>new(Translate(box.Minimum,origin),Translate(box.Maximum,origin));
}
