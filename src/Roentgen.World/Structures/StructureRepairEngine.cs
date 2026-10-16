using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World.Structures;

public sealed record StructureRepairAction(int Attempt, StructureRepairKind Kind, StableId SubjectId, string Summary, StateChecksum BeforeChecksum, StateChecksum AfterChecksum);
public sealed record StructureRepairResult(StructureBlueprint Blueprint, StructureValidationReport Report, IReadOnlyList<StructureRepairAction> Actions, bool UsedSafeFallback)
{
    public bool Succeeded => !Report.HasCriticalIssues;
}

public sealed class StructureRepairEngine
{
    public StructureRepairResult Repair(StructureBlueprint original, StructureValidationReport initialReport, int maximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(initialReport);
        if(maximumAttempts<1||maximumAttempts>64) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        StructureBlueprint current=original; StructureValidationReport report=initialReport; var actions=new List<StructureRepairAction>();
        for(int attempt=1;attempt<=maximumAttempts && report.HasCriticalIssues;attempt++)
        {
            StructureValidationIssue issue=report.Issues.Where(static item=>item.IsCritical).OrderBy(static item=>item.Code,StringComparer.Ordinal).ThenBy(static item=>item.SubjectId).First();
            StateChecksum before=current.ContentChecksum; StructureBlueprint next=ApplyLowestCostRepair(current,issue,attempt); StateChecksum after=next.ContentChecksum;
            if(after==before) break;
            StructureRepairKind kind=issue.CandidateRepairs.Count>0?issue.CandidateRepairs[0]:StructureRepairKind.RegenerateCluster;
            actions.Add(new StructureRepairAction(attempt,kind,issue.SubjectId,$"{issue.Code}: {issue.Message}",before,after)); current=next; report=new StructureValidator().Validate(current);
        }
        bool fallback=false;
        if(report.HasCriticalIssues)
        {
            current=ApplySafeFallback(current,actions.Count+1); report=new StructureValidator().Validate(current); fallback=true;
            actions.Add(new StructureRepairAction(actions.Count+1,StructureRepairKind.SafeFallback,current.Id,"Applied conservative intact fallback routes.",original.ContentChecksum,current.ContentChecksum));
        }
        return new StructureRepairResult(current,report,actions,fallback);
    }

    private static StructureBlueprint ApplyLowestCostRepair(StructureBlueprint blueprint,StructureValidationIssue issue,int attempt)
    {
        return issue.Code switch
        {
            "STR-NAV-001" or "STR-NAV-003" => AddExteriorDoor(blueprint,attempt),
            "STR-NAV-002" => ReconnectRoom(blueprint,issue.SubjectId,attempt),
            "STR-DOOR-002" => RestoreDoor(blueprint,issue.SubjectId),
            "STR-GEO-002" => RestorePrimitive(blueprint,issue.SubjectId),
            "STR-RES-001" or "STR-RES-002" or "STR-RES-003" => RelocateReservation(blueprint,issue.SubjectId),
            _ => blueprint
        };
    }

    private static StructureBlueprint AddExteriorDoor(StructureBlueprint blueprint,int attempt)
    {
        RoomNode corridor=blueprint.Rooms.Where(static room=>room.Floor == 0 && (room.Archetype is RoomArchetype.Corridor or RoomArchetype.Stairwell)).OrderBy(static room=>room.Id).First();
        int z=attempt%2==0?blueprint.Footprint.MinimumZCentimeters:blueprint.Footprint.MaximumZCentimeters;
        var doors=blueprint.Doors.Concat(new[]{new DoorDefinition(StableIdFactory.CreateChild(blueprint.Id,"repair-door",attempt),corridor.Id,StableId.Empty,new StructurePoint(blueprint.Footprint.CenterXCentimeters,110,z),blueprint.Grammar.Navigation.MinimumDoorWidthCentimeters,220,AlongX:true,DoorLifecycleState.Closed,LockPolicy.None,Exterior:true,QuestCritical:true)}).ToArray();
        return Rebuild(blueprint,doors:doors,repair:$"add exterior passage attempt {attempt}");
    }

    private static StructureBlueprint ReconnectRoom(StructureBlueprint blueprint,StableId roomId,int attempt)
    {
        RoomNode room=blueprint.Rooms.First(value=>value.Id==roomId); RoomNode corridor=blueprint.Rooms.Where(value=>value.Floor == room.Floor && (value.Archetype is RoomArchetype.Corridor or RoomArchetype.Stairwell)).OrderBy(static value=>value.Id).First(); bool left=room.Bounds.MaximumXCentimeters<=corridor.Bounds.MinimumXCentimeters; int x=left?corridor.Bounds.MinimumXCentimeters:corridor.Bounds.MaximumXCentimeters; int y=room.Floor*blueprint.Grammar.StructuralSystem.FloorHeightCentimeters+110;
        StableId doorId=StableIdFactory.CreateChild(blueprint.Id,"repair-room-door",$"{roomId}-{attempt}");
        var doors=blueprint.Doors.Concat(new[]{new DoorDefinition(doorId,corridor.Id,room.Id,new StructurePoint(x,y,room.Bounds.CenterZCentimeters),blueprint.Grammar.Navigation.MinimumDoorWidthCentimeters,220,AlongX:false,DoorLifecycleState.Closed,LockPolicy.None,Exterior:false,room.QuestCritical)}).ToArray();
        var connections=blueprint.Connections.Concat(new[]{new RoomConnection(StableIdFactory.CreateChild(blueprint.Id,"repair-connection",$"{roomId}-{attempt}"),corridor.Id,room.Id,ConnectionRequirement.Required,ConnectionKind.Door,LockPolicy.None,false)}).ToArray();
        return Rebuild(blueprint,doors:doors,connections:connections,repair:$"reconnected room {room.Key}");
    }

    private static StructureBlueprint RestoreDoor(StructureBlueprint blueprint,StableId doorId)
    {
        var doors=blueprint.Doors.Select(door=>door.Id==doorId?door with{InitialState=DoorLifecycleState.Closed}:door).ToArray();
        return Rebuild(blueprint,doors:doors,repair:$"restored door {doorId}");
    }

    private static StructureBlueprint RestorePrimitive(StructureBlueprint blueprint,StableId primitiveId)
    {
        var primitives=blueprint.Primitives.Select(primitive=>primitive.Id==primitiveId?primitive with{RemovedByDamage=false,CollisionEnabled=true,DamagePermille=Math.Min(primitive.DamagePermille,250)}:primitive).ToArray();
        return Rebuild(blueprint,primitives:primitives,repair:$"restored traversal primitive {primitiveId}");
    }

    private static StructureBlueprint RelocateReservation(StructureBlueprint blueprint,StableId reservationId)
    {
        RoomNode fallback=blueprint.Rooms.Where(static room=>room.Archetype is not RoomArchetype.Corridor and not RoomArchetype.Stairwell&&room.Access!=AccessClass.Hazardous).OrderByDescending(static room=>room.QuestCritical).ThenBy(static room=>room.Id).First(); int y=fallback.Floor*blueprint.Grammar.StructuralSystem.FloorHeightCentimeters;
        var reservations=blueprint.Reservations.Select(reservation=>reservation.Id==reservationId?reservation with{RoomId=fallback.Id,Bounds=new StructureBox(new StructurePoint(fallback.Bounds.CenterXCentimeters-40,y,fallback.Bounds.CenterZCentimeters-40),new StructurePoint(fallback.Bounds.CenterXCentimeters+40,y+180,fallback.Bounds.CenterZCentimeters+40))}:reservation).ToArray();
        return Rebuild(blueprint,reservations:reservations,repair:$"relocated reservation {reservationId}");
    }

    private static StructureBlueprint ApplySafeFallback(StructureBlueprint blueprint,int attempt)
    {
        var primitives=blueprint.Primitives.Select(static primitive=>primitive.Kind is StructurePrimitiveKind.Floor or StructurePrimitiveKind.Stair?primitive with{RemovedByDamage=false,CollisionEnabled=true}:primitive).ToArray();
        var doors=blueprint.Doors.Select(static door=>door.QuestCritical?door with{InitialState=DoorLifecycleState.Closed,LockPolicy=LockPolicy.None}:door).ToList();
        int exterior=doors.Count(static door=>door.Exterior&&door.InitialState is not DoorLifecycleState.Destroyed and not DoorLifecycleState.Missing);
        if(exterior==0){RoomNode corridor=blueprint.Rooms.First(static room=>room.Floor == 0 && (room.Archetype is RoomArchetype.Corridor or RoomArchetype.Stairwell));doors.Add(new DoorDefinition(StableIdFactory.CreateChild(blueprint.Id,"fallback-door",attempt),corridor.Id,StableId.Empty,new StructurePoint(blueprint.Footprint.CenterXCentimeters,110,blueprint.Footprint.MinimumZCentimeters),120,220,true,DoorLifecycleState.Closed,LockPolicy.None,true,true));}
        return Rebuild(blueprint,doors:doors,primitives:primitives,repair:"safe fallback: restored critical traversal");
    }

    private static StructureBlueprint Rebuild(StructureBlueprint blueprint,IReadOnlyList<RoomConnection>? connections=null,IReadOnlyList<DoorDefinition>? doors=null,IReadOnlyList<StructurePrimitive>? primitives=null,IReadOnlyList<StructureReservation>? reservations=null,string? repair=null)
    {
        string[] history=blueprint.Provenance.RepairHistory.Concat(string.IsNullOrWhiteSpace(repair)?Array.Empty<string>():new[]{repair}).ToArray();
        GenerationProvenance provenance=blueprint.Provenance with{RepairHistory=history};
        return new StructureBlueprint(blueprint.Id,blueprint.ParentSiteId,blueprint.ParcelId,blueprint.Name,blueprint.Grammar,blueprint.WorldOrigin,blueprint.Footprint,blueprint.FloorCount,blueprint.Rooms,connections??blueprint.Connections,doors??blueprint.Doors,blueprint.VerticalLinks,primitives??blueprint.Primitives,reservations??blueprint.Reservations,blueprint.DamageHistory,blueprint.StreamingCells,provenance);
    }
}
