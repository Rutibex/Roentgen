namespace Roentgen.Contracts;

public enum TargetFunction
{
    Senses,
    Consciousness,
    Mobility,
    Balance,
    WeaponHandling,
    AttackSystem,
    DefenseSystem,
    Power,
    Cooling,
    HazardProduction,
    Communication,
    LifeSupport
}

public sealed record TargetPartDefinition(
    string PartId,
    string DisplayName,
    string? ParentPartId,
    float TargetSize,
    float StructuralHealth,
    IReadOnlySet<TargetFunction> Functions,
    IReadOnlyList<PartConsequenceDefinition> Consequences,
    bool CanBeDestroyed);

public sealed record PartConsequenceDefinition(
    float DamageFractionThreshold,
    string EffectType,
    IReadOnlyDictionary<string, double> Parameters);

public sealed record FocusActionPreview(
    StableId ActorId,
    StableId TargetId,
    string TargetPartId,
    float DisplayedHitChance,
    float FocusCost,
    float ExpectedDamage,
    IReadOnlyList<string> PredictedConsequences,
    IReadOnlyList<string> UncertaintyReasons);
