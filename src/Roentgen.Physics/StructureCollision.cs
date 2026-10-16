using Roentgen.Contracts;
using Roentgen.World.Structures;

namespace Roentgen.Physics;

public readonly record struct StructureCollisionPoint(int XCentimeters,int YCentimeters,int ZCentimeters)
{
    public static StructureCollisionPoint From(StructurePoint point)=>new(point.XCentimeters,point.YCentimeters,point.ZCentimeters);
}

public readonly record struct StructureCollisionVector(double X,double Y,double Z)
{
    public double Length=>Math.Sqrt(X*X+Y*Y+Z*Z);
    public StructureCollisionVector Normalized(){double length=Length;return length<=double.Epsilon?new StructureCollisionVector(0,0,0):new StructureCollisionVector(X/length,Y/length,Z/length);}
}

public sealed record StaticStructureCollider(StableId Id,StableId StructureId,StableId PrimitiveId,StructureBox WorldBounds,StructurePrimitiveKind Kind,bool BlocksMovement,bool BlocksSight)
{
    public void Validate(){if(Id.IsEmpty||StructureId.IsEmpty||PrimitiveId.IsEmpty)throw new InvalidOperationException("Static structure collider IDs cannot be empty.");WorldBounds.Validate();}
}

public readonly record struct StructureRaycastHit(StableId StructureId,StableId ColliderId,StableId PrimitiveId,StructureCollisionPoint Position,StructureCollisionVector Normal,int TravelCentimeters);
public readonly record struct StructureSweepHit(StableId StructureId,StableId ColliderId,StructureCollisionPoint Position,StructureCollisionVector Normal,int TravelCentimeters);

public sealed class StructureCollisionData
{
    public StructureCollisionData(StableId structureId,IReadOnlyList<StaticStructureCollider> colliders){StructureId=structureId;Colliders=colliders.OrderBy(static collider=>collider.Id).ToArray();foreach(StaticStructureCollider collider in Colliders)collider.Validate();}
    public StableId StructureId{get;}public IReadOnlyList<StaticStructureCollider> Colliders{get;}public long EstimatedBytes=>Colliders.Count*192L;
}

public sealed class StructureCollisionCompiler
{
    public StructureCollisionData Compile(StructureBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);var colliders=new List<StaticStructureCollider>();
        foreach(StructurePrimitive primitive in blueprint.Primitives.Where(static primitive=>primitive.CollisionEnabled&&!primitive.RemovedByDamage).OrderBy(static primitive=>primitive.Id))
        {
            StructureBox world=Translate(primitive.Bounds,blueprint.WorldOrigin);
            colliders.Add(new StaticStructureCollider(Roentgen.Core.StableIdFactory.CreateChild(blueprint.Id,"collider",primitive.Id.ToString()),blueprint.Id,primitive.Id,world,primitive.Kind,BlocksMovement:true,BlocksSight:primitive.Kind is StructurePrimitiveKind.Wall or StructurePrimitiveKind.Floor or StructurePrimitiveKind.Roof or StructurePrimitiveKind.Debris));
        }
        return new StructureCollisionData(blueprint.Id,colliders);
    }

    private static StructureBox Translate(StructureBox box,StructurePoint origin)=>new(
        new StructurePoint(checked(box.Minimum.XCentimeters+origin.XCentimeters),checked(box.Minimum.YCentimeters+origin.YCentimeters),checked(box.Minimum.ZCentimeters+origin.ZCentimeters)),
        new StructurePoint(checked(box.Maximum.XCentimeters+origin.XCentimeters),checked(box.Maximum.YCentimeters+origin.YCentimeters),checked(box.Maximum.ZCentimeters+origin.ZCentimeters)));
}

public static class StructureCollisionQueries
{
    public static bool Raycast(StructureCollisionData data,StructureCollisionPoint origin,StructureCollisionVector direction,int maximumDistanceCentimeters,out StructureRaycastHit hit,bool sightOnly=false)
    {
        ArgumentNullException.ThrowIfNull(data);if(maximumDistanceCentimeters<=0)throw new ArgumentOutOfRangeException(nameof(maximumDistanceCentimeters));StructureCollisionVector normalized=direction.Normalized();if(normalized.Length<=double.Epsilon){hit=default;return false;}
        double best=maximumDistanceCentimeters+1d;StaticStructureCollider? bestCollider=null;StructureCollisionVector bestNormal=default;
        foreach(StaticStructureCollider collider in data.Colliders)
        {
            if(sightOnly&&!collider.BlocksSight)continue;
            if(TryIntersectAabb(collider.WorldBounds,origin,normalized,out double distance,out StructureCollisionVector normal)&&distance>=0d&&distance<=maximumDistanceCentimeters&&distance<best){best=distance;bestCollider=collider;bestNormal=normal;}
        }
        if(bestCollider is null){hit=default;return false;}
        var position=new StructureCollisionPoint(checked(origin.XCentimeters+(int)Math.Round(normalized.X*best)),checked(origin.YCentimeters+(int)Math.Round(normalized.Y*best)),checked(origin.ZCentimeters+(int)Math.Round(normalized.Z*best)));
        hit=new StructureRaycastHit(bestCollider.StructureId,bestCollider.Id,bestCollider.PrimitiveId,position,bestNormal,checked((int)Math.Round(best)));return true;
    }

    public static bool SweepCapsule(StructureCollisionData data,StructureCollisionPoint start,StructureCollisionPoint end,int radiusCentimeters,int halfHeightCentimeters,out StructureSweepHit hit)
    {
        ArgumentNullException.ThrowIfNull(data);if(radiusCentimeters<=0||halfHeightCentimeters<radiusCentimeters)throw new ArgumentOutOfRangeException(nameof(radiusCentimeters));
        var direction=new StructureCollisionVector(end.XCentimeters-start.XCentimeters,end.YCentimeters-start.YCentimeters,end.ZCentimeters-start.ZCentimeters);double length=direction.Length;if(length<=double.Epsilon){hit=default;return false;}StructureCollisionVector normalized=direction.Normalized();double best=length+1d;StaticStructureCollider? bestCollider=null;StructureCollisionVector bestNormal=default;
        foreach(StaticStructureCollider collider in data.Colliders.Where(static collider=>collider.BlocksMovement))
        {
            StructureBox expanded=Expand(collider.WorldBounds,radiusCentimeters,halfHeightCentimeters);
            if(TrySweepAabb(expanded,start,normalized,out double distance,out StructureCollisionVector normal)&&distance>=0d&&distance<=length&&distance<best){best=distance;bestCollider=collider;bestNormal=normal;}
        }
        if(bestCollider is null){hit=default;return false;}var position=new StructureCollisionPoint(checked(start.XCentimeters+(int)Math.Round(normalized.X*best)),checked(start.YCentimeters+(int)Math.Round(normalized.Y*best)),checked(start.ZCentimeters+(int)Math.Round(normalized.Z*best)));hit=new StructureSweepHit(bestCollider.StructureId,bestCollider.Id,position,bestNormal,checked((int)Math.Round(best)));return true;
    }

    public static bool Contains(StructureCollisionData data,StructureCollisionPoint point,out StaticStructureCollider? collider)
    {
        ArgumentNullException.ThrowIfNull(data);StructurePoint value=new(point.XCentimeters,point.YCentimeters,point.ZCentimeters);collider=data.Colliders.FirstOrDefault(item=>item.BlocksMovement&&item.WorldBounds.Contains(value));return collider is not null;
    }

    // Sweeps need entry contacts, whereas rays starting inside a box need its exit.
    // Treat an initial overlap as a zero-distance contact on the nearest escape face.
    // Outward/tangent motion must remain possible after quantization or a streamed spawn.
    private static bool TrySweepAabb(StructureBox box, StructureCollisionPoint start,
        StructureCollisionVector direction, out double distance, out StructureCollisionVector normal)
    {
        distance = 0;
        normal = default;
        if (box.Contains(new StructurePoint(start.XCentimeters, start.YCentimeters, start.ZCentimeters)))
        {
            var faces = new (double Depth, StructureCollisionVector Normal)[]
            {
                (start.XCentimeters - box.Minimum.XCentimeters, new(-1, 0, 0)),
                (box.Maximum.XCentimeters - start.XCentimeters, new(1, 0, 0)),
                (start.YCentimeters - box.Minimum.YCentimeters, new(0, -1, 0)),
                (box.Maximum.YCentimeters - start.YCentimeters, new(0, 1, 0)),
                (start.ZCentimeters - box.Minimum.ZCentimeters, new(0, 0, -1)),
                (box.Maximum.ZCentimeters - start.ZCentimeters, new(0, 0, 1))
            };
            double nearest = faces.Min(face => face.Depth);
            foreach (var face in faces.Where(face => face.Depth == nearest))
            {
                normal = face.Normal;
                if (direction.X * normal.X + direction.Y * normal.Y + direction.Z * normal.Z >= -1e-12)
                    return false;
            }
            return true;
        }
        // A segment on a boundary plane is tangential, not an entry into the box.
        if ((Math.Abs(direction.X) < 1e-12 && (start.XCentimeters == box.Minimum.XCentimeters || start.XCentimeters == box.Maximum.XCentimeters)) ||
            (Math.Abs(direction.Y) < 1e-12 && (start.YCentimeters == box.Minimum.YCentimeters || start.YCentimeters == box.Maximum.YCentimeters)) ||
            (Math.Abs(direction.Z) < 1e-12 && (start.ZCentimeters == box.Minimum.ZCentimeters || start.ZCentimeters == box.Maximum.ZCentimeters)))
            return false;
        return TryIntersectAabb(box, start, direction, out distance, out normal);
    }

    private static StructureBox Expand(StructureBox box,int radius,int halfHeight)=>new(new StructurePoint(box.Minimum.XCentimeters-radius,box.Minimum.YCentimeters-halfHeight,box.Minimum.ZCentimeters-radius),new StructurePoint(box.Maximum.XCentimeters+radius,box.Maximum.YCentimeters+halfHeight,box.Maximum.ZCentimeters+radius));

    private static bool TryIntersectAabb(StructureBox box,StructureCollisionPoint origin,StructureCollisionVector direction,out double distance,out StructureCollisionVector normal)
    {
        double near=double.NegativeInfinity,far=double.PositiveInfinity;normal=default;StructureCollisionVector candidate=default;
        if(!Slab(origin.XCentimeters,direction.X,box.Minimum.XCentimeters,box.Maximum.XCentimeters,new StructureCollisionVector(-1,0,0),new StructureCollisionVector(1,0,0),ref near,ref far,ref candidate)||
           !Slab(origin.YCentimeters,direction.Y,box.Minimum.YCentimeters,box.Maximum.YCentimeters,new StructureCollisionVector(0,-1,0),new StructureCollisionVector(0,1,0),ref near,ref far,ref candidate)||
           !Slab(origin.ZCentimeters,direction.Z,box.Minimum.ZCentimeters,box.Maximum.ZCentimeters,new StructureCollisionVector(0,0,-1),new StructureCollisionVector(0,0,1),ref near,ref far,ref candidate)){distance=0d;return false;}
        distance=near>=0d?near:far;normal=candidate;return far>=0d&&distance>=0d;
    }

    private static bool Slab(double origin,double direction,double minimum,double maximum,StructureCollisionVector minimumNormal,StructureCollisionVector maximumNormal,ref double near,ref double far,ref StructureCollisionVector normal)
    {
        if(Math.Abs(direction)<1e-12)return origin>=minimum&&origin<=maximum;
        double t1=(minimum-origin)/direction,t2=(maximum-origin)/direction;StructureCollisionVector n1=minimumNormal,n2=maximumNormal;if(t1>t2){(t1,t2)=(t2,t1);(n1,n2)=(n2,n1);}if(t1>near){near=t1;normal=n1;}far=Math.Min(far,t2);return near<=far;
    }
}
