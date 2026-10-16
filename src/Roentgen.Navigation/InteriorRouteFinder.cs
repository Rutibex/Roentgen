using Roentgen.Contracts;
using Roentgen.World.Structures;

namespace Roentgen.Navigation;

public sealed record InteriorRoute(bool Found,IReadOnlyList<StableId> CellPath,IReadOnlyList<StableId> PortalOrLinkPath,int Cost);

public sealed class InteriorRouteFinder
{
    public InteriorRoute Find(InteriorNavigationData data,StableId startCell,StableId destinationCell,TraversalProfile profile)
    {
        ArgumentNullException.ThrowIfNull(data);ArgumentNullException.ThrowIfNull(profile);
        if(startCell==destinationCell)return new InteriorRoute(true,new[]{startCell},Array.Empty<StableId>(),0);
        var adjacency=data.Cells.Select(static cell=>cell.Id).Append(ExteriorId(data)).Distinct().ToDictionary(static id=>id,_=>new List<Edge>());
        foreach(NavigationPortal portal in data.Portals)
        {
            if(!CanUse(portal,profile))continue;int cost=portal.DoorState==DoorLifecycleState.Open?1:2;adjacency[portal.FromCellId].Add(new Edge(portal.ToCellId,portal.Id,cost));adjacency[portal.ToCellId].Add(new Edge(portal.FromCellId,portal.Id,cost));
        }
        foreach(NavigationLink link in data.Links)
        {
            if(link.Kind==VerticalLinkKind.Ladder&&!profile.Capabilities.Contains(TraversalCapability.CanClimb))continue;adjacency[link.FromCellId].Add(new Edge(link.ToCellId,link.Id,3));adjacency[link.ToCellId].Add(new Edge(link.FromCellId,link.Id,3));
        }
        if(!adjacency.ContainsKey(startCell)||!adjacency.ContainsKey(destinationCell))return new InteriorRoute(false,Array.Empty<StableId>(),Array.Empty<StableId>(),int.MaxValue);
        var queue=new PriorityQueue<StableId,(int Cost,string Id)>();var costBy=new Dictionary<StableId,int>{{startCell,0}};var previous=new Dictionary<StableId,(StableId Parent,StableId Edge)>();queue.Enqueue(startCell,(0,startCell.ToString()));
        while(queue.TryDequeue(out StableId current,out _))
        {
            if(current==destinationCell)break;int currentCost=costBy[current];foreach(Edge edge in adjacency[current].OrderBy(static edge=>edge.To).ThenBy(static edge=>edge.Id)){int next=checked(currentCost+edge.Cost);if(!costBy.TryGetValue(edge.To,out int old)||next<old){costBy[edge.To]=next;previous[edge.To]=(current,edge.Id);queue.Enqueue(edge.To,(next,edge.To.ToString()));}}
        }
        if(!costBy.TryGetValue(destinationCell,out int total))return new InteriorRoute(false,Array.Empty<StableId>(),Array.Empty<StableId>(),int.MaxValue);
        var cells=new List<StableId>{destinationCell};var edges=new List<StableId>();StableId cursor=destinationCell;while(cursor!=startCell){var step=previous[cursor];edges.Add(step.Edge);cursor=step.Parent;cells.Add(cursor);}cells.Reverse();edges.Reverse();return new InteriorRoute(true,cells,edges,total);
    }

    public StableId ExteriorId(InteriorNavigationData data)=>StableIdFactoryShim(data.StructureId);
    private static StableId StableIdFactoryShim(StableId structureId)=>Roentgen.Core.StableIdFactory.CreateChild(structureId,"nav-cell","exterior");
    private static bool CanUse(NavigationPortal portal,TraversalProfile profile)=>portal.DoorState switch
    {
        DoorLifecycleState.Destroyed or DoorLifecycleState.Missing=>false,
        DoorLifecycleState.Locked=>profile.Capabilities.Contains(TraversalCapability.CanUnlock)||profile.Capabilities.Contains(TraversalCapability.CanForce),
        DoorLifecycleState.Jammed or DoorLifecycleState.Barricaded=>profile.Capabilities.Contains(TraversalCapability.CanForce)||profile.Capabilities.Contains(TraversalCapability.CanUnlock),
        _=>true
    };
    private sealed record Edge(StableId To,StableId Id,int Cost);
}
