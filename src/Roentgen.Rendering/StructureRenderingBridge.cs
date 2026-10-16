using Microsoft.Xna.Framework;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Rendering;

public sealed record StructureRenderBatchData(string Id,StructureMaterialKind Material,StructureCellLayer Layer,MeshData Mesh,long EstimatedGpuBytes);

public static class StructureRenderingBridge
{
    public static IReadOnlyList<StructureRenderBatchData> CompileBatches(StructureBlueprint blueprint,StructureCellLayer? layer=null)
    {
        ArgumentNullException.ThrowIfNull(blueprint);var cellLayers=blueprint.StreamingCells.ToDictionary(static cell=>cell.Id,static cell=>cell.Layer);var result=new List<StructureRenderBatchData>();
        IEnumerable<StructurePrimitive> source=blueprint.Primitives.Where(static primitive=>!primitive.RemovedByDamage).Where(primitive=>layer is null||cellLayers[primitive.StreamingCellId]==layer.Value);
        foreach(var group in source.GroupBy(primitive=>(primitive.Material,Layer:cellLayers[primitive.StreamingCellId])).OrderBy(static group=>group.Key.Layer).ThenBy(static group=>group.Key.Material))
        {
            MeshData mesh=BuildMesh($"structure-{blueprint.Id}-{group.Key.Layer}-{group.Key.Material}",group.ToArray());
            result.Add(new StructureRenderBatchData(mesh.Name,group.Key.Material,group.Key.Layer,mesh,checked(mesh.Vertices.Length*36L+mesh.Indices.Length*sizeof(int))));
        }
        return result;
    }

    public static MaterialDefinition MaterialFor(StructureMaterialKind material)=>new($"structure-{material.ToString().ToLowerInvariant()}",Color.White,RoughnessFor(material),MetallicFor(material),DoubleSided:true);

    private static MeshData BuildMesh(string name,IReadOnlyList<StructurePrimitive> primitives)
    {
        var vertices=new List<RoentgenVertex>(primitives.Count*24);var indices=new List<int>(primitives.Count*36);
        foreach(StructurePrimitive primitive in primitives)AddBox(vertices,indices,primitive.Bounds,ColorFor(primitive.Material),primitive.DamagePermille);
        if(vertices.Count<3){vertices.Add(new RoentgenVertex(Vector3.Zero,Vector3.Up,Vector2.Zero,Color.Transparent));vertices.Add(new RoentgenVertex(Vector3.Right*0.01f,Vector3.Up,Vector2.UnitX,Color.Transparent));vertices.Add(new RoentgenVertex(Vector3.Forward*0.01f,Vector3.Up,Vector2.UnitY,Color.Transparent));indices.AddRange(new[]{0,1,2});}
        return new MeshData(name,vertices,indices);
    }

    private static void AddBox(ICollection<RoentgenVertex> vertices,ICollection<int> indices,StructureBox box,Color baseColor,int damage)
    {
        Vector3 min=ToMeters(box.Minimum),max=ToMeters(box.Maximum);Color color=damage<=0?baseColor:Color.Lerp(baseColor,new Color(48,42,38),Math.Clamp(damage/1000f,0f,0.75f));
        AddFace(new Vector3(min.X,min.Y,max.Z),new Vector3(max.X,min.Y,max.Z),new Vector3(max.X,max.Y,max.Z),new Vector3(min.X,max.Y,max.Z),Vector3.Forward);
        AddFace(new Vector3(max.X,min.Y,min.Z),new Vector3(min.X,min.Y,min.Z),new Vector3(min.X,max.Y,min.Z),new Vector3(max.X,max.Y,min.Z),Vector3.Backward);
        AddFace(new Vector3(min.X,min.Y,min.Z),new Vector3(min.X,min.Y,max.Z),new Vector3(min.X,max.Y,max.Z),new Vector3(min.X,max.Y,min.Z),Vector3.Left);
        AddFace(new Vector3(max.X,min.Y,max.Z),new Vector3(max.X,min.Y,min.Z),new Vector3(max.X,max.Y,min.Z),new Vector3(max.X,max.Y,max.Z),Vector3.Right);
        AddFace(new Vector3(min.X,max.Y,max.Z),new Vector3(max.X,max.Y,max.Z),new Vector3(max.X,max.Y,min.Z),new Vector3(min.X,max.Y,min.Z),Vector3.Up);
        AddFace(new Vector3(min.X,min.Y,min.Z),new Vector3(max.X,min.Y,min.Z),new Vector3(max.X,min.Y,max.Z),new Vector3(min.X,min.Y,max.Z),Vector3.Down);
        void AddFace(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        {
            normal=Vector3.Normalize(Vector3.Cross(b-a,c-a));
            int start=vertices.Count;
            float u=Vector3.Distance(a,b)/2f,v=Vector3.Distance(a,d)/2f;
            vertices.Add(new RoentgenVertex(a,normal,new Vector2(0,v),color));
            vertices.Add(new RoentgenVertex(b,normal,new Vector2(u,v),color));
            vertices.Add(new RoentgenVertex(c,normal,new Vector2(u,0),color));
            vertices.Add(new RoentgenVertex(d,normal,Vector2.Zero,color));
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);
            indices.Add(start);indices.Add(start+2);indices.Add(start+3);
        }
    }

    private static Vector3 ToMeters(StructurePoint point)=>new(WorldUnits.CentimetersToMeters(point.XCentimeters),WorldUnits.CentimetersToMeters(point.YCentimeters),WorldUnits.CentimetersToMeters(point.ZCentimeters));
    private static Color ColorFor(StructureMaterialKind material)=>material switch{StructureMaterialKind.ScrapMetal=>new Color(112,91,73),StructureMaterialKind.CorrugatedMetal=>new Color(126,105,83),StructureMaterialKind.ReinforcedConcrete=>new Color(128,128,119),StructureMaterialKind.IndustrialSteel=>new Color(105,112,111),StructureMaterialKind.Masonry=>new Color(139,119,94),StructureMaterialKind.Timber=>new Color(125,91,59),StructureMaterialKind.Glass=>new Color(123,164,163),StructureMaterialKind.Roofing=>new Color(83,76,70),StructureMaterialKind.Rubble=>new Color(100,89,78),StructureMaterialKind.Hazard=>new Color(117,139,63),_=>Color.Magenta};
    private static float RoughnessFor(StructureMaterialKind material)=>material==StructureMaterialKind.Glass?0.25f:material is StructureMaterialKind.IndustrialSteel or StructureMaterialKind.ScrapMetal?0.72f:0.92f;
    private static float MetallicFor(StructureMaterialKind material)=>material is StructureMaterialKind.IndustrialSteel or StructureMaterialKind.ScrapMetal or StructureMaterialKind.CorrugatedMetal?0.7f:0f;
}
