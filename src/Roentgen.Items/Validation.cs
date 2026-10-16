using Roentgen.Contracts;

namespace Roentgen.Items;

public sealed class ItemWorldValidator
{
    public ItemValidationReport Validate(ItemWorldState world, ItemDefinitionCatalog catalog)
    {
        var issues = new List<ItemValidationIssue>();
        foreach (ItemInstanceState item in world.Items)
        {
            if (!catalog.TryGet(item.DefinitionId, out ItemDefinition? definition) || definition is null) issues.Add(new("missing-definition", $"Item {item.Id} references missing definition {item.DefinitionId}.", true));
            if (item.Quantity <= 0) issues.Add(new("invalid-quantity", $"Item {item.Id} has nonpositive quantity.", true));
            if (item.Location.Kind == ItemLocationKind.Inventory && !world.Inventories.Any(value => value.OwnerId == item.Location.HolderId && value.ItemIds.Contains(item.Id))) issues.Add(new("inventory-index-mismatch", $"Item {item.Id} is absent from its inventory index.", true));
            if (definition is not null && definition.StackPolicy == StackPolicy.Never && item.Quantity != 1) issues.Add(new("nonstackable-quantity", $"Non-stackable item {item.Id} has quantity {item.Quantity}.", true));
        }
        foreach (InventoryState inventory in world.Inventories) foreach (StableId id in inventory.ItemIds) if (!world.TryGetItem(id, out ItemInstanceState? item) || item is null || item.Location.Kind != ItemLocationKind.Inventory || item.Location.HolderId != inventory.OwnerId) issues.Add(new("orphan-inventory-index", $"Inventory {inventory.OwnerId} contains an invalid item index {id}.", true));
        foreach (ItemReservation reservation in world.Reservations) if (!world.TryGetItem(reservation.ItemId, out ItemInstanceState? item) || item is null || reservation.Quantity <= 0 || reservation.Quantity > item.Quantity) issues.Add(new("invalid-reservation", $"Reservation {reservation.ReservationId} is invalid.", true));
        return new(!issues.Any(static value => value.IsCritical), issues);
    }
}

public sealed class WeaponAssemblyValidator
{
    public ItemValidationReport Validate(ItemInstanceState weapon, WeaponFamilyDefinition family, IReadOnlyDictionary<string, WeaponComponentDefinition> components, ItemWorldState world)
    {
        var issues = new List<ItemValidationIssue>(); foreach (WeaponComponentSlot slot in family.RequiredSlots) if (!weapon.InstalledComponents.ContainsKey(slot)) issues.Add(new("missing-component", $"Weapon {weapon.Id} is missing {slot}.", true));
        foreach ((WeaponComponentSlot slot, StableId componentId) in weapon.InstalledComponents) { if (!world.TryGetItem(componentId, out ItemInstanceState? component) || component is null || !components.TryGetValue(component.DefinitionId, out WeaponComponentDefinition? definition)) issues.Add(new("unknown-component", $"Weapon {weapon.Id} references unknown component {componentId}.", true)); else if (definition.Slot != slot) issues.Add(new("wrong-slot", $"Component {componentId} is in the wrong slot.", true)); }
        return new(!issues.Any(static value => value.IsCritical), issues);
    }
}
