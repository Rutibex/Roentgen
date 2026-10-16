using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.World.Structures;

namespace Roentgen.Navigation;

public sealed class StructureNavigationEmitter
{
    public InteriorNavigationData Emit(StructureBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        var cells=blueprint.Rooms.Select(room=>new NavigationCell(StableIdFactory.CreateChild(blueprint.Id,"nav-cell",room.Id.ToString()),room.Id,room.Floor,room.Bounds,room.Access,room.QuestCritical)).OrderBy(static cell=>cell.Id).ToArray();
        var byRoom=cells.ToDictionary(static cell=>cell.RoomId);
        var exteriorCell=StableIdFactory.CreateChild(blueprint.Id,"nav-cell","exterior");
        var portals=new List<NavigationPortal>();
        foreach(DoorDefinition door in blueprint.Doors.OrderBy(static door=>door.Id))
        {
            if(!byRoom.TryGetValue(door.FromRoomId,out NavigationCell? from))continue;
            StableId to=door.Exterior?exteriorCell:byRoom.TryGetValue(door.ToRoomId,out NavigationCell? target)?target.Id:StableId.Empty;
            if(to.IsEmpty)continue;
            portals.Add(new NavigationPortal(StableIdFactory.CreateChild(blueprint.Id,"nav-portal",door.Id.ToString()),door.Id,from.Id,to,door.Exterior,door.InitialState,door.LockPolicy,door.WidthCentimeters));
        }
        var links=blueprint.VerticalLinks.Where(link=>byRoom.ContainsKey(link.LowerRoomId)&&byRoom.ContainsKey(link.UpperRoomId)).Select(link=>new NavigationLink(StableIdFactory.CreateChild(blueprint.Id,"nav-link",link.Id.ToString()),byRoom[link.LowerRoomId].Id,byRoom[link.UpperRoomId].Id,link.Kind,link.Bounds,link.QuestCritical)).OrderBy(static link=>link.Id).ToArray();
        var cover=new List<CoverPoint>();int coverOrdinal=0;
        foreach(NavigationCell cell in cells.Where(static cell=>cell.Bounds.WidthCentimeters>=300&&cell.Bounds.DepthCentimeters>=300))
        {
            int y=cell.Floor*blueprint.Grammar.StructuralSystem.FloorHeightCentimeters;
            cover.Add(new CoverPoint(StableIdFactory.CreateChild(blueprint.Id,"cover",coverOrdinal++),cell.Id,new StructurePoint(cell.Bounds.MinimumXCentimeters+75,y,cell.Bounds.MinimumZCentimeters+75),1,HighCover:true));
            cover.Add(new CoverPoint(StableIdFactory.CreateChild(blueprint.Id,"cover",coverOrdinal++),cell.Id,new StructurePoint(cell.Bounds.MaximumXCentimeters-75,y,cell.Bounds.MaximumZCentimeters-75),3,HighCover:false));
        }
        var restricted=cells.Where(static cell=>cell.Access==AccessClass.Hazardous).Select((cell,index)=>new RestrictedArea(StableIdFactory.CreateChild(blueprint.Id,"restricted",index),cell.Id,new StructureBox(new StructurePoint(cell.Bounds.MinimumXCentimeters,cell.Floor*blueprint.Grammar.StructuralSystem.FloorHeightCentimeters,cell.Bounds.MinimumZCentimeters),new StructurePoint(cell.Bounds.MaximumXCentimeters,(cell.Floor+1)*blueprint.Grammar.StructuralSystem.FloorHeightCentimeters,cell.Bounds.MaximumZCentimeters)),"hazardous-access")).ToArray();
        var flank=new List<FlankingLink>();int flankOrdinal=0;
        NavigationCell[] corridors=cells.Where(static cell=>cell.Access==AccessClass.Public&&cell.Bounds.DepthCentimeters>cell.Bounds.WidthCentimeters).ToArray();
        foreach(NavigationCell corridor in corridors)foreach(NavigationCell destination in cells.Where(cell=>cell.Floor==corridor.Floor&&cell.Id!=corridor.Id&&cell.QuestCritical))flank.Add(new FlankingLink(StableIdFactory.CreateChild(blueprint.Id,"flank",flankOrdinal++),corridor.Id,destination.Id,blueprint.Doors.Count(door=>door.FromRoomId==destination.RoomId||door.ToRoomId==destination.RoomId)));
        return new InteriorNavigationData(blueprint.Id,cells,portals,links,cover,restricted,flank);
    }
}
