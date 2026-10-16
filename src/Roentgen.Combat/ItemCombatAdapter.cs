using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Items;

namespace Roentgen.Combat;

public sealed class ItemCombatDefinitionAdapter
{
    public WeaponDefinition CompileWeaponDefinition(WeaponCapabilitySnapshot capability)
    {
        WeaponFamilyKind family = capability.CombatFamilyId switch
        {
            "Revolver" => WeaponFamilyKind.Revolver,
            "SelfLoadingPistol" => WeaponFamilyKind.SelfLoadingPistol,
            "Shotgun" => WeaponFamilyKind.Shotgun,
            "HuntingRifle" or "Rifle" => WeaponFamilyKind.Rifle,
            "ImprovisedMelee" => WeaponFamilyKind.ImprovisedMelee,
            "Radiographic" => WeaponFamilyKind.Radiographic,
            _ => WeaponFamilyKind.Unarmed
        };
        CombatAttackKind attack = capability.AttackKind switch { "Melee" => CombatAttackKind.Melee, "Projectile" => CombatAttackKind.Projectile, "Explosive" => CombatAttackKind.Explosive, _ => CombatAttackKind.Hitscan };
        DamageComponent[] damage = capability.Damage.Select(value => new DamageComponent(MapDamage(value.Type), value.Amount)).ToArray();
        return new WeaponDefinition(
            capability.ItemId.ToString(), capability.FamilyId, family, attack, damage,
            capability.MagazineCapacity, capability.AttackTicks, Math.Max(1, capability.AttackTicks / 3), Math.Max(1, capability.AttackTicks / 3), capability.ReloadTicks,
            capability.MaximumRangeMillimeters, capability.SpreadPermille, capability.RecoilPermille, Math.Max(1, 1000 - capability.ReliabilityPermille),
            attack is CombatAttackKind.Projectile or CombatAttackKind.Explosive ? 24000 : 0,
            attack is CombatAttackKind.Projectile or CombatAttackKind.Explosive ? 25 : 20,
            attack == CombatAttackKind.Explosive ? 3000 : 0,
            attack == CombatAttackKind.Explosive ? 90 : 0,
            attack == CombatAttackKind.Melee ? capability.MaximumRangeMillimeters : 0);
    }
    public WeaponRuntimeState CreateRuntimeState(ItemInstanceState item, StableId ownerId, WeaponCapabilitySnapshot capability)
    {
        var result = new WeaponRuntimeState(item.Id, item.Id.ToString(), ownerId, capability.MagazineCapacity, item.AmmunitionLoad?.LoadedCount ?? 0)
        {
            ReserveAmmunition = 0,
            ConditionPermille = item.Condition.OverallPermille
        };
        return result;
    }
    public void CommitRuntimeState(ItemInstanceState item, WeaponRuntimeState runtime)
    {
        int structuralLoss = Math.Max(0, item.Condition.StructuralPermille - runtime.ConditionPermille); if (structuralLoss > 0) item.Condition.Damage(structuralLoss, structuralLoss / 2, structuralLoss / 3);
        item.Touch();
        if (item.AmmunitionLoad is not null)
        {
            int difference = item.AmmunitionLoad.LoadedCount - runtime.Ammunition; if (difference > 0) item.AmmunitionLoad.Unload(difference); else if (difference < 0 && item.AmmunitionLoad.AmmunitionDefinitionId is not null) item.AmmunitionLoad.Load(item.AmmunitionLoad.AmmunitionDefinitionId, -difference);
        }
    }
    public ArmorRuntimeState CompileArmorLayer(ArmorLayerCapability layer)
    {
        AnatomicalRegion[] regions = layer.CoveredRegions.Select(MapRegion).Distinct().Order().ToArray();
        var thresholds = layer.Protection.Select(value => new KeyValuePair<DamageType, int>(MapDamage(value.Type), value.Threshold));
        var resistances = layer.Protection.Select(value => new KeyValuePair<DamageType, int>(MapDamage(value.Type), value.ResistancePermille));
        return new ArmorRuntimeState(layer.ItemId, layer.ItemId.ToString(), MapLayer(layer.Layer), regions, thresholds, resistances, layer.ConditionPermille);
    }
    private static DamageType MapDamage(ItemDamageKind value) => (DamageType)(int)value;
    private static ArmorLayerKind MapLayer(ItemArmorLayer value) => value switch { ItemArmorLayer.PoweredFrame => ArmorLayerKind.PoweredFrame, ItemArmorLayer.OuterPlate => ArmorLayerKind.Outer, ItemArmorLayer.PrimaryProtection => ArmorLayerKind.Primary, ItemArmorLayer.Clothing => ArmorLayerKind.Clothing, _ => ArmorLayerKind.Underlayer };
    private static AnatomicalRegion MapRegion(string value) => value switch { "head" => AnatomicalRegion.Head, "torso" => AnatomicalRegion.Torso, "left-arm" => AnatomicalRegion.LeftArm, "right-arm" => AnatomicalRegion.RightArm, "left-leg" => AnatomicalRegion.LeftLeg, "right-leg" => AnatomicalRegion.RightLeg, _ => AnatomicalRegion.Torso };
}
