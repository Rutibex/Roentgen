using Roentgen.Contracts;

namespace Roentgen.Factions;

/// <summary>
/// Read-only political relationship projection used by combat and full-simulation AI.
/// It deliberately answers only immediate tactical alignment questions; diplomacy remains
/// authoritative in <see cref="SocietyWorldState"/>.
/// </summary>
public interface IFactionRelationResolver
{
    bool IsHostile(StableId observerFactionId, StableId targetFactionId);
    bool IsFriendly(StableId observerFactionId, StableId targetFactionId);
}

public sealed class SocietyFactionRelationResolver : IFactionRelationResolver
{
    private readonly SocietyWorldState world;

    public SocietyFactionRelationResolver(SocietyWorldState world) => this.world = world ?? throw new ArgumentNullException(nameof(world));

    public bool IsHostile(StableId observerFactionId, StableId targetFactionId)
    {
        if (observerFactionId.IsEmpty || targetFactionId.IsEmpty || observerFactionId == targetFactionId) return false;

        // The regression player is an unaffiliated actor rather than a generated political faction.
        // These starting attitudes are explicit campaign bindings and can later be superseded by
        // Phase 16 facts, crimes, reputation, or authored campaign state.
        if (observerFactionId == FactionIds.Player || targetFactionId == FactionIds.Player)
        {
            StableId other = observerFactionId == FactionIds.Player ? targetFactionId : observerFactionId;
            if (other == FactionIds.BlackwaterRecoveryAuthority) return true;
            return false;
        }

        return IsDirectedHostile(observerFactionId, targetFactionId) || IsDirectedHostile(targetFactionId, observerFactionId);
    }

    public bool IsFriendly(StableId observerFactionId, StableId targetFactionId)
    {
        if (observerFactionId.IsEmpty || targetFactionId.IsEmpty) return false;
        if (observerFactionId == targetFactionId) return true;
        if (observerFactionId == FactionIds.Player || targetFactionId == FactionIds.Player)
        {
            StableId other = observerFactionId == FactionIds.Player ? targetFactionId : observerFactionId;
            return other == FactionIds.ProspectClinicCompact || other == FactionIds.Civilians || other == FactionIds.RedQuarryCooperative;
        }

        return IsDirectedFriendly(observerFactionId, targetFactionId) && IsDirectedFriendly(targetFactionId, observerFactionId);
    }

    private bool IsDirectedHostile(StableId source, StableId target)
    {
        if (!world.TryGetRelationship(source, target, out FactionRelationshipState? relationship) || relationship is null) return false;
        return relationship.DerivedKind is FactionRelationKind.Hostile or FactionRelationKind.AtWar;
    }

    private bool IsDirectedFriendly(StableId source, StableId target)
    {
        if (!world.TryGetRelationship(source, target, out FactionRelationshipState? relationship) || relationship is null) return false;
        return relationship.DerivedKind is FactionRelationKind.Allied or FactionRelationKind.Cooperative or FactionRelationKind.Protected;
    }
}

public sealed class DistinctFactionHostilityResolver : IFactionRelationResolver
{
    public static DistinctFactionHostilityResolver Instance { get; } = new();
    private DistinctFactionHostilityResolver() { }
    public bool IsHostile(StableId observerFactionId, StableId targetFactionId) => !observerFactionId.IsEmpty && !targetFactionId.IsEmpty && observerFactionId != targetFactionId;
    public bool IsFriendly(StableId observerFactionId, StableId targetFactionId) => !observerFactionId.IsEmpty && observerFactionId == targetFactionId;
}
