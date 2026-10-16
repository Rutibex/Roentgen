using Roentgen.Contracts;
using Roentgen.Items;

namespace Roentgen.UI;

public enum InventoryCategory { All, Weapons, Armor, Aid, Ammo, Tools, Materials, Quest, Misc }
public enum InventorySortMode { Name, Kind, Mass, Value, Condition }

// Phase 8–10 player-facing browser state. These enums deliberately live with the UI projection
// rather than the authoritative RPG/item state so changing a filter can never alter simulation.
public enum EquipmentViewMode { Slots, CompatibleItems }
public enum SkillCategory { All, Combat, Physical, Survival, Technical, ScientificMedical, Social }
public enum PerkFilter { All, Available, Owned, Locked }
public enum CraftingCategory { All, Favorites, Weapons, Ammunition, Medicine, Tools, Survival, Components, Upgrades, Special }
public enum MerchantPane { MerchantStock, PlayerInventory }

public sealed record EquipmentSlotUiModel(EquipmentSlot Slot, string Label, StableId EquippedItemId, string EquippedItemName, bool HasCompatibleItems, bool CanRepair);
public sealed record EquipmentCandidateUiModel(StableId ItemId, string Name, ItemKind Kind, int ConditionPermille, int MassGrams, int ValueMinorUnits);
public sealed record EquipmentComparisonModel(string Title, IReadOnlyList<string> Lines);
public sealed record CraftingIngredientUiModel(string Label, int Have, int Need, bool Satisfied);
public sealed record CraftingRecipeUiModel(
    string RecipeId, string DisplayName, CraftingCategory Category, CraftingOperationKind Operation, CraftingStationKind Station,
    SkillId Skill, int EffectiveSkill, int Difficulty, IReadOnlyList<CraftingIngredientUiModel> Ingredients,
    IReadOnlyList<string> Outputs, int MaximumCraftCount, bool MaterialsSatisfied, string RiskLabel);

public sealed record ItemRowModel(StableId ItemId, string Name, ItemKind Kind, int Quantity, int MassGrams, int ValueMinorUnits, int ConditionPermille, string Location, IReadOnlyList<string> Tags);
public sealed record InventoryScreenModel(StableId OwnerId, IReadOnlyList<ItemRowModel> Items, int TotalMassGrams, int MaximumMassGrams, int TotalBulkMilliunits, int MaximumBulkMilliunits, IReadOnlyDictionary<EquipmentSlot, StableId> Equipment);
public sealed record MerchantScreenModel(StableId MerchantId, StableId BuyerId, IReadOnlyList<ItemRowModel> MerchantItems, IReadOnlyList<ItemRowModel> BuyerItems, int TaxPermille, int MerchantRiskPermille);
public sealed record MerchantTradePreviewUiModel(
    StableId ItemId, string Name, bool MerchantSelling, int QuantityAvailable, int UnitPriceMinorUnits,
    int MaximumAffordableQuantity, int SuggestedQuantity, int TotalPriceMinorUnits,
    int BaseValueMinorUnits, int ConditionPermille, int ScarcityPermille, int TaxPermille,
    int RiskPermille, int RelationshipPermille, int CommercePermille, string CommerceLabel);
public sealed record CraftingScreenModel(IReadOnlyList<string> KnownRecipeIds, IReadOnlyList<CraftingRecipeDefinition> AvailableRecipes, StableId? SelectedStationId, string Status);
public sealed record ItemInspectionModel(StableId ItemId, string Title, IReadOnlyList<string> Details);

public sealed class Phase13ItemScreenBuilder
{
    private readonly InventoryCapacityService capacity = new();
    public InventoryScreenModel BuildInventory(StableId ownerId, ItemWorldState world, ItemDefinitionCatalog catalog)
    {
        InventoryState inventory = world.GetInventory(ownerId); InventoryUsage usage = capacity.Calculate(inventory, world, catalog); EquipmentLoadoutState loadout = world.EnsureLoadout(ownerId);
        ItemRowModel[] rows = inventory.ItemIds.Select(world.GetItem).OrderBy(item => catalog.Get(item.DefinitionId).DisplayName, StringComparer.Ordinal).ThenBy(static item => item.Id).Select(item => Row(item, catalog)).ToArray();
        return new(ownerId, rows, usage.MassGrams, inventory.MaximumMassGrams, usage.BulkMilliunits, inventory.MaximumBulkMilliunits, loadout.Slots);
    }
    public MerchantScreenModel BuildMerchant(StableId merchantId, StableId buyerId, ItemWorldState world, ItemDefinitionCatalog catalog, SettlementEconomyState economy)
    {
        ItemRowModel[] merchant = world.GetInventory(merchantId).ItemIds.Select(world.GetItem).Where(item => IsTradable(item, catalog)).OrderBy(static value => value.Id).Select(item => Row(item, catalog)).ToArray(); ItemRowModel[] buyer = world.GetInventory(buyerId).ItemIds.Select(world.GetItem).Where(item => IsTradable(item, catalog)).OrderBy(static value => value.Id).Select(item => Row(item, catalog)).ToArray(); return new(merchantId, buyerId, merchant, buyer, economy.TaxPolicy.PurchaseTaxPermille, economy.MerchantRiskPermille);
    }
    private static bool IsTradable(ItemInstanceState item, ItemDefinitionCatalog catalog) { ItemKind kind = catalog.Get(item.DefinitionId).Kind; return kind != ItemKind.Currency && kind != ItemKind.Quest; }
    private static ItemRowModel Row(ItemInstanceState item, ItemDefinitionCatalog catalog) { ItemDefinition definition = catalog.Get(item.DefinitionId); return new(item.Id, item.CustomName ?? item.Blueprint?.DisplayIdentity ?? definition.DisplayName, definition.Kind, item.Quantity, definition.MassGrams * item.Quantity, definition.BaseValueMinorUnits * item.Quantity, item.Condition.OverallPermille, item.Location.Kind.ToString(), definition.Tags.Order(StringComparer.Ordinal).ToArray()); }
}

public enum Phase13OverlayKind { None = 0, Inventory = 1, Merchant = 2, Crafting = 3, Equipment = 4, Inspection = 5, Dialogue = 6, Journal = 7 }
public sealed record Phase13OverlayModel(Phase13OverlayKind Kind, string Title, IReadOnlyList<string> Lines, int SelectedIndex, string Footer, string Status);
