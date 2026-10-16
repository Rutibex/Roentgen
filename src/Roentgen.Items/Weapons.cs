using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record WeaponCapabilitySnapshot(
    StableId ItemId, string FamilyId, string CombatFamilyId, string AttackKind, IReadOnlyList<ItemDamageComponent> Damage,
    int MagazineCapacity, int MaximumRangeMillimeters, int SpreadPermille, int RecoilPermille,
    int ReliabilityPermille, int AttackTicks, int ReloadTicks, int MassGrams,
    IReadOnlySet<string> AmmunitionTags, StateChecksum Fingerprint) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(ItemId); writer.WriteString(FamilyId); writer.WriteString(CombatFamilyId); writer.WriteString(AttackKind); writer.WriteCount(Damage.Count); foreach (ItemDamageComponent value in Damage.OrderBy(static value => value.Type)) value.WriteCanonical(writer); writer.WriteInt32(MagazineCapacity); writer.WriteInt32(MaximumRangeMillimeters); writer.WriteInt32(SpreadPermille); writer.WriteInt32(RecoilPermille); writer.WriteInt32(ReliabilityPermille); writer.WriteInt32(AttackTicks); writer.WriteInt32(ReloadTicks); writer.WriteInt32(MassGrams); writer.WriteCount(AmmunitionTags.Count); foreach (string value in AmmunitionTags.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteChecksum(Fingerprint); }
}

public sealed class WeaponAssemblyCompiler
{
    public WeaponCapabilitySnapshot Compile(ItemInstanceState weapon, WeaponFamilyDefinition family, IReadOnlyDictionary<string, WeaponComponentDefinition> componentDefinitions, ItemWorldState world)
    {
        ArgumentNullException.ThrowIfNull(weapon); ArgumentNullException.ThrowIfNull(family); ArgumentNullException.ThrowIfNull(componentDefinitions); ArgumentNullException.ThrowIfNull(world);
        var bySlot = new SortedDictionary<WeaponComponentSlot, WeaponComponentDefinition>();
        foreach ((WeaponComponentSlot slot, StableId componentId) in weapon.InstalledComponents) { ItemInstanceState component = world.GetItem(componentId); if (!componentDefinitions.TryGetValue(component.DefinitionId, out WeaponComponentDefinition? definition)) throw new InvalidOperationException($"Unknown weapon component '{component.DefinitionId}'."); if (definition.Slot != slot) throw new InvalidOperationException($"Component '{definition.Id}' is installed in the wrong slot."); if (definition.CompatibleFamilyIds.Count > 0 && !definition.CompatibleFamilyIds.Contains(family.Id)) throw new InvalidOperationException($"Component '{definition.Id}' is incompatible with '{family.Id}'."); bySlot[slot] = definition; }
        foreach (WeaponComponentSlot required in family.RequiredSlots) if (!bySlot.ContainsKey(required)) throw new InvalidOperationException($"Weapon '{weapon.Id}' is missing required slot '{required}'.");
        int damageAdd = 0, damageMultiplier = 1000, capacity = family.BaseCapacity, range = family.BaseRangeMillimeters, spread = family.BaseSpreadPermille, recoil = family.BaseRecoilPermille, reliability = family.BaseReliabilityPermille, attack = family.BaseAttackTicks, reload = family.BaseReloadTicks, mass = family.BaseMassGrams;
        foreach (WeaponComponentDefinition component in bySlot.Values) { WeaponStatModifiers m = component.Modifiers; damageAdd = checked(damageAdd + m.DamageAdditive); damageMultiplier = Multiply(damageMultiplier, m.DamageMultiplierPermille); capacity = checked(capacity + m.CapacityAdditive); range = Multiply(range, m.RangeMultiplierPermille); spread = checked(spread + m.SpreadAdditivePermille); recoil = checked(recoil + m.RecoilAdditivePermille); reliability = checked(reliability + m.ReliabilityAdditivePermille); attack = Multiply(attack, m.AttackTimeMultiplierPermille); reload = Multiply(reload, m.ReloadTimeMultiplierPermille); mass = checked(mass + m.MassAdditiveGrams); }
        int condition = weapon.Condition.OverallPermille; reliability = Math.Clamp(reliability * condition / 1000, 50, 1000); spread = Math.Clamp(spread + (1000 - weapon.Condition.CalibrationPermille) / 2, 0, 1000); recoil = Math.Clamp(recoil, 0, 1000); capacity = Math.Max(0, capacity); range = Math.Max(100, range); attack = Math.Max(1, attack); reload = Math.Max(1, reload); mass = Math.Max(1, mass);
        ItemDamageComponent[] damage = family.BaseDamage.Select(value => new ItemDamageComponent(value.Type, Math.Max(0, Multiply(value.Amount + damageAdd, damageMultiplier)))).ToArray();
        var writer = new CanonicalWriter(); writer.WriteStableId(weapon.Id); writer.WriteString(family.Id); writer.WriteCount(bySlot.Count); foreach ((WeaponComponentSlot slot, WeaponComponentDefinition component) in bySlot) { writer.WriteInt32((int)slot); component.WriteCanonical(writer); } weapon.Condition.WriteCanonical(writer);
        return new WeaponCapabilitySnapshot(weapon.Id, family.Id, family.CombatFamilyId, family.AttackKind, damage, capacity, range, spread, recoil, reliability, attack, reload, mass, family.AmmunitionTags, writer.ComputeChecksum());
    }
    private static int Multiply(int value, int permille) => checked((int)((long)value * permille / 1000L));
}
