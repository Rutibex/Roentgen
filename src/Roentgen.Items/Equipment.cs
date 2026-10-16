using Roentgen.Contracts;

namespace Roentgen.Items;

public static class EquipmentRules
{
    public static bool CanEquip(ItemDefinition definition, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Kind switch
        {
            ItemKind.Weapon => slot is EquipmentSlot.ActiveHand or EquipmentSlot.OffHand or EquipmentSlot.Sidearm or EquipmentSlot.Longarm or EquipmentSlot.Melee or EquipmentSlot.Throwable,
            ItemKind.Armor => slot is >= EquipmentSlot.Underlayer and <= EquipmentSlot.PoweredFrame,
            ItemKind.Consumable => slot is EquipmentSlot.QuickConsumable1 or EquipmentSlot.QuickConsumable2,
            ItemKind.Container => slot == EquipmentSlot.BackEquipment,
            _ => false
        };
    }
}

public sealed record EquipmentCapabilitySnapshot(
    StableId ActorId, IReadOnlyList<StableId> EquippedItems, int TotalMassGrams,
    int MobilityModifierPermille, int NoisePermille, IReadOnlySet<string> SocialAppearanceTags);

public sealed class EquipmentCapabilityCompiler
{
    public EquipmentCapabilitySnapshot Compile(StableId actorId, ItemWorldState world, ItemDefinitionCatalog catalog, IReadOnlyDictionary<string, ArmorItemDefinition> armorDefinitions)
    {
        EquipmentLoadoutState loadout = world.GetLoadout(actorId); var items = new List<StableId>(); var appearance = new SortedSet<string>(StringComparer.Ordinal); int mass = 0; int mobility = 1000; int noise = 0;
        foreach ((EquipmentSlot _, StableId itemId) in loadout.Slots)
        {
            ItemInstanceState item = world.GetItem(itemId); ItemDefinition definition = catalog.Get(item.DefinitionId); items.Add(itemId); mass = checked(mass + definition.MassGrams);
            if (armorDefinitions.TryGetValue(item.DefinitionId, out ArmorItemDefinition? armor)) { mobility = Math.Max(400, mobility - armor.MobilityPenaltyPermille * item.Condition.OverallPermille / 1000); noise = Math.Clamp(noise + armor.NoisePermille, 0, 1000); foreach (string tag in armor.SocialAppearanceTags) appearance.Add(tag); }
        }
        return new EquipmentCapabilitySnapshot(actorId, items.Order().ToArray(), mass, mobility, noise, appearance);
    }
}
