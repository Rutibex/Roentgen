using Roentgen.Entities;
using Roentgen.Physics;

namespace Roentgen.Combat;

public sealed class PlayerCollisionCombatOcclusion(IPlayerCollisionWorld collisionWorld) : ICombatOcclusionProvider
{
    private readonly IPlayerCollisionWorld _collisionWorld = collisionWorld ?? throw new ArgumentNullException(nameof(collisionWorld));

    public bool RaycastEnvironment(FixedVector3 origin, FixedVector3 direction, int maximumDistanceMillimeters, out FixedVector3 hitPosition, out int travelMillimeters)
    {
        if (_collisionWorld.Raycast(origin, direction, maximumDistanceMillimeters, CollisionQueryMask.All, out PlayerCollisionHit hit))
        {
            hitPosition = hit.Position;
            travelMillimeters = hit.TravelMillimeters;
            return true;
        }
        hitPosition = default;
        travelMillimeters = 0;
        return false;
    }
}
