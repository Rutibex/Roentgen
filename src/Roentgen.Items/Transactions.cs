using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public abstract record ItemTransactionOperation(ItemTransactionOperationKind Kind);
public sealed record TransferItemOperation(StableId ItemId, ItemLocation Destination, int Quantity = 0) : ItemTransactionOperation(ItemTransactionOperationKind.Transfer);
public sealed record SplitStackOperation(StableId ItemId, StableId NewItemId, int Quantity) : ItemTransactionOperation(ItemTransactionOperationKind.Split);
public sealed record MergeStackOperation(StableId SourceItemId, StableId DestinationItemId) : ItemTransactionOperation(ItemTransactionOperationKind.Merge);
public sealed record EquipItemOperation(StableId ActorId, StableId ItemId, EquipmentSlot Slot) : ItemTransactionOperation(ItemTransactionOperationKind.Equip);
public sealed record UnequipItemOperation(StableId ActorId, EquipmentSlot Slot) : ItemTransactionOperation(ItemTransactionOperationKind.Unequip);
public sealed record ConsumeItemOperation(StableId ItemId, int Quantity) : ItemTransactionOperation(ItemTransactionOperationKind.Consume);
public sealed record DamageConditionOperation(StableId ItemId, int Structural, int Calibration, int Cleanliness, int Contamination) : ItemTransactionOperation(ItemTransactionOperationKind.DamageCondition);
public sealed record RepairConditionOperation(StableId ItemId, int Structural, int Calibration, int Cleanliness, int ContaminationReduction, int MaximumPermille) : ItemTransactionOperation(ItemTransactionOperationKind.RepairCondition);
public sealed record ReserveItemOperation(ItemReservation Reservation) : ItemTransactionOperation(ItemTransactionOperationKind.Reserve);
public sealed record ReleaseReservationOperation(StableId ReservationId) : ItemTransactionOperation(ItemTransactionOperationKind.ReleaseReservation);

public sealed record ItemRevisionRequirement(StableId ItemId, ulong ExpectedRevision);
public sealed record ItemTransaction(StableId TransactionId, ulong SimulationTick, IReadOnlyList<ItemTransactionOperation> Operations, IReadOnlyList<ItemRevisionRequirement> Preconditions);
public sealed record ItemTransactionResult(ItemTransactionStatus Status, string Message, IReadOnlyList<StableId> AffectedItems, StateChecksum? Checksum = null);

public sealed class ItemTransactionService
{
    private readonly ItemDefinitionCatalog catalog;
    public ItemTransactionService(ItemDefinitionCatalog catalog) => this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    public ItemTransactionResult Execute(ItemWorldState world, ItemTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(transaction);
        string? error = Validate(world, transaction);
        if (error is not null) return new ItemTransactionResult(ItemTransactionStatus.Rejected, error, []);
        ItemWorldCheckpoint checkpoint = world.CreateItemCheckpoint();
        var affected = new SortedSet<StableId>();
        try
        {
            foreach (ItemTransactionOperation operation in transaction.Operations) Apply(world, operation, affected);
            return new ItemTransactionResult(ItemTransactionStatus.Committed, "committed", affected.ToArray(), world.ComputeChecksum());
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException)
        {
            world.RestoreItemCheckpoint(checkpoint);
            return new ItemTransactionResult(ItemTransactionStatus.Rejected, $"transaction rolled back: {exception.Message}", []);
        }
    }
    private string? Validate(ItemWorldState world, ItemTransaction transaction)
    {
        if (transaction.TransactionId.IsEmpty) return "transaction ID is empty";
        foreach (ItemRevisionRequirement requirement in transaction.Preconditions) { if (!world.TryGetItem(requirement.ItemId, out ItemInstanceState? item) || item is null) return $"missing item {requirement.ItemId}"; if (item.Revision != requirement.ExpectedRevision) return $"stale item revision {requirement.ItemId}"; }
        var reservations = new Dictionary<StableId, int>();
        foreach (ItemReservation reservation in world.Reservations) reservations[reservation.ItemId] = reservations.GetValueOrDefault(reservation.ItemId) + reservation.Quantity;
        foreach (ItemTransactionOperation operation in transaction.Operations)
        {
            switch (operation)
            {
                case TransferItemOperation transfer:
                    if (!world.TryGetItem(transfer.ItemId, out ItemInstanceState? transferItem) || transferItem is null) return "transfer source missing";
                    int transferQuantity = transfer.Quantity <= 0 ? transferItem.Quantity : transfer.Quantity; if (transferQuantity <= 0 || transferQuantity > transferItem.Quantity - reservations.GetValueOrDefault(transfer.ItemId)) return "transfer quantity unavailable"; if (transferQuantity != transferItem.Quantity) return "partial transfer requires an explicit split";
                    break;
                case SplitStackOperation split:
                    if (!world.TryGetItem(split.ItemId, out ItemInstanceState? splitItem) || splitItem is null || world.TryGetItem(split.NewItemId, out _)) return "invalid stack split"; if (split.Quantity <= 0 || split.Quantity >= splitItem.Quantity - reservations.GetValueOrDefault(split.ItemId)) return "split quantity unavailable"; if (catalog.Get(splitItem.DefinitionId).StackPolicy == StackPolicy.Never) return "item cannot be split";
                    break;
                case MergeStackOperation merge:
                    if (merge.SourceItemId == merge.DestinationItemId) return "cannot merge a stack with itself";
                    if (!world.TryGetItem(merge.SourceItemId, out ItemInstanceState? source) || source is null || !world.TryGetItem(merge.DestinationItemId, out ItemInstanceState? destination) || destination is null) return "merge item missing"; ItemDefinition definition = catalog.Get(source.DefinitionId); if (!StringComparer.Ordinal.Equals(source.DefinitionId, destination.DefinitionId) || !StackRules.CanMerge(source, destination, definition)) return "stacks are incompatible";
                    break;
                case EquipItemOperation equip:
                    if (!world.TryGetItem(equip.ItemId, out ItemInstanceState? equipItem) || equipItem is null) return "equipment item missing"; if (!EquipmentRules.CanEquip(catalog.Get(equipItem.DefinitionId), equip.Slot)) return "item is incompatible with slot"; if (reservations.GetValueOrDefault(equip.ItemId) > 0) return "item is reserved";
                    break;
                case ConsumeItemOperation consume:
                    if (!world.TryGetItem(consume.ItemId, out ItemInstanceState? consumeItem) || consumeItem is null || consume.Quantity <= 0 || consume.Quantity > consumeItem.Quantity - reservations.GetValueOrDefault(consume.ItemId)) return "consume quantity unavailable";
                    break;
                case DamageConditionOperation damage when !world.TryGetItem(damage.ItemId, out _): return "condition item missing";
                case RepairConditionOperation repair when !world.TryGetItem(repair.ItemId, out _): return "repair item missing";
                case ReserveItemOperation reserve:
                    if (!world.TryGetItem(reserve.Reservation.ItemId, out ItemInstanceState? reserveItem) || reserveItem is null) return "reservation item missing"; if (reserve.Reservation.Quantity <= 0 || reserve.Reservation.Quantity > reserveItem.Quantity - reservations.GetValueOrDefault(reserve.Reservation.ItemId)) return "reservation quantity unavailable"; reservations[reserve.Reservation.ItemId] = reservations.GetValueOrDefault(reserve.Reservation.ItemId) + reserve.Reservation.Quantity;
                    break;
            }
        }
        return null;
    }
    private void Apply(ItemWorldState world, ItemTransactionOperation operation, SortedSet<StableId> affected)
    {
        switch (operation)
        {
            case TransferItemOperation transfer: ApplyTransfer(world, transfer, affected); break;
            case SplitStackOperation split: ApplySplit(world, split, affected); break;
            case MergeStackOperation merge: ItemInstanceState source = world.GetItem(merge.SourceItemId); ItemInstanceState destination = world.GetItem(merge.DestinationItemId); destination.SetQuantity(destination.Quantity + source.Quantity); world.RemoveItem(source.Id); affected.Add(source.Id); affected.Add(destination.Id); break;
            case EquipItemOperation equip: ApplyEquip(world, equip, affected); break;
            case UnequipItemOperation unequip: EquipmentLoadoutState loadout = world.GetLoadout(unequip.ActorId); if (loadout.TryGet(unequip.Slot, out StableId itemId)) { loadout.Unequip(unequip.Slot); ItemInstanceState item = world.GetItem(itemId); item.SetLocation(ItemLocation.InInventory(unequip.ActorId)); world.EnsureInventory(unequip.ActorId).Add(itemId); affected.Add(itemId); } break;
            case ConsumeItemOperation consume: ItemInstanceState consumed = world.GetItem(consume.ItemId); if (consume.Quantity == consumed.Quantity) world.RemoveItem(consumed.Id); else consumed.SetQuantity(consumed.Quantity - consume.Quantity); affected.Add(consumed.Id); break;
            case DamageConditionOperation damage: ItemInstanceState damaged = world.GetItem(damage.ItemId); damaged.Condition.Damage(damage.Structural, damage.Calibration, damage.Cleanliness, damage.Contamination); damaged.Touch(); affected.Add(damage.ItemId); break;
            case RepairConditionOperation repair: ItemInstanceState repaired = world.GetItem(repair.ItemId); repaired.Condition.Repair(repair.Structural, repair.Calibration, repair.Cleanliness, repair.ContaminationReduction, repair.MaximumPermille); repaired.Touch(); affected.Add(repair.ItemId); break;
            case ReserveItemOperation reserve: world.AddReservation(reserve.Reservation); affected.Add(reserve.Reservation.ItemId); break;
            case ReleaseReservationOperation release: world.ReleaseReservation(release.ReservationId); break;
        }
    }
    private static void ApplyTransfer(ItemWorldState world, TransferItemOperation operation, SortedSet<StableId> affected)
    {
        ItemInstanceState item = world.GetItem(operation.ItemId); if (operation.Quantity > 0 && operation.Quantity < item.Quantity) throw new InvalidOperationException("Partial transfer requires an explicit split operation.");
        RemoveFromIndex(world, item); item.SetLocation(operation.Destination); AddToIndex(world, item); affected.Add(item.Id);
    }
    private static void ApplySplit(ItemWorldState world, SplitStackOperation operation, SortedSet<StableId> affected)
    {
        ItemInstanceState source = world.GetItem(operation.ItemId); source.SetQuantity(source.Quantity - operation.Quantity); var child = new ItemInstanceState(operation.NewItemId, source.DefinitionId, source.Blueprint, source.Location, operation.Quantity, source.Condition.Clone()); world.AddItem(child); AddToIndex(world, child); affected.Add(source.Id); affected.Add(child.Id);
    }
    private static void ApplyEquip(ItemWorldState world, EquipItemOperation operation, SortedSet<StableId> affected)
    {
        EquipmentLoadoutState loadout = world.EnsureLoadout(operation.ActorId); if (loadout.TryGet(operation.Slot, out StableId previousId)) { ItemInstanceState previous = world.GetItem(previousId); previous.SetLocation(ItemLocation.InInventory(operation.ActorId)); world.EnsureInventory(operation.ActorId).Add(previousId); affected.Add(previousId); }
        ItemInstanceState item = world.GetItem(operation.ItemId); RemoveFromIndex(world, item); loadout.Equip(operation.Slot, item.Id); item.SetLocation(ItemLocation.Equipped(operation.ActorId, operation.Slot)); affected.Add(item.Id);
    }
    private static void RemoveFromIndex(ItemWorldState world, ItemInstanceState item) { if (item.Location.Kind == ItemLocationKind.Inventory && !item.Location.HolderId.IsEmpty) world.EnsureInventory(item.Location.HolderId).Remove(item.Id); }
    private static void AddToIndex(ItemWorldState world, ItemInstanceState item) { if (item.Location.Kind == ItemLocationKind.Inventory && !item.Location.HolderId.IsEmpty) world.EnsureInventory(item.Location.HolderId).Add(item.Id); }
}
