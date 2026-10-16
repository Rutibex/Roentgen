using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Rpg;

namespace Roentgen.Focus;

public sealed class RpgFocusCapabilityProvider : IFocusCapabilityProvider
{
    private readonly ProgressionCapabilityService capabilities;
    public RpgFocusCapabilityProvider(ProgressionCapabilityService capabilities) => this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));

    public int GetWeaponSkillPermille(StableId actorId, WeaponFamilyKind family)
    {
        SkillId skill = family switch
        {
            WeaponFamilyKind.Radiographic => SkillId.DirectedEnergy,
            WeaponFamilyKind.Grenade => SkillId.Demolitions,
            WeaponFamilyKind.ImprovisedMelee or WeaponFamilyKind.CreatureNatural => SkillId.Melee,
            WeaponFamilyKind.Unarmed => SkillId.Unarmed,
            _ => SkillId.Ballistics
        };
        return Math.Clamp(capabilities.GetEffectiveSkill(actorId, skill) * 10, 0, 1000);
    }

    public int GetAwarenessPermille(StableId actorId) => Math.Clamp(capabilities.Get(actorId).Attributes[AttributeId.Awareness] * 100, 0, 1000);
    public int GetReflexPermille(StableId actorId) => Math.Clamp(capabilities.Get(actorId).Attributes[AttributeId.Reflex] * 100, 0, 1000);
    public int GetCostModifierPermille(StableId actorId, FocusActionKind action)
    {
        int modifier = 1000;
        modifier += capabilities.SumPerkEffect(actorId, "focus.cost.modify", action == FocusActionKind.Fire ? "repeat-weapon" : action.ToString().ToLowerInvariant());
        return Math.Clamp(modifier, 500, 1500);
    }
}
