using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.World.Structures;

namespace Roentgen.Navigation;

public enum TraversalCapability { Ordinary = 0, CanUnlock = 1, CanForce = 2, CanClimb = 3, HazardProtected = 4 }
public sealed record TraversalProfile(string Id,IReadOnlySet<TraversalCapability> Capabilities)
{
    public static TraversalProfile Ordinary { get; } = new("ordinary",new HashSet<TraversalCapability>{TraversalCapability.Ordinary,TraversalCapability.CanClimb});
    public static TraversalProfile Technical { get; } = new("technical",new HashSet<TraversalCapability>{TraversalCapability.Ordinary,TraversalCapability.CanClimb,TraversalCapability.CanUnlock});
    public static TraversalProfile Breacher { get; } = new("breacher",new HashSet<TraversalCapability>{TraversalCapability.Ordinary,TraversalCapability.CanClimb,TraversalCapability.CanForce});
}

public sealed record NavigationCell(StableId Id,StableId RoomId,int Floor,StructureRect Bounds,AccessClass Access,bool QuestCritical);
public sealed record NavigationPortal(StableId Id,StableId DoorId,StableId FromCellId,StableId ToCellId,bool Exterior,DoorLifecycleState DoorState,LockPolicy LockPolicy,int WidthCentimeters);
public sealed record NavigationLink(StableId Id,StableId FromCellId,StableId ToCellId,VerticalLinkKind Kind,StructureBox Bounds,bool QuestCritical);
public sealed record CoverPoint(StableId Id,StableId CellId,StructurePoint Position,int NormalQuarterTurns,bool HighCover);
public sealed record RestrictedArea(StableId Id,StableId CellId,StructureBox Bounds,string Reason);
public sealed record FlankingLink(StableId Id,StableId OriginCellId,StableId DestinationCellId,int RouteDiversityScore);

public sealed class InteriorNavigationData:ICanonicalWritable
{
    public InteriorNavigationData(StableId structureId,IReadOnlyList<NavigationCell> cells,IReadOnlyList<NavigationPortal> portals,IReadOnlyList<NavigationLink> links,IReadOnlyList<CoverPoint> cover,IReadOnlyList<RestrictedArea> restrictedAreas,IReadOnlyList<FlankingLink> flankingLinks)
    {StructureId=structureId;Cells=cells.ToArray();Portals=portals.ToArray();Links=links.ToArray();Cover=cover.ToArray();RestrictedAreas=restrictedAreas.ToArray();FlankingLinks=flankingLinks.ToArray();if(structureId.IsEmpty||Cells.Count==0)throw new InvalidOperationException("Interior navigation data is empty.");}
    public StableId StructureId{get;} public IReadOnlyList<NavigationCell> Cells{get;} public IReadOnlyList<NavigationPortal> Portals{get;} public IReadOnlyList<NavigationLink> Links{get;} public IReadOnlyList<CoverPoint> Cover{get;} public IReadOnlyList<RestrictedArea> RestrictedAreas{get;} public IReadOnlyList<FlankingLink> FlankingLinks{get;}
    public StateChecksum ContentChecksum{get{var writer=new CanonicalWriter();WriteCanonical(writer);return writer.ComputeChecksum();}}
    public void WriteCanonical(CanonicalWriter writer){writer.WriteStableId(StructureId);writer.WriteCount(Cells.Count);foreach(NavigationCell cell in Cells.OrderBy(static value=>value.Id)){writer.WriteStableId(cell.Id);writer.WriteStableId(cell.RoomId);writer.WriteInt32(cell.Floor);writer.WriteInt32(cell.Bounds.MinimumXCentimeters);writer.WriteInt32(cell.Bounds.MinimumZCentimeters);writer.WriteInt32(cell.Bounds.MaximumXCentimeters);writer.WriteInt32(cell.Bounds.MaximumZCentimeters);writer.WriteInt32((int)cell.Access);writer.WriteBoolean(cell.QuestCritical);}writer.WriteCount(Portals.Count);foreach(NavigationPortal portal in Portals.OrderBy(static value=>value.Id)){writer.WriteStableId(portal.Id);writer.WriteStableId(portal.DoorId);writer.WriteStableId(portal.FromCellId);writer.WriteStableId(portal.ToCellId);writer.WriteBoolean(portal.Exterior);writer.WriteInt32((int)portal.DoorState);writer.WriteInt32((int)portal.LockPolicy);writer.WriteInt32(portal.WidthCentimeters);}writer.WriteCount(Links.Count);foreach(NavigationLink link in Links.OrderBy(static value=>value.Id)){writer.WriteStableId(link.Id);writer.WriteStableId(link.FromCellId);writer.WriteStableId(link.ToCellId);writer.WriteInt32((int)link.Kind);writer.WriteBoolean(link.QuestCritical);}writer.WriteCount(Cover.Count);foreach(CoverPoint point in Cover.OrderBy(static value=>value.Id)){writer.WriteStableId(point.Id);writer.WriteStableId(point.CellId);writer.WriteInt32(point.Position.XCentimeters);writer.WriteInt32(point.Position.YCentimeters);writer.WriteInt32(point.Position.ZCentimeters);writer.WriteInt32(point.NormalQuarterTurns);writer.WriteBoolean(point.HighCover);}writer.WriteCount(RestrictedAreas.Count);foreach(RestrictedArea area in RestrictedAreas.OrderBy(static value=>value.Id)){writer.WriteStableId(area.Id);writer.WriteStableId(area.CellId);writer.WriteString(area.Reason);}writer.WriteCount(FlankingLinks.Count);foreach(FlankingLink link in FlankingLinks.OrderBy(static value=>value.Id)){writer.WriteStableId(link.Id);writer.WriteStableId(link.OriginCellId);writer.WriteStableId(link.DestinationCellId);writer.WriteInt32(link.RouteDiversityScore);}}
}
