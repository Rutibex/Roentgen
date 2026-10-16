using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Generation;

namespace Roentgen.World.Structures;

public sealed class StructureBlueprintCompiler
{
    public const string GeneratorId = "roentgen.world.structure.blueprint";
    public const int SchemaVersion = 2;
    private const int GridCentimeters = 50;
    private readonly RegionalTopology _topology;

    public StructureBlueprintCompiler(RegionalTopology topology) => _topology = topology ?? throw new ArgumentNullException(nameof(topology));

    public StructureBlueprint Compile(LandmarkReservation site, SettlementParcel parcel, BuildingGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(site); ArgumentNullException.ThrowIfNull(parcel); ArgumentNullException.ThrowIfNull(grammar); grammar.Validate(); parcel.Validate();
        StableId id = StableIdFactory.CreateChild(parcel.Id, "structure", grammar.GrammarId);
        DomainSeed domain = GenerationDomain.Derive(_topology.CampaignSeed, $"structure.{id}.room-graph");
        Pcg32Random random = RandomStreamFactory.Create(domain, "layout");
        StructureRect footprint = SelectFootprint(parcel.LocalBounds, grammar.Footprint, random);
        int floorCount = Math.Min(grammar.Floors.MaximumFloors, Math.Max(grammar.Floors.MinimumFloors, grammar.Purpose is BuildingPurpose.IndustrialIntake or BuildingPurpose.BoardingHouse or BuildingPurpose.Clinic ? 2 + random.NextInt32(2) : SelectFloorCount(grammar)));
        if (footprint.WidthCentimeters < 1100 || footprint.DepthCentimeters < 1000) floorCount = 1;
        int floorHeight = grammar.StructuralSystem.FloorHeightCentimeters;
        int wallThickness = grammar.StructuralSystem.WallThicknessCentimeters;
        int corridorWidth = Math.Max(grammar.Navigation.MinimumCorridorWidthCentimeters, floorCount > 1 ? 460 : grammar.Purpose is BuildingPurpose.Warehouse or BuildingPurpose.Workshop or BuildingPurpose.MarketShelter ? footprint.WidthCentimeters / 3 : 200);
        int corridorMinX = Snap(footprint.CenterXCentimeters - corridorWidth / 2);
        int corridorMaxX = corridorMinX + corridorWidth;

        var rooms = new List<RoomNode>();
        var connections = new List<RoomConnection>();
        var doors = new List<DoorDefinition>();
        var vertical = new List<VerticalLinkDefinition>();
        var cells = new List<StructureStreamingCell>();
        var decisions = new List<GenerationDecision>();
        var requirementsByFloor = AssignRequirements(grammar.RequiredRooms, floorCount);
        var corridorIds = new StableId[floorCount];
        var roomByKey = new Dictionary<string, RoomNode>(StringComparer.Ordinal);
        var roomDoorGaps = new Dictionary<(int Floor, bool Left), List<(int StartZ, int EndZ)>>();
        int doorOrdinal = 0;

        for (int floor = 0; floor < floorCount; floor++)
        {
            StableId corridorId = StableIdFactory.CreateChild(id, "room", $"corridor-{floor}"); corridorIds[floor] = corridorId;
            var corridor = new RoomNode(corridorId, $"corridor-{floor}", floor == 0 ? RoomArchetype.Corridor : RoomArchetype.Stairwell, AccessClass.Public, PrivacyClass.Open, floor, new StructureRect(corridorMinX, footprint.MinimumZCentimeters, corridorMaxX, footprint.MaximumZCentimeters), floorHeight - 20, QuestCritical: true, OccupantCapacity: 6);
            rooms.Add(corridor); roomByKey[corridor.Key] = corridor;
            IReadOnlyList<RoomRequirement> floorRequirements = requirementsByFloor[floor];
            int slots = Math.Max(1, (floorRequirements.Count + 1) / 2);
            int usableDepth = footprint.DepthCentimeters;
            int slotDepth = SnapDown(usableDepth / slots);
            for (int index = 0; index < floorRequirements.Count; index++)
            {
                RoomRequirement requirement = floorRequirements[index]; bool left = index % 2 == 0; int slot = index / 2;
                int minZ = footprint.MinimumZCentimeters + slot * slotDepth;
                int maxZ = slot == slots - 1 ? footprint.MaximumZCentimeters : minZ + slotDepth;
                StructureRect bounds = left
                    ? new StructureRect(footprint.MinimumXCentimeters, minZ, corridorMinX, maxZ)
                    : new StructureRect(corridorMaxX, minZ, footprint.MaximumXCentimeters, maxZ);
                StableId roomId = StableIdFactory.CreateChild(id, "room", requirement.Key);
                var room = new RoomNode(roomId, requirement.Key, requirement.Archetype, requirement.Access, requirement.Privacy, floor, bounds, floorHeight - 20, requirement.QuestCritical, requirement.OccupantCapacity);
                rooms.Add(room); roomByKey[requirement.Key] = room;
                StableId connectionId = StableIdFactory.CreateChild(id, "connection", $"corridor-{floor}-{requirement.Key}");
                bool alternative = requirement.Access is AccessClass.Service or AccessClass.Secure;
                connections.Add(new RoomConnection(connectionId, corridorId, roomId, ConnectionRequirement.Required, ConnectionKind.Door, requirement.Access == AccessClass.Secure ? LockPolicy.LockedWithAlternative : LockPolicy.None, alternative));
                int doorZ = Snap(bounds.CenterZCentimeters); int doorWidth = grammar.Navigation.MinimumDoorWidthCentimeters;
                StableId doorId = StableIdFactory.CreateChild(id, "door", doorOrdinal++);
                DoorLifecycleState state = requirement.Access == AccessClass.Secure ? DoorLifecycleState.Locked : DoorLifecycleState.Closed;
                LockPolicy lockPolicy = requirement.Access == AccessClass.Secure ? LockPolicy.LockedWithAlternative : LockPolicy.None;
                doors.Add(new DoorDefinition(doorId, corridorId, roomId, new StructurePoint(left ? corridorMinX : corridorMaxX, floor * floorHeight + 110, doorZ), doorWidth, 220, AlongX: false, state, lockPolicy, Exterior: false, requirement.QuestCritical));
                var key = (floor, left); if (!roomDoorGaps.TryGetValue(key, out List<(int StartZ,int EndZ)>? gaps)) { gaps = new List<(int,int)>(); roomDoorGaps.Add(key,gaps); } gaps.Add((doorZ-doorWidth/2,doorZ+doorWidth/2));
            }

            if (floor == 0)
            {
            StableId frontDoorId = StableIdFactory.CreateChild(id, "door", doorOrdinal++);
            doors.Add(new DoorDefinition(frontDoorId, corridorId, StableId.Empty, new StructurePoint(footprint.CenterXCentimeters, floor * floorHeight + 110, footprint.MinimumZCentimeters), grammar.Navigation.MinimumDoorWidthCentimeters + 20, 230, AlongX: true, floor == 0 ? DoorLifecycleState.Closed : DoorLifecycleState.Missing, LockPolicy.None, Exterior: true, QuestCritical: floor == 0));
            if (floor == 0 && grammar.Navigation.RequiresAlternateCriticalRoute)
            {
                StableId rearDoorId = StableIdFactory.CreateChild(id, "door", doorOrdinal++);
                doors.Add(new DoorDefinition(rearDoorId, corridorId, StableId.Empty, new StructurePoint(footprint.CenterXCentimeters, floor * floorHeight + 110, footprint.MaximumZCentimeters), grammar.Navigation.MinimumDoorWidthCentimeters, 220, AlongX: true, DoorLifecycleState.Jammed, LockPolicy.SkillGateWithAlternative, Exterior: true, QuestCritical: true));
            }
        }

        }

        foreach (AdjacencyRule rule in grammar.AdjacencyRules)
        {
            if (!roomByKey.TryGetValue(rule.RoomKeyA, out RoomNode? a) || !roomByKey.TryGetValue(rule.RoomKeyB, out RoomNode? b)) continue;
            if (a.Floor != b.Floor || rule.Requirement == ConnectionRequirement.Forbidden) continue;
            int left = Math.Max(a.Bounds.MinimumXCentimeters, b.Bounds.MinimumXCentimeters);
            int right = Math.Min(a.Bounds.MaximumXCentimeters, b.Bounds.MaximumXCentimeters);
            int? boundary = a.Bounds.MaximumZCentimeters == b.Bounds.MinimumZCentimeters ? a.Bounds.MaximumZCentimeters
                : b.Bounds.MaximumZCentimeters == a.Bounds.MinimumZCentimeters ? b.Bounds.MaximumZCentimeters : null;
            if (boundary is null || right - left < 220) continue;
            StableId connectionId = StableIdFactory.CreateChild(id, "semantic-connection", $"{rule.RoomKeyA}-{rule.RoomKeyB}");
            connections.Add(new RoomConnection(connectionId, a.Id, b.Id, rule.Requirement, ConnectionKind.Door, rule.LockPolicy, true));
            doors.Add(new DoorDefinition(StableIdFactory.CreateChild(id,"door",doorOrdinal++), a.Id,b.Id,
                new StructurePoint((left+right)/2,a.Floor*floorHeight+110,boundary.Value),120,220,true,
                DoorLifecycleState.Closed,rule.LockPolicy,false,false));
        }

        for (int floor = 0; floor < floorCount - 1; floor++)
        {
            int y = floor * floorHeight;
            int run = Math.Min(floorHeight * 2, footprint.DepthCentimeters - 320);
            var bounds = new StructureBox(new StructurePoint(corridorMinX + 30, y, footprint.MaximumZCentimeters - 160 - run),
                new StructurePoint(corridorMinX + 190, y + floorHeight, footprint.MaximumZCentimeters - 160));
            VerticalLinkKind kind = VerticalLinkKind.Stair;
            vertical.Add(new VerticalLinkDefinition(StableIdFactory.CreateChild(id,"vertical",floor), corridorIds[floor], corridorIds[floor+1], kind, bounds, QuestCritical:true));
            connections.Add(new RoomConnection(StableIdFactory.CreateChild(id,"vertical-connection",floor), corridorIds[floor], corridorIds[floor+1], ConnectionRequirement.Required, kind == VerticalLinkKind.Ladder ? ConnectionKind.Ladder : ConnectionKind.Stair, LockPolicy.None, false));
        }

        var primitives = ArchitecturalGeometryCompiler.Compile(id, grammar, footprint, floorCount, rooms, doors, vertical, cells);
        DamageHistory damage = BuildDamage(id, grammar, rooms, primitives, random, out IReadOnlyList<StructurePrimitive> damagedPrimitives);
        var reservations = BuildReservations(id, rooms, floorHeight);
        if (cells.Count == 0) throw new InvalidOperationException("Structure compiler did not emit streaming cells.");
        decisions.Add(new GenerationDecision("layout", grammar.Purpose is BuildingPurpose.Warehouse or BuildingPurpose.Workshop ? "industrial-gallery" : floorCount > 1 ? "multi-floor-service-spine" : "residential-spine", $"{rooms.Count} rooms over {floorCount} floor(s)"));
        decisions.Add(new GenerationDecision("geometry", "shared-grid", $"{damagedPrimitives.Count} primitives"));
        decisions.Add(new GenerationDecision("damage", damage.Events.Count == 0 ? "intact" : "historical-events", $"severity {damage.AggregateSeverityPermille}"));
        var provenance = new GenerationProvenance(id, site.Id, _topology.CampaignSeed, domain, GeneratorId, SchemaVersion, new[] { "room-graph-created-before-geometry", "stable-grid-layout", "critical-routes-preserved" }, Array.Empty<string>(), Decisions: decisions);
        var blueprint = new StructureBlueprint(id, site.Id, parcel.Id, parcel.Name, grammar, new StructurePoint(site.Center.XCentimeters, parcel.BaseElevationCentimeters, site.Center.ZCentimeters), footprint, floorCount, rooms, connections, doors, vertical, damagedPrimitives, reservations, damage, cells, provenance);
        StructureValidationReport report = new StructureValidator().Validate(blueprint);
        if (report.HasCriticalIssues)
        {
            StructureRepairResult repaired = new StructureRepairEngine().Repair(blueprint, report, maximumAttempts: 8);
            blueprint = repaired.Blueprint;
        }
        return blueprint;
    }

    public StructureBlueprint CompileStandalone(LandmarkReservation site, BuildingPurpose purpose)
    {
        var parcel = new SettlementParcel(StableIdFactory.CreateChild(site.Id,"parcel",0), site.Name, purpose, new StructureRect(-site.HalfWidthCentimeters + 300, -site.HalfDepthCentimeters + 300, site.HalfWidthCentimeters - 300, site.HalfDepthCentimeters - 300), site.TargetElevationCentimeters, 0, StableIdFactory.CreateChild(site.Id,"district",0), QuestReserved:true, MayRemainEmpty:false);
        return Compile(site, parcel, StructureGrammarCatalog.Get(purpose));
    }

    private static StructureRect SelectFootprint(StructureRect parcel, FootprintRules rules, IDeterministicRandom random)
    {
        StructureRect available = parcel.Inset(rules.SetbackCentimeters);
        int maxWidth = Math.Min(rules.MaximumWidthCentimeters, available.WidthCentimeters); int maxDepth = Math.Min(rules.MaximumDepthCentimeters, available.DepthCentimeters);
        int minWidth = Math.Min(rules.MinimumWidthCentimeters, maxWidth); int minDepth = Math.Min(rules.MinimumDepthCentimeters, maxDepth);
        int width = SnapDown(random.NextInt32(Math.Max(1,minWidth/GridCentimeters), Math.Max(minWidth/GridCentimeters+1,maxWidth/GridCentimeters+1))*GridCentimeters);
        int depth = SnapDown(random.NextInt32(Math.Max(1,minDepth/GridCentimeters), Math.Max(minDepth/GridCentimeters+1,maxDepth/GridCentimeters+1))*GridCentimeters);
        int centerX=parcel.CenterXCentimeters; int centerZ=parcel.CenterZCentimeters;
        return new StructureRect(Snap(centerX-width/2),Snap(centerZ-depth/2),Snap(centerX-width/2)+width,Snap(centerZ-depth/2)+depth);
    }

    private static int SelectFloorCount(BuildingGrammar grammar) => grammar.Purpose switch { BuildingPurpose.IndustrialIntake => Math.Min(2,grammar.Floors.MaximumFloors), BuildingPurpose.UtilityRelay => Math.Min(2,grammar.Floors.MaximumFloors), BuildingPurpose.BoardingHouse => Math.Min(2,grammar.Floors.MaximumFloors), BuildingPurpose.GuardPost => Math.Min(2,grammar.Floors.MaximumFloors), _ => grammar.Floors.MinimumFloors };

    private static IReadOnlyList<RoomRequirement>[] AssignRequirements(IReadOnlyList<RoomRequirement> requirements, int floors)
    {
        var lists=Enumerable.Range(0,floors).Select(_=>new List<RoomRequirement>()).ToArray();
        foreach(RoomRequirement room in requirements)
        {
            int floor=room.FloorPreference>0?Math.Min(room.FloorPreference,floors-1):0;
            if(room.FloorPreference<=0 && floors>1 && lists[0].Count>=4) floor=Math.Min(floors-1, lists.Select((value,index)=>(value.Count,index)).OrderBy(static pair=>pair.Count).ThenBy(static pair=>pair.index).First().index);
            lists[floor].Add(room);
        }
        return lists;
    }

    private static DamageHistory BuildDamage(StableId id, BuildingGrammar grammar, IReadOnlyList<RoomNode> rooms, IReadOnlyList<StructurePrimitive> primitives, IDeterministicRandom random, out IReadOnlyList<StructurePrimitive> output)
    {
        if(grammar.DamageSeverityPermille<=0){output=primitives;return new DamageHistory(Array.Empty<DamageEvent>(),0);}
        int count=Math.Clamp(grammar.DamageSeverityPermille/180,1,5); var events=new List<DamageEvent>(); var changed=primitives.ToArray();
        DamageEventType[] types={DamageEventType.Fire,DamageEventType.WallBreach,DamageEventType.Flooding,DamageEventType.Looting,DamageEventType.StructuralFatigue,DamageEventType.PartialRepair};
        for(int index=0;index<count;index++)
        {
            RoomNode room=rooms[random.NextInt32(rooms.Count)]; DamageEventType type=types[random.NextInt32(types.Length)]; int severity=Math.Clamp(grammar.DamageSeverityPermille+random.NextInt32(-120,121),80,900);
            events.Add(new DamageEvent(StableIdFactory.CreateChild(id,"damage",index),type,room.Id,severity,random.NextInt32(4),random.NextInt32(2,90),new[]{type.ToString().ToLowerInvariant(),"historical"}));
            var candidates=changed.Select((primitive,ordinal)=>(primitive,ordinal)).Where(static pair=>pair.primitive.Kind is StructurePrimitiveKind.Wall or StructurePrimitiveKind.Roof or StructurePrimitiveKind.Debris).Where(static pair=>!pair.primitive.RemovedByDamage).ToArray();
            if(candidates.Length>0 && severity>350)
            {
                var chosen=candidates[random.NextInt32(candidates.Length)]; changed[chosen.ordinal]=chosen.primitive with { RemovedByDamage=true, CollisionEnabled=false, DamagePermille=severity };
            }
        }
        output=changed; return new DamageHistory(events,grammar.DamageSeverityPermille);
    }

    private static IReadOnlyList<StructureReservation> BuildReservations(StableId id,IReadOnlyList<RoomNode> rooms,int floorHeightCentimeters)
    {
        var result=new List<StructureReservation>(); int ordinal=0; RoomNode[] critical=rooms.Where(static room=>room.QuestCritical&&room.Archetype is not (RoomArchetype.Corridor or RoomArchetype.Stairwell)).ToArray(); RoomNode[] fallbacks=rooms.Where(static room=>room.Access!=AccessClass.Hazardous).ToArray();
        foreach(RoomNode room in critical)
        {
            int x=room.Bounds.CenterXCentimeters; int z=room.Bounds.CenterZCentimeters; int y=room.Floor*floorHeightCentimeters;
            result.Add(new StructureReservation(StableIdFactory.CreateChild(id,"reservation",ordinal++),ReservationKind.ObjectiveVolume,room.Id,new StructureBox(new StructurePoint(x-50,y,z-50),new StructurePoint(x+50,y+180,z+50)),new[]{room.Archetype.ToString().ToLowerInvariant(),"quest-critical"},100,true,fallbacks.Where(f=>f.Id!=room.Id).Take(2).Select(static f=>f.Id).ToArray()));
        }
        RoomNode first=rooms.First(static room=>room.Archetype is not (RoomArchetype.Corridor or RoomArchetype.Stairwell)); int fy=first.Floor*floorHeightCentimeters;
        result.Add(new StructureReservation(StableIdFactory.CreateChild(id,"reservation",ordinal++),ReservationKind.LootContainer,first.Id,new StructureBox(new StructurePoint(first.Bounds.CenterXCentimeters-40,fy,first.Bounds.CenterZCentimeters-40),new StructurePoint(first.Bounds.CenterXCentimeters+40,fy+100,first.Bounds.CenterZCentimeters+40)),new[]{"loot-socket"},80,false,Array.Empty<StableId>()));
        result.Add(new StructureReservation(StableIdFactory.CreateChild(id,"reservation",ordinal),ReservationKind.OccupantAnchor,first.Id,new StructureBox(new StructurePoint(first.Bounds.CenterXCentimeters-35,fy,first.Bounds.CenterZCentimeters-35),new StructurePoint(first.Bounds.CenterXCentimeters+35,fy+190,first.Bounds.CenterZCentimeters+35)),new[]{"standing","conversation"},90,true,Array.Empty<StableId>()));
        return result;
    }

    private static StructureMaterialKind MaterialFor(StructuralSystemKind kind)=>kind switch{StructuralSystemKind.ReinforcedConcrete=>StructureMaterialKind.ReinforcedConcrete,StructuralSystemKind.IndustrialSteel=>StructureMaterialKind.IndustrialSteel,StructuralSystemKind.MasonryBlock=>StructureMaterialKind.Masonry,_=>StructureMaterialKind.CorrugatedMetal};
    private static int Snap(int value)=>value>=0?(value+GridCentimeters/2)/GridCentimeters*GridCentimeters:(value-GridCentimeters/2)/GridCentimeters*GridCentimeters;
    private static int SnapDown(int value)=>Math.Max(GridCentimeters,WorldUnits.FloorDivide(value,GridCentimeters)*GridCentimeters);
}
