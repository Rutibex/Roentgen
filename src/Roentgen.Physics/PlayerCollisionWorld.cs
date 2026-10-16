using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.Physics;

[Flags]
public enum CollisionQueryMask
{
    None = 0,
    Terrain = 1,
    Structures = 2,
    Doors = 4,
    Characters = 8,
    All = Terrain | Structures | Doors | Characters
}

public enum PlayerCollisionKind { None=0,Terrain=1,Structure=2,Door=3,StreamingFrontier=4,Character=5 }

public readonly record struct PlayerCollisionHit(PlayerCollisionKind Kind,StableId EntityId,FixedVector3 Position,double NormalX,double NormalY,double NormalZ,int TravelMillimeters,int SurfaceTopMillimeters);
public readonly record struct GroundContact(bool Valid,int HeightMillimeters,double NormalX,double NormalY,double NormalZ,StableId EntityId,bool StreamingReady);

public sealed record DynamicDoorCollider(StableId DoorId,StructureBox Bounds,int OpenProgressPermille,bool BlocksMovement,bool BlocksSight);

public sealed class DynamicDoorCollisionRegistry
{
    private readonly SortedDictionary<StableId,DynamicDoorCollider> _doors=[];
    public IReadOnlyCollection<DynamicDoorCollider> Doors=>_doors.Values;
    public void Set(DynamicDoorCollider collider)=>_doors[collider.DoorId]=collider;
    public bool Remove(StableId id)=>_doors.Remove(id);
    public bool TryGet(StableId id,out DynamicDoorCollider? collider)=>_doors.TryGetValue(id,out collider);
    public bool Raycast(StructureCollisionPoint origin,StructureCollisionVector direction,int maximumCentimeters,out StructureRaycastHit hit)
    {
        double best=maximumCentimeters+1;DynamicDoorCollider? selected=null;StructureCollisionVector normal=default;
        foreach(DynamicDoorCollider door in _doors.Values.Where(static value=>value.BlocksSight))
        {
            var data=new StructureCollisionData(door.DoorId,new[]{new StaticStructureCollider(door.DoorId,door.DoorId,door.DoorId,door.Bounds,StructurePrimitiveKind.Wall,true,true)});
            if(StructureCollisionQueries.Raycast(data,origin,direction,maximumCentimeters,out StructureRaycastHit candidate,true)&&candidate.TravelCentimeters<best){best=candidate.TravelCentimeters;selected=door;normal=candidate.Normal;}
        }
        if(selected is null){hit=default;return false;}StructureCollisionVector n=direction.Normalized();var point=new StructureCollisionPoint(origin.XCentimeters+(int)Math.Round(n.X*best),origin.YCentimeters+(int)Math.Round(n.Y*best),origin.ZCentimeters+(int)Math.Round(n.Z*best));hit=new StructureRaycastHit(selected.DoorId,selected.DoorId,selected.DoorId,point,normal,(int)Math.Round(best));return true;
    }
    public bool Overlaps(StructureCollisionPoint center,int radius,int halfHeight)
    {
        StructureBox capsule=new(new StructurePoint(center.XCentimeters-radius,center.YCentimeters-halfHeight,center.ZCentimeters-radius),new StructurePoint(center.XCentimeters+radius,center.YCentimeters+halfHeight,center.ZCentimeters+radius));
        return _doors.Values.Any(door=>door.BlocksMovement&&door.Bounds.Intersects(capsule));
    }
    public bool Sweep(StructureCollisionPoint start,StructureCollisionPoint end,int radius,int halfHeight,out StructureSweepHit hit)
    {
        int best=int.MaxValue;StructureSweepHit selected=default;bool found=false;
        foreach(DynamicDoorCollider door in _doors.Values.Where(static value=>value.BlocksMovement))
        {
            var data=new StructureCollisionData(door.DoorId,new[]{new StaticStructureCollider(door.DoorId,door.DoorId,door.DoorId,door.Bounds,StructurePrimitiveKind.Wall,true,true)});
            if(StructureCollisionQueries.SweepCapsule(data,start,end,radius,halfHeight,out StructureSweepHit candidate)&&candidate.TravelCentimeters<best){best=candidate.TravelCentimeters;selected=candidate;found=true;}
        }
        hit=selected;return found;
    }
}

/// <summary>
/// Cheap always-current collision proxies for living characters. Character geometry is animated and
/// unsuitable as a physics source, so player collision uses a deterministic upright body proxy.
/// </summary>
public sealed record DynamicCharacterCollider(StableId CharacterId,StructureBox Bounds,int RadiusMillimeters,int HeightMillimeters,bool BlocksMovement=true);

public sealed class DynamicCharacterCollisionRegistry
{
    private readonly SortedDictionary<StableId,DynamicCharacterCollider> _characters=[];
    public IReadOnlyCollection<DynamicCharacterCollider> Characters=>_characters.Values;

    public void Set(StableId id,FixedVector3 feetPosition,int radiusMillimeters,int heightMillimeters,bool blocksMovement=true)
    {
        if(id.IsEmpty)throw new ArgumentException("Character ID cannot be empty.",nameof(id));
        if(radiusMillimeters<=0||heightMillimeters<=0)throw new ArgumentOutOfRangeException(nameof(radiusMillimeters));
        int x=checked((int)(feetPosition.XMillimeters/10));
        int y=checked((int)(feetPosition.YMillimeters/10));
        int z=checked((int)(feetPosition.ZMillimeters/10));
        int radius=Math.Max(1,(radiusMillimeters+9)/10);
        int height=Math.Max(radius*2,(heightMillimeters+9)/10);
        var bounds=new StructureBox(new StructurePoint(x-radius,y,z-radius),new StructurePoint(x+radius,y+height,z+radius));
        _characters[id]=new DynamicCharacterCollider(id,bounds,radiusMillimeters,heightMillimeters,blocksMovement);
    }

    public bool Remove(StableId id)=>_characters.Remove(id);
    public void Clear()=>_characters.Clear();

    public bool Sweep(StructureCollisionPoint start,StructureCollisionPoint end,int radius,int halfHeight,out StructureSweepHit hit)
    {
        double dx=end.XCentimeters-start.XCentimeters,dz=end.ZCentimeters-start.ZCentimeters;
        double horizontalLength=Math.Sqrt(dx*dx+dz*dz);int best=int.MaxValue;StructureSweepHit selected=default;bool found=false;
        foreach(DynamicCharacterCollider character in _characters.Values.Where(static value=>value.BlocksMovement))
        {
            StructureBox b=character.Bounds;
            if(Math.Max(start.YCentimeters,end.YCentimeters)+halfHeight<b.Minimum.YCentimeters||Math.Min(start.YCentimeters,end.YCentimeters)-halfHeight>b.Maximum.YCentimeters)continue;
            double cx=(b.Minimum.XCentimeters+b.Maximum.XCentimeters)*0.5,cz=(b.Minimum.ZCentimeters+b.Maximum.ZCentimeters)*0.5;
            double characterRadius=(b.Maximum.XCentimeters-b.Minimum.XCentimeters)*0.5,totalRadius=radius+characterRadius;
            double ox=start.XCentimeters-cx,oz=start.ZCentimeters-cz;double c=ox*ox+oz*oz-totalRadius*totalRadius;
            double t;
            if(c<=0)
            {
                // If an NPC has stepped into the player, allow movement that separates the two
                // bodies, but block movement that pushes farther through the NPC.
                if(dx*ox+dz*oz>0)continue;
                t=0;
            }
            else
            {
                double a=dx*dx+dz*dz;if(a<=1e-9)continue;double q=ox*dx+oz*dz;double discriminant=q*q-a*c;if(discriminant<0)continue;t=(-q-Math.Sqrt(discriminant))/a;if(t<0||t>1)continue;
            }
            double hx=start.XCentimeters+dx*t,hz=start.ZCentimeters+dz*t,nx=hx-cx,nz=hz-cz,nlen=Math.Sqrt(nx*nx+nz*nz);if(nlen<=1e-9){nx=-dx;nz=-dz;nlen=Math.Max(1e-9,Math.Sqrt(nx*nx+nz*nz));}
            int travel=(int)Math.Round(horizontalLength*t);if(travel>=best)continue;
            best=travel;selected=new StructureSweepHit(character.CharacterId,character.CharacterId,new StructureCollisionPoint((int)Math.Round(hx),(int)Math.Round(start.YCentimeters+(end.YCentimeters-start.YCentimeters)*t),(int)Math.Round(hz)),new StructureCollisionVector(nx/nlen,0,nz/nlen),travel);found=true;
        }
        hit=selected;return found;
    }

    public bool Overlaps(StructureCollisionPoint center,int radius,int halfHeight,StableId ignoredCharacterId=default)
    {
        StructureBox capsule=new(new StructurePoint(center.XCentimeters-radius,center.YCentimeters-halfHeight,center.ZCentimeters-radius),new StructurePoint(center.XCentimeters+radius,center.YCentimeters+halfHeight,center.ZCentimeters+radius));
        return _characters.Values.Any(character=>character.BlocksMovement&&character.CharacterId!=ignoredCharacterId&&character.Bounds.Intersects(capsule));
    }
}

public interface IPlayerCollisionWorld
{
    bool CastCapsule(FixedVector3 feetStart,FixedVector3 feetEnd,int radiusMillimeters,int heightMillimeters,CollisionQueryMask mask,out PlayerCollisionHit hit);
    GroundContact ProbeGround(FixedVector3 feetPosition,int maximumRiseMillimeters,int maximumDropMillimeters);
    bool Raycast(FixedVector3 origin,FixedVector3 direction,int maximumDistanceMillimeters,CollisionQueryMask mask,out PlayerCollisionHit hit);
    bool HasStandingClearance(FixedVector3 feetPosition,int radiusMillimeters,int heightMillimeters);
}

public sealed class PlayerCollisionWorld : IPlayerCollisionWorld
{
    private readonly TerrainCollisionRegistry _terrain;private readonly StructureCollisionRegistry _structures;private readonly DynamicDoorCollisionRegistry _doors;private readonly DynamicCharacterCollisionRegistry? _characters;
    public PlayerCollisionWorld(TerrainCollisionRegistry terrain,StructureCollisionRegistry structures,DynamicDoorCollisionRegistry doors,DynamicCharacterCollisionRegistry? characters=null){_terrain=terrain;_structures=structures;_doors=doors;_characters=characters;}
    public bool CastCapsule(FixedVector3 feetStart,FixedVector3 feetEnd,int radiusMillimeters,int heightMillimeters,CollisionQueryMask mask,out PlayerCollisionHit hit)
    {
        StructureCollisionPoint start=ToCenter(feetStart,heightMillimeters);StructureCollisionPoint end=ToCenter(feetEnd,heightMillimeters);// Reserve one centimeter for millimeter-to-centimeter query quantization.
        // Otherwise sub-centimeter ticks can drift into the wall before a sweep exists.
        int radius=Math.Max(1,(radiusMillimeters+9)/10)+1;int half=Math.Max(radius,(heightMillimeters+19)/20);int best=int.MaxValue;PlayerCollisionHit selected=default;bool found=false;
        if(mask.HasFlag(CollisionQueryMask.Structures)&&_structures.SweepCapsuleMovement(start,end,radius,half,out StructureSweepHit structureHit,out int surfaceTop)) {best=structureHit.TravelCentimeters;selected=From(structureHit,PlayerCollisionKind.Structure,surfaceTop);found=true;}
        if(mask.HasFlag(CollisionQueryMask.Doors)&&_doors.Sweep(start,end,radius,half,out StructureSweepHit doorHit)&&doorHit.TravelCentimeters<best){best=doorHit.TravelCentimeters;selected=From(doorHit,PlayerCollisionKind.Door,0);found=true;}
        if(mask.HasFlag(CollisionQueryMask.Characters)&&_characters is not null&&_characters.Sweep(start,end,radius,half,out StructureSweepHit characterHit)&&characterHit.TravelCentimeters<best){best=characterHit.TravelCentimeters;selected=From(characterHit,PlayerCollisionKind.Character,0);found=true;}
        hit=selected;return found;
    }
    public GroundContact ProbeGround(FixedVector3 feetPosition,int maximumRiseMillimeters,int maximumDropMillimeters)
    {
        int x=checked((int)(feetPosition.XMillimeters/10));int z=checked((int)(feetPosition.ZMillimeters/10));int current=checked((int)(feetPosition.YMillimeters/10));int best=int.MinValue;double nx=0,ny=1,nz=0;StableId id=StableId.Empty;bool ready=false;
        if(_terrain.TrySample(x,z,out TerrainSurfaceSample terrain)){ready=true;int h=terrain.HeightCentimeters;if(h<=current+maximumRiseMillimeters/10&&h>=current-maximumDropMillimeters/10&&h>best){best=h;nx=terrain.Normal.X;ny=terrain.Normal.Y;nz=terrain.Normal.Z;ready=true;}}
        if(_structures.TrySampleSupport(x,current,z,maximumRiseMillimeters/10,maximumDropMillimeters/10,out StructureSupportSample support)&&support.HeightCentimeters>best){best=support.HeightCentimeters;nx=support.Normal.X;ny=support.Normal.Y;nz=support.Normal.Z;id=support.StructureId;ready=true;}
        return best != int.MinValue ? new GroundContact(true,best*10,nx,ny,nz,id,true) : new GroundContact(false,(int)feetPosition.YMillimeters,0,1,0,StableId.Empty,ready);
    }
    public bool Raycast(FixedVector3 origin,FixedVector3 direction,int maximumDistanceMillimeters,CollisionQueryMask mask,out PlayerCollisionHit hit)
    {
        var start=new StructureCollisionPoint((int)(origin.XMillimeters/10),(int)(origin.YMillimeters/10),(int)(origin.ZMillimeters/10));var vector=new StructureCollisionVector(direction.XMillimeters,direction.YMillimeters,direction.ZMillimeters);int max=Math.Max(1,maximumDistanceMillimeters/10);int best=int.MaxValue;PlayerCollisionHit selected=default;bool found=false;
        if(mask.HasFlag(CollisionQueryMask.Terrain)&&TryRaycastTerrain(origin,direction,maximumDistanceMillimeters,out PlayerCollisionHit terrainHit)){best=terrainHit.TravelMillimeters/10;selected=terrainHit;found=true;}
        if(mask.HasFlag(CollisionQueryMask.Structures)&&_structures.Raycast(start,vector,max,out StructureRaycastHit structure,true)&&structure.TravelCentimeters<best){best=structure.TravelCentimeters;selected=new PlayerCollisionHit(PlayerCollisionKind.Structure,structure.StructureId,ToMm(structure.Position),structure.Normal.X,structure.Normal.Y,structure.Normal.Z,best*10,0);found=true;}
        if(mask.HasFlag(CollisionQueryMask.Doors)&&_doors.Raycast(start,vector,max,out StructureRaycastHit door)&&door.TravelCentimeters<best){best=door.TravelCentimeters;selected=new PlayerCollisionHit(PlayerCollisionKind.Door,door.StructureId,ToMm(door.Position),door.Normal.X,door.Normal.Y,door.Normal.Z,best*10,0);found=true;}
        hit=selected;return found;
    }
    private bool TryRaycastTerrain(FixedVector3 origin,FixedVector3 direction,int maximumDistanceMillimeters,out PlayerCollisionHit hit)
    {
        double length=Math.Sqrt((double)direction.XMillimeters*direction.XMillimeters+(double)direction.YMillimeters*direction.YMillimeters+(double)direction.ZMillimeters*direction.ZMillimeters);
        if(length<=0.0001||maximumDistanceMillimeters<=0){hit=default;return false;}
        double dx=direction.XMillimeters/length,dy=direction.YMillimeters/length,dz=direction.ZMillimeters/length;
        const int stepMillimeters=200;
        for(int travel=stepMillimeters;travel<=maximumDistanceMillimeters;travel+=stepMillimeters)
        {
            long x=origin.XMillimeters+(long)Math.Round(dx*travel);long y=origin.YMillimeters+(long)Math.Round(dy*travel);long z=origin.ZMillimeters+(long)Math.Round(dz*travel);
            if(_terrain.TrySample(checked((int)(x/10)),checked((int)(z/10)),out TerrainSurfaceSample sample)&&y<=sample.HeightCentimeters*10L)
            {
                var position=new FixedVector3(x,sample.HeightCentimeters*10L,z);
                hit=new PlayerCollisionHit(PlayerCollisionKind.Terrain,StableId.Empty,position,sample.Normal.X,sample.Normal.Y,sample.Normal.Z,travel,sample.HeightCentimeters*10);
                return true;
            }
        }
        hit=default;return false;
    }
    public bool HasStandingClearance(FixedVector3 feetPosition,int radiusMillimeters,int heightMillimeters)
    {
        StructureCollisionPoint center=ToCenter(feetPosition,heightMillimeters);int radius=Math.Max(1,radiusMillimeters/10);int half=Math.Max(radius,heightMillimeters/20);
        return !_structures.OverlapsCapsule(center,radius,half)&&!_doors.Overlaps(center,radius,half)&&(_characters is null||!_characters.Overlaps(center,radius,half));
    }
    private static StructureCollisionPoint ToCenter(FixedVector3 feet,int heightMm)=>new((int)(feet.XMillimeters/10),(int)((feet.YMillimeters+heightMm/2)/10),(int)(feet.ZMillimeters/10));
    private static FixedVector3 ToMm(StructureCollisionPoint p)=>new((long)p.XCentimeters*10,(long)p.YCentimeters*10,(long)p.ZCentimeters*10);
    private static PlayerCollisionHit From(StructureSweepHit hit,PlayerCollisionKind kind,int top)=>new(kind,hit.StructureId,ToMm(hit.Position),hit.Normal.X,hit.Normal.Y,hit.Normal.Z,hit.TravelCentimeters*10,top*10);
}
