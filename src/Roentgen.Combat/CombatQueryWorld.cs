using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.Combat;

public readonly record struct CombatRay(FixedVector3 Origin, FixedVector3 Direction, int MaximumDistanceMillimeters, StableId IgnoreActorId);
public readonly record struct CombatHit(CombatHitOutcome Outcome, StableId ActorId, StableId BodyPartId, FixedVector3 Position, int TravelMillimeters, AnatomicalRegion Region, string Reason);
public readonly record struct ExplosionExposure(StableId ActorId, StableId BodyPartId, AnatomicalRegion Region, int DistanceMillimeters, int ExposurePermille);
public readonly record struct MeleeSweep(FixedVector3 Start, FixedVector3 End, int RadiusMillimeters, StableId IgnoreActorId);

public interface ICombatOcclusionProvider
{
    bool RaycastEnvironment(FixedVector3 origin, FixedVector3 direction, int maximumDistanceMillimeters, out FixedVector3 hitPosition, out int travelMillimeters);
}

public sealed class NullCombatOcclusionProvider : ICombatOcclusionProvider
{
    public static NullCombatOcclusionProvider Instance { get; } = new();
    public bool RaycastEnvironment(FixedVector3 origin, FixedVector3 direction, int maximumDistanceMillimeters, out FixedVector3 hitPosition, out int travelMillimeters)
    {
        hitPosition = default;
        travelMillimeters = 0;
        return false;
    }
}

public interface ICombatQueryWorld
{
    CombatHit RaycastActors(CombatRay ray, IEnumerable<CombatActorState> actors, ICombatOcclusionProvider? occlusion = null);
    CombatHit SweepMelee(MeleeSweep sweep, IEnumerable<CombatActorState> actors, ICombatOcclusionProvider? occlusion = null);
    IReadOnlyList<ExplosionExposure> QueryExplosion(FixedVector3 center, int radiusMillimeters, IEnumerable<CombatActorState> actors, StableId ignoreActorId, ICombatOcclusionProvider? occlusion = null);
}

public sealed class DynamicCombatQueryWorld : ICombatQueryWorld
{
    public CombatHit RaycastActors(CombatRay ray, IEnumerable<CombatActorState> actors, ICombatOcclusionProvider? occlusion = null)
    {
        if (ray.MaximumDistanceMillimeters <= 0) return new CombatHit(CombatHitOutcome.Invalid, StableId.Empty, StableId.Empty, ray.Origin, 0, AnatomicalRegion.None, "range");
        FixedVector3 direction = CombatMath.Normalize(ray.Direction);
        int environmentDistance = int.MaxValue;
        FixedVector3 environmentPosition = default;
        ICombatOcclusionProvider provider = occlusion ?? NullCombatOcclusionProvider.Instance;
        if (provider.RaycastEnvironment(ray.Origin, direction, ray.MaximumDistanceMillimeters, out FixedVector3 staticHit, out int staticDistance))
        {
            environmentDistance = staticDistance;
            environmentPosition = staticHit;
        }

        CombatHit best = new(CombatHitOutcome.Miss, StableId.Empty, StableId.Empty, CombatMath.PointAlong(ray.Origin, direction, ray.MaximumDistanceMillimeters), ray.MaximumDistanceMillimeters, AnatomicalRegion.None, "clear");
        double bestDistance = Math.Min(ray.MaximumDistanceMillimeters, environmentDistance);
        foreach (CombatActorState actor in actors.OrderBy(static value => value.Id))
        {
            if (actor.Id == ray.IgnoreActorId || actor.IsDead) continue;
            foreach (BodyPartRuntimeState part in actor.BodyParts.OrderBy(static value => value.Definition.Id))
            {
                FixedVector3 center = CombatSpatialModel.PartCenter(actor, part.Definition.Region);
                int radius = Math.Max(60, part.Definition.RadiusMillimeters);
                if (!TryRaySphere(ray.Origin, direction, center, radius, out double distance)) continue;
                if (distance < 0 || distance > bestDistance) continue;
                bestDistance = distance;
                best = new CombatHit(
                    CombatHitOutcome.Actor,
                    actor.Id,
                    part.Definition.Id,
                    CombatMath.PointAlong(ray.Origin, direction, (long)Math.Round(distance)),
                    (int)Math.Round(distance),
                    part.Definition.Region,
                    "body-part");
            }
        }

        if (best.Outcome == CombatHitOutcome.Miss && environmentDistance <= ray.MaximumDistanceMillimeters)
        {
            return new CombatHit(CombatHitOutcome.Environment, StableId.Empty, StableId.Empty, environmentPosition, environmentDistance, AnatomicalRegion.None, "environment");
        }
        return best;
    }


    public CombatHit SweepMelee(MeleeSweep sweep, IEnumerable<CombatActorState> actors, ICombatOcclusionProvider? occlusion = null)
    {
        int radius = Math.Max(1, sweep.RadiusMillimeters);
        FixedVector3 segment = sweep.End - sweep.Start;
        double segmentLength = CombatMath.Length(segment);
        if (segmentLength < 0.001) return new CombatHit(CombatHitOutcome.Invalid, StableId.Empty, StableId.Empty, sweep.Start, 0, AnatomicalRegion.None, "melee-segment");
        FixedVector3 direction = CombatMath.Normalize(segment);
        ICombatOcclusionProvider provider = occlusion ?? NullCombatOcclusionProvider.Instance;
        int environmentDistance = int.MaxValue;
        FixedVector3 environmentPosition = sweep.End;
        if (provider.RaycastEnvironment(sweep.Start, direction, (int)Math.Round(segmentLength), out FixedVector3 staticHit, out int staticDistance))
        {
            environmentDistance = staticDistance;
            environmentPosition = staticHit;
        }
        CombatHit best = new(CombatHitOutcome.Miss, StableId.Empty, StableId.Empty, sweep.End, (int)Math.Round(segmentLength), AnatomicalRegion.None, "melee-clear");
        double bestAlong = Math.Min(segmentLength, environmentDistance);
        foreach (CombatActorState actor in actors.OrderBy(static value => value.Id))
        {
            if (actor.Id == sweep.IgnoreActorId || actor.IsDead) continue;
            foreach (BodyPartRuntimeState part in actor.BodyParts.OrderBy(static value => value.Definition.Id))
            {
                FixedVector3 center = CombatSpatialModel.PartCenter(actor, part.Definition.Region);
                (double distanceToSegment, double along) = DistanceToSegment(sweep.Start, sweep.End, center);
                if (distanceToSegment > radius + Math.Max(60, part.Definition.RadiusMillimeters) || along > bestAlong) continue;
                bestAlong = along;
                best = new CombatHit(CombatHitOutcome.Actor, actor.Id, part.Definition.Id, CombatMath.PointAlong(sweep.Start, direction, (long)Math.Round(along)), (int)Math.Round(along), part.Definition.Region, "melee-sweep");
            }
        }
        if (best.Outcome == CombatHitOutcome.Miss && environmentDistance <= segmentLength)
        {
            return new CombatHit(CombatHitOutcome.Environment, StableId.Empty, StableId.Empty, environmentPosition, environmentDistance, AnatomicalRegion.None, "environment");
        }
        return best;
    }

    public IReadOnlyList<ExplosionExposure> QueryExplosion(FixedVector3 center, int radiusMillimeters, IEnumerable<CombatActorState> actors, StableId ignoreActorId, ICombatOcclusionProvider? occlusion = null)
    {
        if (radiusMillimeters <= 0) return [];
        ICombatOcclusionProvider provider = occlusion ?? NullCombatOcclusionProvider.Instance;
        var results = new List<ExplosionExposure>();
        foreach (CombatActorState actor in actors.OrderBy(static value => value.Id))
        {
            if (actor.Id == ignoreActorId || actor.IsDead) continue;
            BodyPartRuntimeState? selected = null;
            int selectedDistance = int.MaxValue;
            foreach (BodyPartRuntimeState part in actor.BodyParts)
            {
                FixedVector3 partCenter = CombatSpatialModel.PartCenter(actor, part.Definition.Region);
                int distance = (int)Math.Round(CombatMath.Distance(center, partCenter));
                if (distance < selectedDistance)
                {
                    selected = part;
                    selectedDistance = distance;
                }
            }
            if (selected is null || selectedDistance > radiusMillimeters) continue;
            FixedVector3 delta = CombatSpatialModel.PartCenter(actor, selected.Definition.Region) - center;
            bool blocked = provider.RaycastEnvironment(center, delta, selectedDistance, out _, out int obstructionDistance) && obstructionDistance + 100 < selectedDistance;
            int exposure = Math.Clamp(1000 - selectedDistance * 1000 / radiusMillimeters, 0, 1000);
            if (blocked) exposure /= 3;
            results.Add(new ExplosionExposure(actor.Id, selected.Definition.Id, selected.Definition.Region, selectedDistance, exposure));
        }
        return results;
    }


    private static (double Distance, double Along) DistanceToSegment(FixedVector3 start, FixedVector3 end, FixedVector3 point)
    {
        double sx = start.XMillimeters;
        double sy = start.YMillimeters;
        double sz = start.ZMillimeters;
        double vx = end.XMillimeters - sx;
        double vy = end.YMillimeters - sy;
        double vz = end.ZMillimeters - sz;
        double wx = point.XMillimeters - sx;
        double wy = point.YMillimeters - sy;
        double wz = point.ZMillimeters - sz;
        double lengthSquared = vx * vx + vy * vy + vz * vz;
        double t = lengthSquared <= 0.0001 ? 0.0 : Math.Clamp((wx * vx + wy * vy + wz * vz) / lengthSquared, 0.0, 1.0);
        double cx = sx + vx * t;
        double cy = sy + vy * t;
        double cz = sz + vz * t;
        double dx = point.XMillimeters - cx;
        double dy = point.YMillimeters - cy;
        double dz = point.ZMillimeters - cz;
        return (Math.Sqrt(dx * dx + dy * dy + dz * dz), Math.Sqrt(lengthSquared) * t);
    }

    private static bool TryRaySphere(FixedVector3 origin, FixedVector3 normalizedDirection, FixedVector3 center, int radius, out double distance)
    {
        double dx = normalizedDirection.XMillimeters / (double)CombatMath.DirectionScale;
        double dy = normalizedDirection.YMillimeters / (double)CombatMath.DirectionScale;
        double dz = normalizedDirection.ZMillimeters / (double)CombatMath.DirectionScale;
        double ox = origin.XMillimeters - center.XMillimeters;
        double oy = origin.YMillimeters - center.YMillimeters;
        double oz = origin.ZMillimeters - center.ZMillimeters;
        double b = ox * dx + oy * dy + oz * dz;
        double c = ox * ox + oy * oy + oz * oz - (double)radius * radius;
        double discriminant = b * b - c;
        if (discriminant < 0)
        {
            distance = 0;
            return false;
        }
        distance = -b - Math.Sqrt(discriminant);
        if (distance < 0) distance = -b + Math.Sqrt(discriminant);
        return distance >= 0;
    }
}

public static class CombatSpatialModel
{
    public static FixedVector3 PartCenter(CombatActorState actor, AnatomicalRegion region)
    {
        int height = actor.Presentation.Appearance.HeightMillimeters;
        long lateral = Math.Max(120, actor.Presentation.Appearance.BuildPermille * 260L / 1000L);
        (long x, long y, long z) = region switch
        {
            AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster or AnatomicalRegion.Jaw => (0, height * 88L / 100L, 0),
            AnatomicalRegion.Torso or AnatomicalRegion.ExposedGrowth => (0, height * 58L / 100L, 0),
            AnatomicalRegion.LeftArm => (-lateral, height * 60L / 100L, 0),
            AnatomicalRegion.RightArm or AnatomicalRegion.EquippedWeapon => (lateral, height * 60L / 100L, 0),
            AnatomicalRegion.LeftLeg => (-lateral / 2, height * 25L / 100L, 0),
            AnatomicalRegion.RightLeg => (lateral / 2, height * 25L / 100L, 0),
            AnatomicalRegion.FrontLeftLimb => (-lateral, height * 35L / 100L, -height * 24L / 100L),
            AnatomicalRegion.FrontRightLimb => (lateral, height * 35L / 100L, -height * 24L / 100L),
            AnatomicalRegion.RearLeftLimb => (-lateral, height * 30L / 100L, height * 24L / 100L),
            AnatomicalRegion.RearRightLimb => (lateral, height * 30L / 100L, height * 24L / 100L),
            _ => (0, height * 50L / 100L, 0)
        };
        double yaw = actor.Presentation.FacingYawUnits / 10000.0;
        double cos = Math.Cos(yaw);
        double sin = Math.Sin(yaw);
        long rotatedX = (long)Math.Round(x * cos + z * sin);
        long rotatedZ = (long)Math.Round(-x * sin + z * cos);
        return actor.Presentation.FeetPosition + new FixedVector3(rotatedX, y, rotatedZ);
    }
}
