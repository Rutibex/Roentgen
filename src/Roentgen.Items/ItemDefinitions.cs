using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record ItemDefinition(
    string Id, string DisplayName, ItemKind Kind, IReadOnlySet<string> Tags, int MassGrams, int BulkMilliunits,
    int BaseValueMinorUnits, StackPolicy StackPolicy, int MaximumStackSize, string AssetRecipeFamily,
    string MaterialProfileId, string? DescriptionTemplateId = null) : ICanonicalWritable
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id); ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        if (MassGrams < 0 || BulkMilliunits < 0 || BaseValueMinorUnits < 0) throw new InvalidOperationException($"Item definition '{Id}' has negative physical or economic values.");
        if (MaximumStackSize <= 0) throw new InvalidOperationException($"Item definition '{Id}' has an invalid stack size.");
        if (StackPolicy == StackPolicy.Never && MaximumStackSize != 1) throw new InvalidOperationException($"Non-stackable item '{Id}' must have stack size one.");
    }
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id); writer.WriteString(DisplayName); writer.WriteInt32((int)Kind); writer.WriteCount(Tags.Count); foreach (string tag in Tags.Order(StringComparer.Ordinal)) writer.WriteString(tag);
        writer.WriteInt32(MassGrams); writer.WriteInt32(BulkMilliunits); writer.WriteInt32(BaseValueMinorUnits); writer.WriteInt32((int)StackPolicy); writer.WriteInt32(MaximumStackSize); writer.WriteString(AssetRecipeFamily); writer.WriteString(MaterialProfileId); writer.WriteString(DescriptionTemplateId);
    }
}

public sealed class ItemDefinitionCatalog : ICanonicalWritable
{
    private readonly SortedDictionary<string, ItemDefinition> definitions = new(StringComparer.Ordinal);
    public IReadOnlyCollection<ItemDefinition> Definitions => definitions.Values;
    public void Add(ItemDefinition definition) { ArgumentNullException.ThrowIfNull(definition); definition.Validate(); if (!definitions.TryAdd(definition.Id, definition)) throw new InvalidOperationException($"Duplicate item definition '{definition.Id}'."); }
    public ItemDefinition Get(string id) => definitions.TryGetValue(id, out ItemDefinition? value) ? value : throw new KeyNotFoundException($"Unknown item definition '{id}'.");
    public bool TryGet(string id, out ItemDefinition? definition) => definitions.TryGetValue(id, out definition);
    public IReadOnlyList<ItemDefinition> WithTag(string tag) => definitions.Values.Where(value => value.Tags.Contains(tag)).ToArray();
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteCount(definitions.Count); foreach (ItemDefinition definition in definitions.Values) definition.WriteCanonical(writer); }
}

public sealed record ItemDamageComponent(ItemDamageKind Type, int Amount) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Type); writer.WriteInt32(Amount); }
}

public sealed record WeaponStatModifiers(
    int DamageAdditive, int DamageMultiplierPermille, int CapacityAdditive, int RangeMultiplierPermille,
    int SpreadAdditivePermille, int RecoilAdditivePermille, int ReliabilityAdditivePermille,
    int AttackTimeMultiplierPermille, int ReloadTimeMultiplierPermille, int MassAdditiveGrams) : ICanonicalWritable
{
    public static WeaponStatModifiers Neutral => new(0, 1000, 0, 1000, 0, 0, 0, 1000, 1000, 0);
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32(DamageAdditive); writer.WriteInt32(DamageMultiplierPermille); writer.WriteInt32(CapacityAdditive); writer.WriteInt32(RangeMultiplierPermille); writer.WriteInt32(SpreadAdditivePermille); writer.WriteInt32(RecoilAdditivePermille); writer.WriteInt32(ReliabilityAdditivePermille); writer.WriteInt32(AttackTimeMultiplierPermille); writer.WriteInt32(ReloadTimeMultiplierPermille); writer.WriteInt32(MassAdditiveGrams); }
}

public sealed record WeaponFamilyDefinition(
    string Id, string DisplayName, string CombatFamilyId, string AttackKind, IReadOnlyList<ItemDamageComponent> BaseDamage,
    int BaseCapacity, int BaseRangeMillimeters, int BaseSpreadPermille, int BaseRecoilPermille, int BaseReliabilityPermille,
    int BaseAttackTicks, int BaseReloadTicks, int BaseMassGrams, IReadOnlySet<WeaponComponentSlot> RequiredSlots,
    IReadOnlySet<string> AmmunitionTags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteString(DisplayName); writer.WriteString(CombatFamilyId); writer.WriteString(AttackKind); writer.WriteCount(BaseDamage.Count); foreach (ItemDamageComponent c in BaseDamage) c.WriteCanonical(writer); writer.WriteInt32(BaseCapacity); writer.WriteInt32(BaseRangeMillimeters); writer.WriteInt32(BaseSpreadPermille); writer.WriteInt32(BaseRecoilPermille); writer.WriteInt32(BaseReliabilityPermille); writer.WriteInt32(BaseAttackTicks); writer.WriteInt32(BaseReloadTicks); writer.WriteInt32(BaseMassGrams); writer.WriteCount(RequiredSlots.Count); foreach (WeaponComponentSlot slot in RequiredSlots.Order()) writer.WriteInt32((int)slot); writer.WriteCount(AmmunitionTags.Count); foreach (string tag in AmmunitionTags.Order(StringComparer.Ordinal)) writer.WriteString(tag); }
}

public sealed record WeaponComponentDefinition(
    string Id, string DisplayName, WeaponComponentSlot Slot, IReadOnlySet<string> CompatibleFamilyIds,
    IReadOnlySet<string> Tags, WeaponStatModifiers Modifiers, string MaterialProfileId, string AssetRecipeFragment,
    string AcousticContribution, int BaseValueMinorUnits) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteString(DisplayName); writer.WriteInt32((int)Slot); writer.WriteCount(CompatibleFamilyIds.Count); foreach (string value in CompatibleFamilyIds.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteCount(Tags.Count); foreach (string value in Tags.Order(StringComparer.Ordinal)) writer.WriteString(value); Modifiers.WriteCanonical(writer); writer.WriteString(MaterialProfileId); writer.WriteString(AssetRecipeFragment); writer.WriteString(AcousticContribution); writer.WriteInt32(BaseValueMinorUnits); }
}

public sealed record AmmunitionDefinition(
    string Id, string DisplayName, AmmunitionKind Kind, IReadOnlySet<string> CompatibilityTags, int MassPerUnitGrams,
    IReadOnlyList<ItemDamageComponent> DamageModifiers, int PenetrationPermille, int VelocityModifierPermille,
    int ReliabilityModifierPermille, string ProjectileProfileId, string EffectProfileId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteString(DisplayName); writer.WriteInt32((int)Kind); writer.WriteCount(CompatibilityTags.Count); foreach (string value in CompatibilityTags.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteInt32(MassPerUnitGrams); writer.WriteCount(DamageModifiers.Count); foreach (ItemDamageComponent value in DamageModifiers) value.WriteCanonical(writer); writer.WriteInt32(PenetrationPermille); writer.WriteInt32(VelocityModifierPermille); writer.WriteInt32(ReliabilityModifierPermille); writer.WriteString(ProjectileProfileId); writer.WriteString(EffectProfileId); }
}

public sealed record ArmorProtection(ItemDamageKind Type, int Threshold, int ResistancePermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Type); writer.WriteInt32(Threshold); writer.WriteInt32(ResistancePermille); }
}

public sealed record ArmorItemDefinition(
    string Id, ItemArmorLayer Layer, IReadOnlySet<string> CoveredRegions, IReadOnlyList<ArmorProtection> Protection,
    int MobilityPenaltyPermille, int NoisePermille, int EnvironmentalProtectionPermille,
    IReadOnlySet<string> SocialAppearanceTags, IReadOnlySet<string> CompatibleModificationTags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)Layer); writer.WriteCount(CoveredRegions.Count); foreach (string value in CoveredRegions.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteCount(Protection.Count); foreach (ArmorProtection value in Protection.OrderBy(static value => value.Type)) value.WriteCanonical(writer); writer.WriteInt32(MobilityPenaltyPermille); writer.WriteInt32(NoisePermille); writer.WriteInt32(EnvironmentalProtectionPermille); writer.WriteCount(SocialAppearanceTags.Count); foreach (string value in SocialAppearanceTags.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteCount(CompatibleModificationTags.Count); foreach (string value in CompatibleModificationTags.Order(StringComparer.Ordinal)) writer.WriteString(value); }
}

public sealed record ItemEffectDefinition(string EffectType, int Magnitude, int DurationTicks, string? TargetTag = null) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(EffectType); writer.WriteInt32(Magnitude); writer.WriteInt32(DurationTicks); writer.WriteString(TargetTag); }
}

public sealed record ConsumableDefinition(string Id, ConsumableUseKind UseKind, int UseTicks, int ChargesPerUse, IReadOnlyList<ItemEffectDefinition> Effects, IReadOnlySet<string> RequirementTags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)UseKind); writer.WriteInt32(UseTicks); writer.WriteInt32(ChargesPerUse); writer.WriteCount(Effects.Count); foreach (ItemEffectDefinition value in Effects) value.WriteCanonical(writer); writer.WriteCount(RequirementTags.Count); foreach (string value in RequirementTags.Order(StringComparer.Ordinal)) writer.WriteString(value); }
}
