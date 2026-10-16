using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World.Structures;

public sealed record ResourceAnchor(ResourceAnchorKind Kind, StableId SourceId, string Description);
public sealed record DistrictRequirement(DistrictKind Kind, int MinimumParcels, bool Required);
public sealed record ServiceRequirement(BuildingPurpose Purpose, int Count, bool Required);
public sealed record ArchitecturalPalette(StructuralSystemKind PrimarySystem, StructureMaterialKind PrimaryMaterial, StructureMaterialKind SecondaryMaterial, string StyleKey);
public sealed record HistoricalDamageProfile(int SeverityPermille, IReadOnlyList<DamageEventType> AllowedEvents);

public sealed record SettlementPhysicalProgram(
    StableId Id,
    SettlementScale Scale,
    FoundingPurpose Purpose,
    IReadOnlyList<ResourceAnchor> ResourceAnchors,
    DefensiveStrategy Defense,
    IReadOnlyList<DistrictRequirement> RequiredDistricts,
    IReadOnlyList<ServiceRequirement> RequiredServices,
    ArchitecturalPalette Palette,
    HistoricalDamageProfile HistoricalDamage)
{
    public void Validate()
    {
        if (Id.IsEmpty || ResourceAnchors.Count < 3 || RequiredDistricts.Count == 0 || RequiredServices.Count == 0) throw new InvalidOperationException("Settlement physical program is incomplete.");
        if (HistoricalDamage.SeverityPermille is < 0 or > 1000) throw new InvalidOperationException("Settlement damage severity is invalid.");
    }
}

public sealed record DistrictPlan(StableId Id, DistrictKind Kind, StructureRect Bounds, int Priority);
public sealed record StreetSegment(StableId Id, StructurePoint Start, StructurePoint End, int HalfWidthCentimeters, bool IsPrimary);
public sealed record SettlementParcel(StableId Id, string Name, BuildingPurpose Purpose, StructureRect LocalBounds, int BaseElevationCentimeters, int OrientationQuarterTurns, StableId DistrictId, bool QuestReserved, bool MayRemainEmpty)
{
    public void Validate()
    {
        if (Id.IsEmpty || DistrictId.IsEmpty || string.IsNullOrWhiteSpace(Name) || OrientationQuarterTurns is < 0 or > 3) throw new InvalidOperationException("Settlement parcel is invalid.");
        LocalBounds.Validate();
    }
}

public sealed class SettlementPlan : ICanonicalWritable
{
    public SettlementPlan(StableId id, LandmarkReservation site, SettlementPhysicalProgram program, IReadOnlyList<DistrictPlan> districts, IReadOnlyList<StreetSegment> streets, IReadOnlyList<SettlementParcel> parcels, GenerationProvenance provenance)
    {
        if (id.IsEmpty) throw new ArgumentException("Settlement plan ID cannot be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(site); ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(districts); ArgumentNullException.ThrowIfNull(streets); ArgumentNullException.ThrowIfNull(parcels); ArgumentNullException.ThrowIfNull(provenance);
        program.Validate();
        foreach (DistrictPlan district in districts) district.Bounds.Validate();
        foreach (SettlementParcel parcel in parcels) parcel.Validate();
        Id = id; Site = site; Program = program; Districts = districts.ToArray(); Streets = streets.ToArray(); Parcels = parcels.ToArray(); Provenance = provenance;
    }

    public StableId Id { get; }
    public LandmarkReservation Site { get; }
    public SettlementPhysicalProgram Program { get; }
    public IReadOnlyList<DistrictPlan> Districts { get; }
    public IReadOnlyList<StreetSegment> Streets { get; }
    public IReadOnlyList<SettlementParcel> Parcels { get; }
    public GenerationProvenance Provenance { get; }
    public StateChecksum ContentChecksum => ComputeChecksum();
    public StateChecksum ComputeChecksum() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ComputeChecksum(); }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteStableId(Site.Id); writer.WriteStableId(Program.Id); writer.WriteInt32((int)Program.Scale); writer.WriteInt32((int)Program.Purpose); writer.WriteInt32((int)Program.Defense); writer.WriteString(Program.Palette.StyleKey);
        writer.WriteCount(Districts.Count);
        foreach (DistrictPlan district in Districts.OrderBy(static value => value.Id)) { writer.WriteStableId(district.Id); writer.WriteInt32((int)district.Kind); WriteRect(writer, district.Bounds); writer.WriteInt32(district.Priority); }
        writer.WriteCount(Streets.Count);
        foreach (StreetSegment street in Streets.OrderBy(static value => value.Id)) { writer.WriteStableId(street.Id); WritePoint(writer, street.Start); WritePoint(writer, street.End); writer.WriteInt32(street.HalfWidthCentimeters); writer.WriteBoolean(street.IsPrimary); }
        writer.WriteCount(Parcels.Count);
        foreach (SettlementParcel parcel in Parcels.OrderBy(static value => value.Id)) { writer.WriteStableId(parcel.Id); writer.WriteString(parcel.Name); writer.WriteInt32((int)parcel.Purpose); WriteRect(writer, parcel.LocalBounds); writer.WriteInt32(parcel.BaseElevationCentimeters); writer.WriteInt32(parcel.OrientationQuarterTurns); writer.WriteStableId(parcel.DistrictId); writer.WriteBoolean(parcel.QuestReserved); writer.WriteBoolean(parcel.MayRemainEmpty); }
    }

    internal static void WriteRect(CanonicalWriter writer, StructureRect rect) { writer.WriteInt32(rect.MinimumXCentimeters); writer.WriteInt32(rect.MinimumZCentimeters); writer.WriteInt32(rect.MaximumXCentimeters); writer.WriteInt32(rect.MaximumZCentimeters); }
    internal static void WritePoint(CanonicalWriter writer, StructurePoint point) { writer.WriteInt32(point.XCentimeters); writer.WriteInt32(point.YCentimeters); writer.WriteInt32(point.ZCentimeters); }
}

public sealed record RoomNode(StableId Id, string Key, RoomArchetype Archetype, AccessClass Access, PrivacyClass Privacy, int Floor, StructureRect Bounds, int MinimumHeightCentimeters, bool QuestCritical, int OccupantCapacity)
{
    public void Validate() { if (Id.IsEmpty || string.IsNullOrWhiteSpace(Key) || Floor < 0 || MinimumHeightCentimeters < 220 || OccupantCapacity < 0) throw new InvalidOperationException("Room node is invalid."); Bounds.Validate(); }
}

public sealed record RoomConnection(StableId Id, StableId RoomA, StableId RoomB, ConnectionRequirement Requirement, ConnectionKind Kind, LockPolicy LockPolicy, bool IsAlternativeRoute)
{
    public void Validate() { if (Id.IsEmpty || RoomA.IsEmpty || RoomB.IsEmpty || RoomA == RoomB) throw new InvalidOperationException("Room connection is invalid."); }
}

public sealed record DoorDefinition(StableId Id, StableId FromRoomId, StableId ToRoomId, StructurePoint Center, int WidthCentimeters, int HeightCentimeters, bool AlongX, DoorLifecycleState InitialState, LockPolicy LockPolicy, bool Exterior, bool QuestCritical)
{
    public void Validate() { if (Id.IsEmpty || FromRoomId.IsEmpty || WidthCentimeters < 80 || HeightCentimeters < 190) throw new InvalidOperationException("Door definition is invalid."); }
}

public sealed record VerticalLinkDefinition(StableId Id, StableId LowerRoomId, StableId UpperRoomId, VerticalLinkKind Kind, StructureBox Bounds, bool QuestCritical)
{
    public void Validate() { if (Id.IsEmpty || LowerRoomId.IsEmpty || UpperRoomId.IsEmpty || LowerRoomId == UpperRoomId) throw new InvalidOperationException("Vertical link is invalid."); Bounds.Validate(); }
}

public sealed record StructurePrimitive(StableId Id, StructurePrimitiveKind Kind, StructureMaterialKind Material, StructureBox Bounds, StableId StreamingCellId, bool Exterior, bool CollisionEnabled, bool RemovedByDamage, int DamagePermille)
{
    public void Validate() { if (Id.IsEmpty || StreamingCellId.IsEmpty || DamagePermille is < 0 or > 1000) throw new InvalidOperationException("Structure primitive is invalid."); Bounds.Validate(); }
}

public sealed record StructureReservation(StableId Id, ReservationKind Kind, StableId RoomId, StructureBox Bounds, IReadOnlyList<string> Tags, int RequiredClearanceCentimeters, bool ProtectedFromDamage, IReadOnlyList<StableId> FallbackRoomIds)
{
    public void Validate() { if (Id.IsEmpty || RoomId.IsEmpty || RequiredClearanceCentimeters < 0) throw new InvalidOperationException("Structure reservation is invalid."); Bounds.Validate(); }
}

public sealed record DamageEvent(StableId Id, DamageEventType Type, StableId AffectedRoomId, int SeverityPermille, int DirectionQuarterTurns, int AgeYears, IReadOnlyList<string> Effects)
{
    public void Validate() { if (Id.IsEmpty || AffectedRoomId.IsEmpty || SeverityPermille is < 0 or > 1000 || DirectionQuarterTurns is < 0 or > 3 || AgeYears < 0) throw new InvalidOperationException("Damage event is invalid."); }
}
public sealed record DamageHistory(IReadOnlyList<DamageEvent> Events, int AggregateSeverityPermille);

public sealed record StructureStreamingCell(StableId Id, string Name, int Floor, StructureCellLayer Layer, StructureBox Bounds, IReadOnlyList<StableId> RoomIds, IReadOnlyList<StableId> PortalDoorIds)
{
    public void Validate() { if (Id.IsEmpty || string.IsNullOrWhiteSpace(Name) || Floor < -1) throw new InvalidOperationException("Streaming cell is invalid."); Bounds.Validate(); }
}

public sealed class StructureBlueprint : ICanonicalWritable
{
    public StructureBlueprint(StableId id, StableId parentSiteId, StableId parcelId, string name, BuildingGrammar grammar, StructurePoint worldOrigin, StructureRect footprint, int floorCount, IReadOnlyList<RoomNode> rooms, IReadOnlyList<RoomConnection> connections, IReadOnlyList<DoorDefinition> doors, IReadOnlyList<VerticalLinkDefinition> verticalLinks, IReadOnlyList<StructurePrimitive> primitives, IReadOnlyList<StructureReservation> reservations, DamageHistory damageHistory, IReadOnlyList<StructureStreamingCell> streamingCells, GenerationProvenance provenance)
    {
        if (id.IsEmpty || parentSiteId.IsEmpty || parcelId.IsEmpty || string.IsNullOrWhiteSpace(name) || floorCount < 1) throw new ArgumentException("Structure blueprint header is invalid.");
        ArgumentNullException.ThrowIfNull(grammar); grammar.Validate(); footprint.Validate();
        Rooms = rooms.ToArray(); Connections = connections.ToArray(); Doors = doors.ToArray(); VerticalLinks = verticalLinks.ToArray(); Primitives = primitives.ToArray(); Reservations = reservations.ToArray(); StreamingCells = streamingCells.ToArray();
        foreach (RoomNode item in Rooms) item.Validate(); foreach (RoomConnection item in Connections) item.Validate(); foreach (DoorDefinition item in Doors) item.Validate(); foreach (VerticalLinkDefinition item in VerticalLinks) item.Validate(); foreach (StructurePrimitive item in Primitives) item.Validate(); foreach (StructureReservation item in Reservations) item.Validate(); foreach (DamageEvent item in damageHistory.Events) item.Validate(); foreach (StructureStreamingCell item in StreamingCells) item.Validate();
        Id = id; ParentSiteId = parentSiteId; ParcelId = parcelId; Name = name; Grammar = grammar; WorldOrigin = worldOrigin; Footprint = footprint; FloorCount = floorCount; DamageHistory = damageHistory; Provenance = provenance;
    }

    public StableId Id { get; }
    public StableId ParentSiteId { get; }
    public StableId ParcelId { get; }
    public string Name { get; }
    public BuildingGrammar Grammar { get; }
    public StructurePoint WorldOrigin { get; }
    public StructureRect Footprint { get; }
    public int FloorCount { get; }
    public IReadOnlyList<RoomNode> Rooms { get; }
    public IReadOnlyList<RoomConnection> Connections { get; }
    public IReadOnlyList<DoorDefinition> Doors { get; }
    public IReadOnlyList<VerticalLinkDefinition> VerticalLinks { get; }
    public IReadOnlyList<StructurePrimitive> Primitives { get; }
    public IReadOnlyList<StructureReservation> Reservations { get; }
    public DamageHistory DamageHistory { get; }
    public IReadOnlyList<StructureStreamingCell> StreamingCells { get; }
    public GenerationProvenance Provenance { get; }
    public StateChecksum ContentChecksum => ComputeChecksum();
    public StateChecksum ComputeChecksum() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ComputeChecksum(); }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteStableId(ParentSiteId); writer.WriteStableId(ParcelId); writer.WriteString(Name); writer.WriteString(Grammar.GrammarId); writer.WriteInt32(Grammar.SchemaVersion); SettlementPlan.WritePoint(writer, WorldOrigin); SettlementPlan.WriteRect(writer, Footprint); writer.WriteInt32(FloorCount);
        writer.WriteCount(Rooms.Count); foreach (RoomNode room in Rooms.OrderBy(static value => value.Id)) { writer.WriteStableId(room.Id); writer.WriteString(room.Key); writer.WriteInt32((int)room.Archetype); writer.WriteInt32((int)room.Access); writer.WriteInt32((int)room.Privacy); writer.WriteInt32(room.Floor); SettlementPlan.WriteRect(writer, room.Bounds); writer.WriteInt32(room.MinimumHeightCentimeters); writer.WriteBoolean(room.QuestCritical); writer.WriteInt32(room.OccupantCapacity); }
        writer.WriteCount(Connections.Count); foreach (RoomConnection item in Connections.OrderBy(static value => value.Id)) { writer.WriteStableId(item.Id); writer.WriteStableId(item.RoomA); writer.WriteStableId(item.RoomB); writer.WriteInt32((int)item.Requirement); writer.WriteInt32((int)item.Kind); writer.WriteInt32((int)item.LockPolicy); writer.WriteBoolean(item.IsAlternativeRoute); }
        writer.WriteCount(Doors.Count); foreach (DoorDefinition item in Doors.OrderBy(static value => value.Id)) { writer.WriteStableId(item.Id); writer.WriteStableId(item.FromRoomId); writer.WriteStableId(item.ToRoomId); SettlementPlan.WritePoint(writer, item.Center); writer.WriteInt32(item.WidthCentimeters); writer.WriteInt32(item.HeightCentimeters); writer.WriteBoolean(item.AlongX); writer.WriteInt32((int)item.InitialState); writer.WriteInt32((int)item.LockPolicy); writer.WriteBoolean(item.Exterior); writer.WriteBoolean(item.QuestCritical); }
        writer.WriteCount(VerticalLinks.Count); foreach (VerticalLinkDefinition item in VerticalLinks.OrderBy(static value => value.Id)) { writer.WriteStableId(item.Id); writer.WriteStableId(item.LowerRoomId); writer.WriteStableId(item.UpperRoomId); writer.WriteInt32((int)item.Kind); WriteBox(writer,item.Bounds); writer.WriteBoolean(item.QuestCritical); }
        writer.WriteCount(Primitives.Count); foreach (StructurePrimitive item in Primitives.OrderBy(static value => value.Id)) { writer.WriteStableId(item.Id); writer.WriteInt32((int)item.Kind); writer.WriteInt32((int)item.Material); WriteBox(writer,item.Bounds); writer.WriteStableId(item.StreamingCellId); writer.WriteBoolean(item.Exterior); writer.WriteBoolean(item.CollisionEnabled); writer.WriteBoolean(item.RemovedByDamage); writer.WriteInt32(item.DamagePermille); }
        writer.WriteCount(Reservations.Count); foreach (StructureReservation item in Reservations.OrderBy(static value => value.Id)) { writer.WriteStableId(item.Id); writer.WriteInt32((int)item.Kind); writer.WriteStableId(item.RoomId); WriteBox(writer,item.Bounds); writer.WriteCount(item.Tags.Count); foreach(string tag in item.Tags.OrderBy(static value=>value,StringComparer.Ordinal)) writer.WriteString(tag); writer.WriteInt32(item.RequiredClearanceCentimeters); writer.WriteBoolean(item.ProtectedFromDamage); writer.WriteCount(item.FallbackRoomIds.Count); foreach(StableId fallback in item.FallbackRoomIds.OrderBy(static value=>value)) writer.WriteStableId(fallback); }
        writer.WriteCount(DamageHistory.Events.Count); foreach (DamageEvent item in DamageHistory.Events.OrderBy(static value => value.Id)) { writer.WriteStableId(item.Id); writer.WriteInt32((int)item.Type); writer.WriteStableId(item.AffectedRoomId); writer.WriteInt32(item.SeverityPermille); writer.WriteInt32(item.DirectionQuarterTurns); writer.WriteInt32(item.AgeYears); writer.WriteCount(item.Effects.Count); foreach(string effect in item.Effects.OrderBy(static value=>value,StringComparer.Ordinal)) writer.WriteString(effect); } writer.WriteInt32(DamageHistory.AggregateSeverityPermille);
        writer.WriteCount(StreamingCells.Count); foreach (StructureStreamingCell item in StreamingCells.OrderBy(static value=>value.Id)) { writer.WriteStableId(item.Id); writer.WriteString(item.Name); writer.WriteInt32(item.Floor); writer.WriteInt32((int)item.Layer); WriteBox(writer,item.Bounds); writer.WriteCount(item.RoomIds.Count); foreach(StableId room in item.RoomIds.OrderBy(static value=>value)) writer.WriteStableId(room); writer.WriteCount(item.PortalDoorIds.Count); foreach(StableId door in item.PortalDoorIds.OrderBy(static value=>value)) writer.WriteStableId(door); }
    }

    private static void WriteBox(CanonicalWriter writer, StructureBox box) { SettlementPlan.WritePoint(writer,box.Minimum); SettlementPlan.WritePoint(writer,box.Maximum); }
}
