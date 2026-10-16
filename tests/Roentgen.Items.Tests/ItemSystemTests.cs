using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Items;

namespace Roentgen.Items.Tests;

public sealed class ItemSystemTests
{
    private readonly Phase13ContentBundle content = Phase13Content.Create();

    [Fact]
    public void Generated_weapon_blueprints_are_deterministic()
    {
        WeaponFamilyDefinition family = content.WeaponFamilies["weapon.revolver"];
        var generator = new ItemBlueprintGenerator();
        GeneratedItemBlueprint first = generator.GenerateWeapon(new CampaignSeed(42), family, content.WeaponComponents.Values.ToArray());
        GeneratedItemBlueprint second = generator.GenerateWeapon(new CampaignSeed(42), family, content.WeaponComponents.Values.ToArray());
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Components, second.Components);
    }

    [Fact]
    public void Inventory_transfer_is_atomic_and_revision_checked()
    {
        var world = new ItemWorldState(); StableId player = new(1, 1); StableId container = new(2, 2); world.EnsureInventory(player); world.EnsureInventory(container);
        StableId itemId = new(3, 3); var item = new ItemInstanceState(itemId, "material.scrap-steel", null, ItemLocation.InInventory(container), 5); world.AddItem(item); world.GetInventory(container).Add(itemId);
        var transaction = new ItemTransaction(new(4, 4), 1, [new TransferItemOperation(itemId, ItemLocation.InInventory(player))], [new ItemRevisionRequirement(itemId, item.Revision)]);
        ItemTransactionResult result = new ItemTransactionService(content.Items).Execute(world, transaction);
        Assert.Equal(ItemTransactionStatus.Committed, result.Status);
        Assert.Contains(itemId, world.GetInventory(player).ItemIds);
        Assert.DoesNotContain(itemId, world.GetInventory(container).ItemIds);
    }

    [Fact]
    public void Failed_multi_operation_transaction_rolls_back_all_item_changes()
    {
        var world = new ItemWorldState(); StableId owner = new(10, 10); world.EnsureInventory(owner); StableId itemId = new(11, 11);
        var item = new ItemInstanceState(itemId, "material.scrap-steel", null, ItemLocation.InInventory(owner), 2); world.AddItem(item); world.GetInventory(owner).Add(itemId);
        StateChecksum before = world.ComputeChecksum();
        var transaction = new ItemTransaction(new(12, 12), 1, [new ConsumeItemOperation(itemId, 2), new ConsumeItemOperation(itemId, 2)], []);
        ItemTransactionResult result = new ItemTransactionService(content.Items).Execute(world, transaction);
        Assert.Equal(ItemTransactionStatus.Rejected, result.Status);
        Assert.Equal(before, world.ComputeChecksum());
        Assert.Equal(2, world.GetItem(itemId).Quantity);
    }

    [Fact]
    public void Stack_split_and_merge_conserve_quantity()
    {
        var world = new ItemWorldState(); StableId owner = new(1, 2); world.EnsureInventory(owner); StableId sourceId = new(3, 4); StableId childId = new(5, 6);
        var source = new ItemInstanceState(sourceId, "ammo.light-cartridge", null, ItemLocation.InInventory(owner), 20); world.AddItem(source); world.GetInventory(owner).Add(sourceId);
        var service = new ItemTransactionService(content.Items);
        Assert.Equal(ItemTransactionStatus.Committed, service.Execute(world, new ItemTransaction(new(7, 8), 1, [new SplitStackOperation(sourceId, childId, 7)], [])).Status);
        Assert.Equal(13, world.GetItem(sourceId).Quantity); Assert.Equal(7, world.GetItem(childId).Quantity);
        Assert.Equal(ItemTransactionStatus.Committed, service.Execute(world, new ItemTransaction(new(9, 10), 2, [new MergeStackOperation(childId, sourceId)], [])).Status);
        Assert.Equal(20, world.GetItem(sourceId).Quantity); Assert.False(world.TryGetItem(childId, out _));
    }

    [Fact]
    public void Weapon_compiler_requires_and_uses_real_components()
    {
        var world = new ItemWorldState(); StableId owner = new(1, 3); world.EnsureInventory(owner); var factory = new ItemFactory(content);
        ItemInstanceState weapon = factory.CreateGeneratedWeapon(world, new(2, 3), new CampaignSeed(7), "weapon.hunting-rifle", ItemLocation.InInventory(owner));
        WeaponCapabilitySnapshot snapshot = new WeaponAssemblyCompiler().Compile(weapon, content.WeaponFamilies[weapon.DefinitionId], content.WeaponComponents, world);
        Assert.True(snapshot.MagazineCapacity > 0); Assert.True(snapshot.MaximumRangeMillimeters > 0); Assert.Equal(content.WeaponFamilies[weapon.DefinitionId].RequiredSlots.Count, weapon.InstalledComponents.Count);
    }

    [Fact]
    public void Ammunition_loading_consumes_inventory_stack()
    {
        var world = new ItemWorldState(); StableId owner = new(1, 4); world.EnsureInventory(owner); var factory = new ItemFactory(content);
        ItemInstanceState weapon = factory.CreateGeneratedWeapon(world, new(2, 4), new CampaignSeed(8), "weapon.revolver", ItemLocation.InInventory(owner));
        ItemInstanceState ammo = factory.CreateSimple(new(3, 4), "ammo.light-cartridge", ItemLocation.InInventory(owner), 12); world.AddItem(ammo); world.GetInventory(owner).Add(ammo.Id);
        AmmunitionTransferResult result = new AmmunitionService(content.Items, content.Ammunition).Load(world, weapon.Id, ammo.Id, 6, new(4, 4));
        Assert.True(result.Success); Assert.Equal(6, weapon.AmmunitionLoad?.LoadedCount); Assert.Equal(6, world.GetItem(ammo.Id).Quantity);
    }

    [Fact]
    public void Armor_layers_are_stably_ordered()
    {
        var world = new ItemWorldState(); StableId actor = new(1, 5); world.EnsureInventory(actor); world.EnsureLoadout(actor); var factory = new ItemFactory(content);
        ItemInstanceState clothing = factory.CreateSimple(new(2, 5), "armor.field-clothing", ItemLocation.InInventory(actor)); world.AddItem(clothing); world.GetInventory(actor).Add(clothing.Id);
        ItemInstanceState torso = factory.CreateSimple(new(3, 5), "armor.field-torso", ItemLocation.InInventory(actor)); world.AddItem(torso); world.GetInventory(actor).Add(torso.Id);
        var tx = new ItemTransactionService(content.Items); tx.Execute(world, new ItemTransaction(new(4,5),1,[new EquipItemOperation(actor,clothing.Id,EquipmentSlot.Clothing),new EquipItemOperation(actor,torso.Id,EquipmentSlot.Torso)],[]));
        ArmorCapabilitySnapshot snapshot = new ArmorCapabilityCompiler().Compile(actor, world, content.Armor);
        Assert.Equal(ItemArmorLayer.PrimaryProtection, snapshot.Layers[0].Layer); Assert.Equal(ItemArmorLayer.Clothing, snapshot.Layers[1].Layer);
    }

    [Fact]
    public void Loot_is_order_independent_and_save_safe()
    {
        LootProfile profile = content.LootProfiles["clinic-storage"]; StableId container = new(11, 12); var generator = new LootGenerator();
        GeneratedLoot first = generator.Generate(new CampaignSeed(100), container, profile, content.Items); GeneratedLoot second = generator.Generate(new CampaignSeed(100), container, profile, content.Items);
        Assert.Equal(first.Fingerprint, second.Fingerprint); Assert.Equal(first.TotalValueMinorUnits, second.TotalValueMinorUnits);
    }

    [Fact]
    public void Economy_never_creates_negative_stock()
    {
        SettlementEconomyState economy = Phase13Content.CreateEconomyFixtures()[0]; for (int day = 0; day < 20_000; day++) economy.AdvanceDay(); Assert.All(economy.Stocks.Values, stock => Assert.True(stock.QuantityMilliunits >= 0));
    }

    [Fact]
    public void Buy_price_exceeds_immediate_sell_price()
    {
        SettlementEconomyState economy = Phase13Content.CreateEconomyFixtures()[0]; ItemDefinition definition = content.Items.Get("consumable.trauma-dressing"); var item = new ItemInstanceState(new(20, 20), definition.Id, null, ItemLocation.InInventory(new(21, 21)), 1); var prices = new EconomyPriceCalculator();
        int buy = prices.Calculate(definition, item, economy, EconomyCommodity.Medicine, true, 1000, 1000).FinalPriceMinorUnits; int sell = prices.Calculate(definition, item, economy, EconomyCommodity.Medicine, false, 1000, 1000).FinalPriceMinorUnits; Assert.True(buy > sell);
    }

    [Fact]
    public void Phase13_content_meets_representative_budgets()
    {
        Assert.Equal(6, content.WeaponFamilies.Count); Assert.True(content.WeaponComponents.Count >= 24); Assert.Equal(8, content.Ammunition.Count); Assert.True(content.Consumables.Count >= 10); Assert.Equal(30, content.Recipes.Count); Assert.Equal(12, content.LootProfiles.Count); Assert.Equal(4, content.Merchants.Count);
    }
}
