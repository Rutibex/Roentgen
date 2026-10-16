using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed class ItemFactory
{
    private readonly Phase13ContentBundle content;
    private readonly ItemBlueprintGenerator blueprints = new();
    public ItemFactory(Phase13ContentBundle content) => this.content = content ?? throw new ArgumentNullException(nameof(content));
    public ItemInstanceState CreateSimple(StableId id, string definitionId, ItemLocation location, int quantity = 1, int conditionPermille = 1000)
    {
        ItemDefinition definition = content.Items.Get(definitionId); if (quantity > definition.MaximumStackSize) throw new InvalidOperationException($"Quantity exceeds stack size for '{definitionId}'."); return new ItemInstanceState(id, definitionId, null, location, quantity, new ItemConditionState(conditionPermille, conditionPermille, conditionPermille));
    }
    public ItemInstanceState CreateGeneratedWeapon(ItemWorldState world, StableId weaponId, CampaignSeed seed, string familyId, ItemLocation location)
    {
        WeaponFamilyDefinition family = content.WeaponFamilies[familyId]; GeneratedItemBlueprint blueprint = blueprints.GenerateWeapon(seed, family, content.WeaponComponents.Values.ToArray()); var weapon = new ItemInstanceState(weaponId, familyId, blueprint, location, 1); world.AddItem(weapon);
        foreach (ComponentSelection selection in blueprint.Components) { StableId componentId = StableIdFactory.CreateChild(weaponId, "component", (int)selection.Slot); var component = new ItemInstanceState(componentId, selection.ComponentDefinitionId, null, ItemLocation.InContainer(weaponId), 1); world.AddItem(component); weapon.InstallComponent(selection.Slot, componentId); }
        WeaponCapabilitySnapshot capability = new WeaponAssemblyCompiler().Compile(weapon, family, content.WeaponComponents, world); foreach (string tag in capability.AmmunitionTags) weapon.AddRuntimeTag($"accepts:{tag}"); weapon.InitializeAmmunitionLoad(capability.MagazineCapacity); if (location.Kind == ItemLocationKind.Inventory) world.EnsureInventory(location.HolderId).Add(weapon.Id); return weapon;
    }
    public IReadOnlyList<ItemInstanceState> InstantiateStartingPackage(ItemWorldState world, StableId ownerId, CampaignSeed seed, IReadOnlyList<string> definitionIds)
    {
        var result = new List<ItemInstanceState>();
        int ordinal = 0;
        foreach (string requestedDefinitionId in definitionIds)
        {
            string definitionId = NormalizeStartingDefinition(requestedDefinitionId);
            StableId id = StableIdFactory.CreateChild(ownerId, "starting-item", ordinal++);
            ItemInstanceState item = content.WeaponFamilies.ContainsKey(definitionId)
                ? CreateGeneratedWeapon(world, id, new CampaignSeed(seed.Value + (ulong)ordinal), definitionId, ItemLocation.InInventory(ownerId))
                : CreateSimple(id, definitionId, ItemLocation.InInventory(ownerId));
            if (!world.TryGetItem(id, out _)) world.AddItem(item);
            world.EnsureInventory(ownerId).Add(id);
            result.Add(item);
        }
        return result;
    }

    private string NormalizeStartingDefinition(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);
        if (content.Items.TryGet(legacyId, out _) || content.WeaponFamilies.ContainsKey(legacyId)) return legacyId;
        return legacyId switch
        {
            "light-rifle" => "weapon.hunting-rifle",
            "compact-sidearm" or "concealed-pistol" => "weapon.self-loading-pistol",
            "cutting-bar" or "survey-hammer" => "weapon.improvised-melee",
            "breaching-charge" => "ammo.explosive-charge",
            "respirator" => "armor.field-respirator",
            "protective-hood" => "armor.field-head",
            "work-coat" or "formal-coat" => "armor.field-clothing",
            "medical-roll" or "medical-pouch" => "consumable.trauma-dressing",
            "antiseptic" => "material.medical-alcohol",
            "water-test-strip" or "chalk-set" => "material.diagnostic-film",
            "wire-spool" => "material.copper-wire",
            "multimeter" or "mechanic-tools" or "inspection-lamp" => "tool.field-kit",
            "diagnostic-lens" => "tool.medical-kit",
            "sample-case" => "container.field-pack",
            "fieldcraft-map" or "trade-ledger" or "obligation-ledger" => "document.clinic-repair-manual",
            _ => throw new KeyNotFoundException($"Starting equipment definition '{legacyId}' has no Phase 13 mapping.")
        };
    }
}
