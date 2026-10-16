using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Rpg;

namespace Roentgen.Combat;

public sealed record AttackFactor(string Id, int ModifierPermille, string Explanation);
public sealed record AttackEvaluation(int SpreadPermille, int EstimatedHitChancePermille, IReadOnlyList<AttackFactor> Factors);

public interface ICombatCapabilityProvider
{
    int GetSkillPermille(StableId actorId, WeaponFamilyKind family, CombatAttackKind attackKind);
    int GetAwarenessPermille(StableId actorId);
    int GetMeleeForcePermille(StableId actorId);
    int GetReloadSpeedPermille(StableId actorId);
    int GetKnockdownResistancePermille(StableId actorId);
}

public sealed class BaselineCombatCapabilityProvider : ICombatCapabilityProvider
{
    public int GetSkillPermille(StableId actorId, WeaponFamilyKind family, CombatAttackKind attackKind) => 500;
    public int GetAwarenessPermille(StableId actorId) => 500;
    public int GetMeleeForcePermille(StableId actorId) => 500;
    public int GetReloadSpeedPermille(StableId actorId) => 1000;
    public int GetKnockdownResistancePermille(StableId actorId) => 500;
}

public sealed class RpgCombatCapabilityProvider : ICombatCapabilityProvider
{
    private readonly ProgressionCapabilityService capabilities;
    public RpgCombatCapabilityProvider(ProgressionCapabilityService capabilities) => this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    public int GetSkillPermille(StableId actorId, WeaponFamilyKind family, CombatAttackKind attackKind)
    {
        SkillId skill = attackKind == CombatAttackKind.Melee || family is WeaponFamilyKind.Unarmed or WeaponFamilyKind.ImprovisedMelee or WeaponFamilyKind.CreatureNatural
            ? (family == WeaponFamilyKind.Unarmed ? SkillId.Unarmed : SkillId.Melee)
            : family switch
            {
                WeaponFamilyKind.Radiographic => SkillId.DirectedEnergy,
                WeaponFamilyKind.Grenade => SkillId.Demolitions,
                _ => SkillId.Ballistics
            };
        return Math.Clamp(capabilities.GetEffectiveSkill(actorId, skill) * 10, 0, 1000);
    }
    public int GetAwarenessPermille(StableId actorId) => Math.Clamp(capabilities.Get(actorId).Attributes[AttributeId.Awareness] * 100, 0, 1000);
    public int GetMeleeForcePermille(StableId actorId) => Math.Clamp(capabilities.Get(actorId).Attributes[AttributeId.Physique] * 100, 0, 1000);
    public int GetReloadSpeedPermille(StableId actorId) => capabilities.Get(actorId).DerivedStatistics[DerivedStatId.ReloadSpeedPermille];
    public int GetKnockdownResistancePermille(StableId actorId) => capabilities.Get(actorId).DerivedStatistics[DerivedStatId.KnockdownResistancePermille];
}

public sealed class AttackEvaluator
{
    private readonly ICombatCapabilityProvider capabilities;
    public AttackEvaluator(ICombatCapabilityProvider? capabilities = null) => this.capabilities = capabilities ?? new BaselineCombatCapabilityProvider();

    public AttackEvaluation Evaluate(CombatActorState actor, WeaponRuntimeState weapon, WeaponDefinition definition, int distanceMillimeters, bool targetMoving, bool targetInCover)
    {
        var factors = new List<AttackFactor>();
        AddPenalty("weapon", definition.BaseSpreadPermille, "weapon dispersion");
        if (distanceMillimeters > definition.MaximumRangeMillimeters / 2) AddPenalty("range", (distanceMillimeters - definition.MaximumRangeMillimeters / 2) * 180 / Math.Max(1, definition.MaximumRangeMillimeters / 2), "long range");
        if (actor.Presentation.Velocity != FixedVector3.Zero) AddPenalty("movement", 120, "shooter moving");
        if (targetMoving) AddPenalty("target-movement", 80, "target moving");
        if (targetInCover) AddPenalty("cover", 180, "partial cover");
        if (weapon.RecoilPermille > 0) AddPenalty("recoil", weapon.RecoilPermille / 2, "accumulated recoil");
        int armPenalty = Math.Max(actor.Presentation.Injury.LeftArmImpairmentPermille, actor.Presentation.Injury.RightArmImpairmentPermille) / 4;
        if (armPenalty > 0) AddPenalty("injury", armPenalty, "arm impairment");
        int conditionPenalty = (1000 - weapon.ConditionPermille) / 3;
        if (conditionPenalty > 0) AddPenalty("condition", conditionPenalty, "poor weapon condition");
        int skill = capabilities.GetSkillPermille(actor.Id, definition.Family, definition.AttackKind);
        int awareness = capabilities.GetAwarenessPermille(actor.Id);
        int skillReduction = Math.Max(0, skill - 300) / 3;
        int awarenessReduction = Math.Max(0, awareness - 300) / 6;
        if (skillReduction > 0) factors.Add(new AttackFactor("skill", -skillReduction, "trained weapon handling"));
        if (awarenessReduction > 0) factors.Add(new AttackFactor("awareness", -awarenessReduction, "target observation"));
        int spread = Math.Clamp(factors.Sum(static factor => factor.ModifierPermille), 0, 1000);
        int chance = Math.Clamp(1000 - spread * 2 - distanceMillimeters * 250 / Math.Max(1, definition.MaximumRangeMillimeters), 50, 980);
        return new AttackEvaluation(spread, chance, factors.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray());

        void AddPenalty(string id, int value, string explanation) => factors.Add(new AttackFactor(id, Math.Max(0, value), explanation));
    }
}
