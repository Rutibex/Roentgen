using System.Collections.Concurrent;
using Roentgen.Contracts;
using Roentgen.World.Structures;

namespace Roentgen.Physics;

public readonly record struct StructureSupportSample(StableId StructureId,StableId ColliderId,int HeightCentimeters,StructureCollisionVector Normal);

public sealed class StructureCollisionRegistry
{
    private readonly ConcurrentDictionary<StableId,StructureCollisionData> _structures=new();
    public int Count=>_structures.Count;public long EstimatedBytes=>_structures.Values.Sum(static data=>data.EstimatedBytes);
    public void Register(StructureCollisionData data){ArgumentNullException.ThrowIfNull(data);_structures[data.StructureId]=data;}
    public bool Remove(StableId structureId)=>_structures.TryRemove(structureId,out _);public bool TryGet(StableId structureId,out StructureCollisionData? data)=>_structures.TryGetValue(structureId,out data);
    public bool Raycast(StructureCollisionPoint origin,StructureCollisionVector direction,int maximumDistanceCentimeters,out StructureRaycastHit hit,bool sightOnly=false)
    {
        double best=maximumDistanceCentimeters+1d;StructureRaycastHit bestHit=default;bool found=false;foreach(StructureCollisionData data in _structures.Values.OrderBy(static value=>value.StructureId))if(StructureCollisionQueries.Raycast(data,origin,direction,maximumDistanceCentimeters,out StructureRaycastHit candidate,sightOnly)&&candidate.TravelCentimeters<best){best=candidate.TravelCentimeters;bestHit=candidate;found=true;}hit=bestHit;return found;
    }
    public bool SweepCapsule(StructureCollisionPoint start,StructureCollisionPoint end,int radiusCentimeters,int halfHeightCentimeters,out StructureSweepHit hit)=>SweepCapsuleInternal(start,end,radiusCentimeters,halfHeightCentimeters,null,out hit,out _);
    public bool SweepCapsuleMovement(StructureCollisionPoint start,StructureCollisionPoint end,int radiusCentimeters,int halfHeightCentimeters,out StructureSweepHit hit,out int surfaceTopCentimeters)
    {
        bool vertical=Math.Abs(end.YCentimeters-start.YCentimeters)>Math.Abs(end.XCentimeters-start.XCentimeters)+Math.Abs(end.ZCentimeters-start.ZCentimeters);
        return SweepCapsuleInternal(start,end,radiusCentimeters,halfHeightCentimeters,collider=>collider.Kind is StructurePrimitiveKind.Wall or StructurePrimitiveKind.Column or StructurePrimitiveKind.Beam or StructurePrimitiveKind.Railing or StructurePrimitiveKind.Debris || vertical&&collider.Kind is StructurePrimitiveKind.Ceiling or StructurePrimitiveKind.Roof,out hit,out surfaceTopCentimeters);
    }
    private bool SweepCapsuleInternal(StructureCollisionPoint start,StructureCollisionPoint end,int radius,int halfHeight,Func<StaticStructureCollider,bool>? predicate,out StructureSweepHit hit,out int surfaceTop)
    {
        int maximum=(int)Math.Ceiling(Math.Sqrt(Math.Pow(end.XCentimeters-start.XCentimeters,2)+Math.Pow(end.YCentimeters-start.YCentimeters,2)+Math.Pow(end.ZCentimeters-start.ZCentimeters,2)));int best=maximum+1;StructureSweepHit selected=default;surfaceTop=0;bool found=false;
        foreach(StructureCollisionData data in _structures.Values.OrderBy(static value=>value.StructureId))
        {
            StructureCollisionData filtered=predicate is null?data:new StructureCollisionData(data.StructureId,data.Colliders.Where(predicate).ToArray());if(StructureCollisionQueries.SweepCapsule(filtered,start,end,radius,halfHeight,out StructureSweepHit candidate)&&candidate.TravelCentimeters<best){best=candidate.TravelCentimeters;selected=candidate;found=true;StaticStructureCollider? collider=filtered.Colliders.FirstOrDefault(value=>value.Id==candidate.ColliderId);surfaceTop=collider?.WorldBounds.Maximum.YCentimeters??0;}
        }
        hit=selected;return found;
    }
    public bool OverlapsCapsule(StructureCollisionPoint center,int radiusCentimeters,int halfHeightCentimeters)
    {
        StructureBox capsule=new(new StructurePoint(center.XCentimeters-radiusCentimeters,center.YCentimeters-halfHeightCentimeters,center.ZCentimeters-radiusCentimeters),new StructurePoint(center.XCentimeters+radiusCentimeters,center.YCentimeters+halfHeightCentimeters,center.ZCentimeters+radiusCentimeters));
        foreach(StructureCollisionData data in _structures.Values)foreach(StaticStructureCollider collider in data.Colliders.Where(static value=>value.BlocksMovement&&value.Kind is not (StructurePrimitiveKind.Floor or StructurePrimitiveKind.Foundation)))if(collider.WorldBounds.Intersects(capsule))return true;
        return false;
    }
    public bool TrySampleSupport(int xCentimeters,int currentYCentimeters,int zCentimeters,int maximumRiseCentimeters,int maximumDropCentimeters,out StructureSupportSample sample)
    {
        int best=int.MinValue;StaticStructureCollider? selected=null;foreach(StructureCollisionData data in _structures.Values.OrderBy(static value=>value.StructureId))foreach(StaticStructureCollider collider in data.Colliders.Where(static value=>value.Kind is StructurePrimitiveKind.Floor or StructurePrimitiveKind.Foundation or StructurePrimitiveKind.Stair or StructurePrimitiveKind.Ramp))
        {
            StructureBox b=collider.WorldBounds;if(xCentimeters<b.Minimum.XCentimeters||xCentimeters>b.Maximum.XCentimeters||zCentimeters<b.Minimum.ZCentimeters||zCentimeters>b.Maximum.ZCentimeters)continue;int top=b.Maximum.YCentimeters;if(top>currentYCentimeters+maximumRiseCentimeters||top<currentYCentimeters-maximumDropCentimeters||top<=best)continue;best=top;selected=collider;
        }
        if(selected is null){sample=default;return false;}sample=new StructureSupportSample(selected.StructureId,selected.Id,best,new StructureCollisionVector(0,1,0));return true;
    }
}
