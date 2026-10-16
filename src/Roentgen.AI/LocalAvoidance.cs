using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Navigation;

namespace Roentgen.AI;

/// <summary>
/// Deterministic neighborhood registry for local agent avoidance.  It retains references
/// to authoritative character state rather than copying positions into a parallel world.
/// </summary>
public sealed class NavigationAgentSpatialRegistry
{
    private readonly SortedDictionary<StableId, (CharacterState Character, NavigationAgentProfile Profile)> agents = [];

    public void Register(CharacterState character, NavigationAgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(profile);
        agents[character.Id] = (character, profile);
    }

    public bool Remove(StableId id) => agents.Remove(id);

    public IEnumerable<(CharacterState Character, NavigationAgentProfile Profile)> Neighbors(CharacterState character, int rangeMillimeters)
    {
        long rangeSquared = (long)rangeMillimeters * rangeMillimeters;
        foreach (KeyValuePair<StableId, (CharacterState Character, NavigationAgentProfile Profile)> pair in agents)
        {
            StableId id = pair.Key;
            (CharacterState Character, NavigationAgentProfile Profile) entry = pair.Value;
            if (id == character.Id) continue;
            long dx = entry.Character.FeetPosition.XMillimeters - character.FeetPosition.XMillimeters;
            long dz = entry.Character.FeetPosition.ZMillimeters - character.FeetPosition.ZMillimeters;
            if (dx * dx + dz * dz <= rangeSquared) yield return entry;
        }
    }
}

/// <summary>
/// Applies bounded reciprocal separation before the shared collision query. Equal cases
/// are resolved by stable ID so identical state and command streams produce identical motion.
/// </summary>
public sealed class AvoidanceNavigationMovementQuery(
    INavigationMovementQuery inner,
    NavigationAgentSpatialRegistry registry) : INavigationMovementQuery
{
    public bool TryMove(CharacterState character, FixedVector3 desiredStep, NavigationAgentProfile profile, out FixedVector3 actualStep)
    {
        double desiredLengthSquared = (double)desiredStep.XMillimeters * desiredStep.XMillimeters
            + (double)desiredStep.ZMillimeters * desiredStep.ZMillimeters;
        if (desiredLengthSquared <= 0) return inner.TryMove(character, desiredStep, profile, out actualStep);

        double adjustedX = desiredStep.XMillimeters;
        double adjustedZ = desiredStep.ZMillimeters;
        int searchRange = Math.Max(2_000, profile.RadiusMillimeters * 5);
        foreach ((CharacterState neighbor, NavigationAgentProfile neighborProfile) in registry.Neighbors(character, searchRange))
        {
            double candidateX = character.FeetPosition.XMillimeters + adjustedX;
            double candidateZ = character.FeetPosition.ZMillimeters + adjustedZ;
            double dx = candidateX - neighbor.FeetPosition.XMillimeters;
            double dz = candidateZ - neighbor.FeetPosition.ZMillimeters;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            int desiredSeparation = profile.RadiusMillimeters + neighborProfile.RadiusMillimeters + 120;
            if (distance >= desiredSeparation) continue;

            if (distance < 1.0)
            {
                // Stable IDs choose opposite deterministic separation directions.
                dx = character.Id.CompareTo(neighbor.Id) < 0 ? -1.0 : 1.0;
                dz = character.Id.CompareTo(neighbor.Id) < 0 ? 1.0 : -1.0;
                distance = Math.Sqrt(2.0);
            }
            double pressure = Math.Clamp((desiredSeparation - distance) / desiredSeparation, 0.0, 1.0);
            double priority = character.Id.CompareTo(neighbor.Id) < 0 ? 0.45 : 1.0;
            adjustedX += dx / distance * pressure * priority * Math.Sqrt(desiredLengthSquared);
            adjustedZ += dz / distance * pressure * priority * Math.Sqrt(desiredLengthSquared);
        }

        double adjustedLength = Math.Sqrt(adjustedX * adjustedX + adjustedZ * adjustedZ);
        double maximumLength = Math.Sqrt(desiredLengthSquared);
        if (adjustedLength > maximumLength && adjustedLength > 0)
        {
            double scale = maximumLength / adjustedLength;
            adjustedX *= scale;
            adjustedZ *= scale;
        }
        var adjusted = new FixedVector3((long)Math.Round(adjustedX), desiredStep.YMillimeters, (long)Math.Round(adjustedZ));
        return inner.TryMove(character, adjusted, profile, out actualStep);
    }
}
