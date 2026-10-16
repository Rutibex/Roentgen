using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World.Structures;

/// <summary>Compiles the same room boundaries and apertures used by interaction and navigation.</summary>
public static class ArchitecturalGeometryCompiler
{
    public static List<StructurePrimitive> Compile(StableId id, BuildingGrammar grammar, StructureRect footprint,
        int floorCount, IReadOnlyList<RoomNode> rooms, IReadOnlyList<DoorDefinition> doors,
        IReadOnlyList<VerticalLinkDefinition> stairs, ICollection<StructureStreamingCell> cells)
    {
        var output = new List<StructurePrimitive>();
        int h = grammar.StructuralSystem.FloorHeightCentimeters, t = grammar.StructuralSystem.WallThicknessCentimeters;
        StructureMaterialKind material = grammar.StructuralSystem.Kind switch
        {
            StructuralSystemKind.ReinforcedConcrete => StructureMaterialKind.ReinforcedConcrete,
            StructuralSystemKind.IndustrialSteel => StructureMaterialKind.IndustrialSteel,
            StructuralSystemKind.MasonryBlock => StructureMaterialKind.Masonry,
            _ => StructureMaterialKind.CorrugatedMetal
        };
        StableId shell = StableIdFactory.CreateChild(id,"cell","shell");
        cells.Add(new StructureStreamingCell(shell,"exterior-shell",0,StructureCellLayer.ExteriorShell,
            Box(footprint.MinimumXCentimeters-t,-60,footprint.MinimumZCentimeters-t,footprint.MaximumXCentimeters+t,floorCount*h+100,footprint.MaximumZCentimeters+t),[],doors.Where(d=>d.Exterior).Select(d=>d.Id).ToArray()));
        for (int floor=0;floor<floorCount;floor++)
        {
            int y=floor*h;
            StableId cell=StableIdFactory.CreateChild(id,"cell",$"floor-{floor}");
            StableId decor=StableIdFactory.CreateChild(id,"cell",$"decoration-{floor}");
            StructureBox cellBounds=Box(footprint.MinimumXCentimeters,y,footprint.MinimumZCentimeters,footprint.MaximumXCentimeters,y+h,footprint.MaximumZCentimeters);
            cells.Add(new StructureStreamingCell(cell,$"floor-{floor}",floor,StructureCellLayer.CriticalInterior,cellBounds,
                rooms.Where(r=>r.Floor==floor).Select(r=>r.Id).ToArray(),doors.Where(d=>d.Center.YCentimeters>=y&&d.Center.YCentimeters<y+h).Select(d=>d.Id).ToArray()));
            cells.Add(new StructureStreamingCell(decor,$"furnishing-{floor}",floor,StructureCellLayer.Decoration,cellBounds,[],[]));
            VerticalLinkDefinition? below=stairs.FirstOrDefault(s=>s.Bounds.Maximum.YCentimeters==y);
            if(below is null) Add(StructurePrimitiveKind.Floor,material,Box(footprint.MinimumXCentimeters,y-20,footprint.MinimumZCentimeters,footprint.MaximumXCentimeters,y,footprint.MaximumZCentimeters),cell);
            else
            {
                StructureBox cut=below.Bounds;
                // Four non-overlapping slab sections leave the complete flight aperture open.
                Slab(footprint.MinimumXCentimeters,footprint.MinimumZCentimeters,cut.Minimum.XCentimeters,footprint.MaximumZCentimeters);
                Slab(cut.Maximum.XCentimeters,footprint.MinimumZCentimeters,footprint.MaximumXCentimeters,footprint.MaximumZCentimeters);
                Slab(cut.Minimum.XCentimeters,footprint.MinimumZCentimeters,cut.Maximum.XCentimeters,cut.Minimum.ZCentimeters);
                Slab(cut.Minimum.XCentimeters,cut.Maximum.ZCentimeters,cut.Maximum.XCentimeters,footprint.MaximumZCentimeters);
                // Guard the open side while leaving both landings accessible.
                Add(StructurePrimitiveKind.Railing,StructureMaterialKind.IndustrialSteel,
                    Box(cut.Maximum.XCentimeters+3,y+85,cut.Minimum.ZCentimeters+15,cut.Maximum.XCentimeters+8,y+95,cut.Maximum.ZCentimeters-15),cell);
                for(int z=cut.Minimum.ZCentimeters+40;z<cut.Maximum.ZCentimeters;z+=140)
                    Add(StructurePrimitiveKind.Railing,StructureMaterialKind.IndustrialSteel,Box(cut.Maximum.XCentimeters+3,y,z,cut.Maximum.XCentimeters+8,y+95,z+5),cell);
            }
            void Slab(int x0,int z0,int x1,int z1){if(x1>x0&&z1>z0)Add(StructurePrimitiveKind.Floor,material,Box(x0,y-20,z0,x1,y,z1),cell);}

            // Exterior walls: framed windows and door headers are actual geometry.
            Wall(true,footprint.MinimumZCentimeters,footprint.MinimumXCentimeters,footprint.MaximumXCentimeters,true);
            Wall(true,footprint.MaximumZCentimeters,footprint.MinimumXCentimeters,footprint.MaximumXCentimeters,true);
            Wall(false,footprint.MinimumXCentimeters,footprint.MinimumZCentimeters,footprint.MaximumZCentimeters,true);
            Wall(false,footprint.MaximumXCentimeters,footprint.MinimumZCentimeters,footprint.MaximumZCentimeters,true);
            var edges=new HashSet<(bool AlongX,int Fixed,int Start,int End)>();
            foreach(RoomNode room in rooms.Where(r=>r.Floor==floor))
            {
                StructureRect b=room.Bounds;
                Edge(true,b.MinimumZCentimeters,b.MinimumXCentimeters,b.MaximumXCentimeters);
                Edge(true,b.MaximumZCentimeters,b.MinimumXCentimeters,b.MaximumXCentimeters);
                Edge(false,b.MinimumXCentimeters,b.MinimumZCentimeters,b.MaximumZCentimeters);
                Edge(false,b.MaximumXCentimeters,b.MinimumZCentimeters,b.MaximumZCentimeters);
            }
            // Merge collinear overlapping edges before cutting doors; adjacent rooms share one wall.
            foreach(var group in edges.GroupBy(e=>(e.AlongX,e.Fixed)))
            {
                var segments=group.OrderBy(e=>e.Start).ToArray();int start=segments[0].Start,end=segments[0].End;
                foreach(var segment in segments.Skip(1))
                {
                    if(segment.Start<=end){end=Math.Max(end,segment.End);continue;}
                    Wall(group.Key.AlongX,group.Key.Fixed,start,end,false);start=segment.Start;end=segment.End;
                }
                Wall(group.Key.AlongX,group.Key.Fixed,start,end,false);
            }
            void Edge(bool along,int axis,int start,int end)
            {
                if(along&&(axis==footprint.MinimumZCentimeters||axis==footprint.MaximumZCentimeters))return;
                if(!along&&(axis==footprint.MinimumXCentimeters||axis==footprint.MaximumXCentimeters))return;
                edges.Add((along,axis,start,end));
            }
            void Wall(bool along,int axis,int start,int end,bool exterior)
            {
                var openings=new List<(int A,int B,int Bottom,int Top)>();
                foreach(DoorDefinition door in doors.Where(d=>d.AlongX==along&&Math.Abs((along?d.Center.ZCentimeters:d.Center.XCentimeters)-axis)<=1&&d.Center.YCentimeters>=y&&d.Center.YCentimeters<y+h))
                {
                    int center=along?door.Center.XCentimeters:door.Center.ZCentimeters;
                    if(center>start&&center<end)openings.Add((center-door.WidthCentimeters/2,center+door.WidthCentimeters/2,0,door.HeightCentimeters));
                }
                if(exterior)
                {
                    for(int center=start+180;center<end-140;center+=320)
                    {
                        if(openings.Any(o=>center+80>o.A-65&&center-80<o.B+65))continue;
                        openings.Add((center-65,center+65,110,235));
                    }
                }
                int cursor=start;
                foreach(var opening in openings.OrderBy(o=>o.A))
                {
                    int a=Math.Clamp(opening.A,start,end),b=Math.Clamp(opening.B,start,end);
                    if(a>cursor)Segment(cursor,a,0,h-20,material,t);
                    if(opening.Bottom>0)Segment(a,b,0,opening.Bottom,material,t);
                    if(opening.Top<h-20)Segment(a,b,opening.Top,h-20,material,t);
                    // Window and door frames use small steel sections; windows remain unglazed ruins.
                    Segment(a-5,a+2,opening.Bottom,opening.Top,StructureMaterialKind.IndustrialSteel,t+8);
                    Segment(b-2,b+5,opening.Bottom,opening.Top,StructureMaterialKind.IndustrialSteel,t+8);
                    Segment(a,b,opening.Top-4,opening.Top+4,StructureMaterialKind.IndustrialSteel,t+8);
                    if(opening.Bottom>0)Segment(a-6,b+6,opening.Bottom-6,opening.Bottom+2,StructureMaterialKind.IndustrialSteel,t+12);
                    cursor=Math.Max(cursor,b);
                }
                if(cursor<end)Segment(cursor,end,0,h-20,material,t);
                void Segment(int a,int b,int bottom,int top,StructureMaterialKind mat,int thickness)
                {
                    if(b<=a||top<=bottom)return;
                    var bounds=along?Box(a,y+bottom,axis-thickness/2,b,y+top,axis+thickness/2):Box(axis-thickness/2,y+bottom,a,axis+thickness/2,y+top,b);
                    Add(StructurePrimitiveKind.Wall,mat,bounds,exterior?shell:cell,exterior);
                }
            }
            // Shallow wall-side furniture leaves room centers, portals and quest reservations clear.
            foreach(RoomNode room in rooms.Where(r=>r.Floor==floor&&r.Archetype is not (RoomArchetype.Corridor or RoomArchetype.Stairwell)))
            {
                var b=room.Bounds;if(b.WidthCentimeters<300||b.DepthCentimeters<260)continue;
                int x=b.MinimumXCentimeters+55,z=b.MinimumZCentimeters+70;
                if(room.Archetype is RoomArchetype.Sleeping or RoomArchetype.Treatment)
                {
                    Add(StructurePrimitiveKind.Debris,StructureMaterialKind.IndustrialSteel,Box(x,y,z,x+85,y+40,z+180),decor,false,false);
                    Add(StructurePrimitiveKind.Debris,StructureMaterialKind.Timber,Box(x-2,y+40,z-2,x+87,y+55,z+182),decor,false,false);
                }
                else
                {
                    for(int shelf=0;shelf<3;shelf++)Add(StructurePrimitiveKind.Debris,StructureMaterialKind.Timber,Box(x,y+40+shelf*45,z,x+140,y+46+shelf*45,z+45),decor,false,false);
                    for(int post=0;post<2;post++)Add(StructurePrimitiveKind.Column,StructureMaterialKind.IndustrialSteel,Box(x+post*135,y,z,x+post*135+5,y+150,z+45),decor,false,false);
                }
                // Exposed service conduit follows the wall instead of crossing the walking route.
                Add(StructurePrimitiveKind.Beam,StructureMaterialKind.IndustrialSteel,Box(b.MaximumXCentimeters-28,y+h-48,b.MinimumZCentimeters+35,b.MaximumXCentimeters-20,y+h-40,b.MaximumZCentimeters-35),decor,false,false);
            }
        }
        foreach(VerticalLinkDefinition flight in stairs)
        {
            int rise=flight.Bounds.Maximum.YCentimeters-flight.Bounds.Minimum.YCentimeters;
            int run=flight.Bounds.Maximum.ZCentimeters-flight.Bounds.Minimum.ZCentimeters;
            int steps=(rise+17)/18;
            StableId cell=cells.First(c=>c.Layer==StructureCellLayer.CriticalInterior&&c.Floor==flight.Bounds.Minimum.YCentimeters/h).Id;
            for(int step=0;step<steps;step++)
            {
                int z0=flight.Bounds.Minimum.ZCentimeters+run*step/steps,z1=flight.Bounds.Minimum.ZCentimeters+run*(step+1)/steps;
                int top=flight.Bounds.Minimum.YCentimeters+rise*(step+1)/steps;
                Add(StructurePrimitiveKind.Stair,material,Box(flight.Bounds.Minimum.XCentimeters,top-18,z0,flight.Bounds.Maximum.XCentimeters,top,z1),cell);
            }
        }
        int roofY=floorCount*h;
        Add(StructurePrimitiveKind.Roof,StructureMaterialKind.Roofing,Box(footprint.MinimumXCentimeters-35,roofY-20,footprint.MinimumZCentimeters-35,footprint.MaximumXCentimeters+35,roofY+10,footprint.MaximumZCentimeters+35),shell,true);
        // Roof equipment and perimeter trim give each structure a visible silhouette.
        Add(StructurePrimitiveKind.Beam,StructureMaterialKind.IndustrialSteel,Box(footprint.MinimumXCentimeters-38,roofY-24,footprint.MinimumZCentimeters-38,footprint.MaximumXCentimeters+38,roofY+16,footprint.MinimumZCentimeters-25),shell,true);
        int chimneyX=footprint.MaximumXCentimeters-180,chimneyZ=footprint.MaximumZCentimeters-180;
        Add(StructurePrimitiveKind.Column,material,Box(chimneyX,roofY,chimneyZ,chimneyX+80,roofY+160,chimneyZ+80),shell,true);
        return output;

        void Add(StructurePrimitiveKind kind,StructureMaterialKind mat,StructureBox bounds,StableId cell,bool exterior=false,bool collision=true)
            =>output.Add(new StructurePrimitive(StableIdFactory.CreateChild(id,"primitive",output.Count),kind,mat,bounds,cell,exterior,collision,false,0));
    }
    private static StructureBox Box(int x0,int y0,int z0,int x1,int y1,int z1)=>new(new(x0,y0,z0),new(x1,y1,z1));
}
