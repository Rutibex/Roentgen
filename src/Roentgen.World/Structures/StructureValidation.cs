using Roentgen.Contracts;

namespace Roentgen.World.Structures;

public sealed record StructureValidationIssue(string Code, StructureValidationSeverity Severity, StableId SubjectId, string Message, IReadOnlyList<StructureRepairKind> CandidateRepairs)
{
    public bool IsCritical => Severity == StructureValidationSeverity.Critical;
}

public sealed record StructureValidationMetrics(int ReachableRooms, int TotalRooms, int CriticalReservations, int ExteriorDoors, int CollisionPrimitives, int RemovedPrimitives);

public sealed class StructureValidationReport
{
    public StructureValidationReport(IReadOnlyList<StructureValidationIssue> issues, StructureValidationMetrics metrics)
    {
        Issues = issues.ToArray(); Metrics = metrics;
    }
    public IReadOnlyList<StructureValidationIssue> Issues { get; }
    public StructureValidationMetrics Metrics { get; }
    public bool IsValid => Issues.All(static issue => issue.Severity < StructureValidationSeverity.Error);
    public bool HasCriticalIssues => Issues.Any(static issue => issue.IsCritical);
}

public sealed class StructureValidator
{
    public StructureValidationReport Validate(StructureBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        var issues = new List<StructureValidationIssue>();
        ValidateIds(blueprint, issues);
        ValidateRooms(blueprint, issues);
        HashSet<StableId> reachable = ValidateReachability(blueprint, issues);
        ValidateDoorsAndLinks(blueprint, issues);
        ValidateReservations(blueprint, reachable, issues);
        ValidateGeometry(blueprint, issues);
        ValidateStreaming(blueprint, issues);
        var metrics = new StructureValidationMetrics(reachable.Count, blueprint.Rooms.Count, blueprint.Reservations.Count(static reservation => reservation.ProtectedFromDamage), blueprint.Doors.Count(static door => door.Exterior), blueprint.Primitives.Count(static primitive => primitive.CollisionEnabled && !primitive.RemovedByDamage), blueprint.Primitives.Count(static primitive => primitive.RemovedByDamage));
        return new StructureValidationReport(issues, metrics);
    }

    private static void ValidateIds(StructureBlueprint blueprint, ICollection<StructureValidationIssue> issues)
    {
        var ids = new HashSet<StableId>();
        IEnumerable<(StableId Id,string Kind)> values = blueprint.Rooms.Select(static item => (item.Id,"room"))
            .Concat(blueprint.Connections.Select(static item => (item.Id,"connection")))
            .Concat(blueprint.Doors.Select(static item => (item.Id,"door")))
            .Concat(blueprint.VerticalLinks.Select(static item => (item.Id,"vertical-link")))
            .Concat(blueprint.Primitives.Select(static item => (item.Id,"primitive")))
            .Concat(blueprint.Reservations.Select(static item => (item.Id,"reservation")))
            .Concat(blueprint.StreamingCells.Select(static item => (item.Id,"streaming-cell")));
        foreach ((StableId id,string kind) in values)
        {
            if (id.IsEmpty || !ids.Add(id)) issues.Add(new StructureValidationIssue("STR-ID-001",StructureValidationSeverity.Critical,id,$"Duplicate or empty {kind} ID.",new[]{StructureRepairKind.RegenerateCluster}));
        }
    }

    private static void ValidateRooms(StructureBlueprint blueprint, ICollection<StructureValidationIssue> issues)
    {
        foreach (RoomNode room in blueprint.Rooms)
        {
            if (room.Floor >= blueprint.FloorCount || room.Bounds.MinimumXCentimeters < blueprint.Footprint.MinimumXCentimeters || room.Bounds.MaximumXCentimeters > blueprint.Footprint.MaximumXCentimeters || room.Bounds.MinimumZCentimeters < blueprint.Footprint.MinimumZCentimeters || room.Bounds.MaximumZCentimeters > blueprint.Footprint.MaximumZCentimeters)
                issues.Add(new StructureValidationIssue("STR-ROOM-001",StructureValidationSeverity.Critical,room.Id,$"Room '{room.Key}' is outside its floor or footprint.",new[]{StructureRepairKind.ResizeRoom,StructureRepairKind.RegenerateFloor}));
            if (room.Bounds.WidthCentimeters < 100 || room.Bounds.DepthCentimeters < 100)
                issues.Add(new StructureValidationIssue("STR-ROOM-002",StructureValidationSeverity.Error,room.Id,$"Room '{room.Key}' is too narrow.",new[]{StructureRepairKind.ResizeRoom}));
        }
        RoomNode[] ordinary = blueprint.Rooms.Where(static room => room.Archetype is not RoomArchetype.Corridor and not RoomArchetype.Stairwell).ToArray();
        for (int a=0;a<ordinary.Length;a++) for(int b=a+1;b<ordinary.Length;b++) if(ordinary[a].Floor==ordinary[b].Floor && ordinary[a].Bounds.Intersects(ordinary[b].Bounds))
            issues.Add(new StructureValidationIssue("STR-ROOM-003",StructureValidationSeverity.Critical,ordinary[b].Id,$"Rooms '{ordinary[a].Key}' and '{ordinary[b].Key}' overlap.",new[]{StructureRepairKind.ResizeRoom,StructureRepairKind.RegenerateFloor}));
    }

    private static HashSet<StableId> ValidateReachability(StructureBlueprint blueprint, ICollection<StructureValidationIssue> issues)
    {
        StableId[] starts=blueprint.Doors.Where(static door=>door.Exterior && door.InitialState is not DoorLifecycleState.Destroyed and not DoorLifecycleState.Missing).Select(static door=>door.FromRoomId).Distinct().ToArray();
        if(starts.Length==0)
        {
            issues.Add(new StructureValidationIssue("STR-NAV-001",StructureValidationSeverity.Critical,blueprint.Id,"Structure has no usable exterior entrance.",new[]{StructureRepairKind.AddPassage,StructureRepairKind.MoveDoorway}));
            return new HashSet<StableId>();
        }
        var adjacency=blueprint.Rooms.ToDictionary(static room=>room.Id,_=>new List<StableId>());
        foreach(DoorDefinition door in blueprint.Doors.Where(static door=>!door.Exterior && door.InitialState is not DoorLifecycleState.Destroyed and not DoorLifecycleState.Missing)) if(adjacency.ContainsKey(door.FromRoomId)&&adjacency.ContainsKey(door.ToRoomId)){adjacency[door.FromRoomId].Add(door.ToRoomId);adjacency[door.ToRoomId].Add(door.FromRoomId);}
        foreach(VerticalLinkDefinition link in blueprint.VerticalLinks){if(adjacency.ContainsKey(link.LowerRoomId)&&adjacency.ContainsKey(link.UpperRoomId)){adjacency[link.LowerRoomId].Add(link.UpperRoomId);adjacency[link.UpperRoomId].Add(link.LowerRoomId);}}
        var reached=new HashSet<StableId>(starts); var queue=new Queue<StableId>(starts);
        while(queue.Count>0){StableId current=queue.Dequeue();foreach(StableId next in adjacency[current].OrderBy(static id=>id)) if(reached.Add(next)) queue.Enqueue(next);}
        foreach(RoomNode room in blueprint.Rooms.Where(room=>!reached.Contains(room.Id))) issues.Add(new StructureValidationIssue("STR-NAV-002",room.QuestCritical?StructureValidationSeverity.Critical:StructureValidationSeverity.Error,room.Id,$"Room '{room.Key}' is unreachable from an exterior entrance.",new[]{StructureRepairKind.ReconnectRoom,StructureRepairKind.AddPassage}));
        if(blueprint.Grammar.Navigation.RequiresAlternateCriticalRoute && blueprint.Doors.Count(static door=>door.Exterior && door.InitialState is not DoorLifecycleState.Destroyed and not DoorLifecycleState.Missing)<2)
            issues.Add(new StructureValidationIssue("STR-NAV-003",StructureValidationSeverity.Critical,blueprint.Id,"Grammar requires an alternate exterior route.",new[]{StructureRepairKind.AddPassage}));
        return reached;
    }

    private static void ValidateDoorsAndLinks(StructureBlueprint blueprint,ICollection<StructureValidationIssue> issues)
    {
        var roomIds=blueprint.Rooms.Select(static room=>room.Id).ToHashSet();
        foreach(DoorDefinition door in blueprint.Doors)
        {
            if(!roomIds.Contains(door.FromRoomId)||(!door.Exterior&&!roomIds.Contains(door.ToRoomId))) issues.Add(new StructureValidationIssue("STR-DOOR-001",StructureValidationSeverity.Critical,door.Id,"Door references an unknown room.",new[]{StructureRepairKind.MoveDoorway}));
            if(door.QuestCritical && door.InitialState is DoorLifecycleState.Destroyed or DoorLifecycleState.Missing) issues.Add(new StructureValidationIssue("STR-DOOR-002",StructureValidationSeverity.Critical,door.Id,"Quest-critical door is absent.",new[]{StructureRepairKind.MoveDoorway,StructureRepairKind.AddPassage}));
        }
        foreach(VerticalLinkDefinition link in blueprint.VerticalLinks) if(!roomIds.Contains(link.LowerRoomId)||!roomIds.Contains(link.UpperRoomId)) issues.Add(new StructureValidationIssue("STR-VERT-001",StructureValidationSeverity.Critical,link.Id,"Vertical link references an unknown room.",new[]{StructureRepairKind.AddVerticalLink}));
    }

    private static void ValidateReservations(StructureBlueprint blueprint,HashSet<StableId> reachable,ICollection<StructureValidationIssue> issues)
    {
        var rooms=blueprint.Rooms.ToDictionary(static room=>room.Id);
        foreach(StructureReservation reservation in blueprint.Reservations)
        {
            if(!rooms.TryGetValue(reservation.RoomId,out RoomNode? room)){issues.Add(new StructureValidationIssue("STR-RES-001",StructureValidationSeverity.Critical,reservation.Id,"Reservation references an unknown room.",new[]{StructureRepairKind.RelocateReservation}));continue;}
            if(!reachable.Contains(room.Id)) issues.Add(new StructureValidationIssue("STR-RES-002",reservation.ProtectedFromDamage?StructureValidationSeverity.Critical:StructureValidationSeverity.Error,reservation.Id,"Reservation is in an unreachable room.",new[]{StructureRepairKind.RelocateReservation,StructureRepairKind.ReconnectRoom}));
            if(reservation.Bounds.Minimum.XCentimeters<room.Bounds.MinimumXCentimeters||reservation.Bounds.Maximum.XCentimeters>room.Bounds.MaximumXCentimeters||reservation.Bounds.Minimum.ZCentimeters<room.Bounds.MinimumZCentimeters||reservation.Bounds.Maximum.ZCentimeters>room.Bounds.MaximumZCentimeters)
                issues.Add(new StructureValidationIssue("STR-RES-003",StructureValidationSeverity.Error,reservation.Id,"Reservation exceeds its room bounds.",new[]{StructureRepairKind.RelocateReservation}));
        }
    }

    private static void ValidateGeometry(StructureBlueprint blueprint,ICollection<StructureValidationIssue> issues)
    {
        var cells=blueprint.StreamingCells.Select(static cell=>cell.Id).ToHashSet();
        foreach(StructurePrimitive primitive in blueprint.Primitives)
        {
            if(!cells.Contains(primitive.StreamingCellId)) issues.Add(new StructureValidationIssue("STR-GEO-001",StructureValidationSeverity.Critical,primitive.Id,"Primitive references an unknown streaming cell.",new[]{StructureRepairKind.RegenerateCluster}));
            if(primitive.Kind is StructurePrimitiveKind.Floor or StructurePrimitiveKind.Stair && primitive.RemovedByDamage) issues.Add(new StructureValidationIssue("STR-GEO-002",StructureValidationSeverity.Critical,primitive.Id,"Required traversal primitive was removed by damage.",new[]{StructureRepairKind.ReduceDamage}));
        }
    }

    private static void ValidateStreaming(StructureBlueprint blueprint,ICollection<StructureValidationIssue> issues)
    {
        if(!blueprint.StreamingCells.Any(static cell=>cell.Layer==StructureCellLayer.ExteriorShell)) issues.Add(new StructureValidationIssue("STR-STREAM-001",StructureValidationSeverity.Critical,blueprint.Id,"Structure has no exterior shell cell.",new[]{StructureRepairKind.RegenerateCluster}));
        foreach(int floor in Enumerable.Range(0,blueprint.FloorCount)) if(!blueprint.StreamingCells.Any(cell=>cell.Layer==StructureCellLayer.CriticalInterior&&cell.Floor==floor)) issues.Add(new StructureValidationIssue("STR-STREAM-002",StructureValidationSeverity.Critical,blueprint.Id,$"Floor {floor} has no critical interior cell.",new[]{StructureRepairKind.RegenerateFloor}));
    }
}
