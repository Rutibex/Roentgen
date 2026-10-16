using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

internal sealed record ItemWorldCheckpoint(
    IReadOnlyDictionary<StableId, ItemInstanceState> Items,
    IReadOnlyDictionary<StableId, InventoryState> Inventories,
    IReadOnlyDictionary<StableId, EquipmentLoadoutState> Loadouts,
    IReadOnlyDictionary<StableId, ItemReservation> Reservations,
    ulong Revision);

public sealed class ItemWorldState : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, ItemInstanceState> items = [];
    private readonly SortedDictionary<StableId, InventoryState> inventories = [];
    private readonly SortedDictionary<StableId, EquipmentLoadoutState> loadouts = [];
    private readonly SortedDictionary<StableId, ItemReservation> reservations = [];
    private readonly SortedDictionary<StableId, SettlementEconomyState> economies = [];
    public IReadOnlyCollection<ItemInstanceState> Items => items.Values;
    public IReadOnlyCollection<InventoryState> Inventories => inventories.Values;
    public IReadOnlyCollection<EquipmentLoadoutState> Loadouts => loadouts.Values;
    public IReadOnlyCollection<ItemReservation> Reservations => reservations.Values;
    public IReadOnlyCollection<SettlementEconomyState> Economies => economies.Values;
    public ulong Revision { get; private set; }
    public void AddItem(ItemInstanceState item) { ArgumentNullException.ThrowIfNull(item); if (!items.TryAdd(item.Id, item)) throw new InvalidOperationException($"Duplicate item ID '{item.Id}'."); Revision++; }
    public bool RemoveItem(StableId id) => DestroyItem(id);
    public bool DestroyItem(StableId id)
    {
        if (!items.Remove(id)) return false;
        foreach (InventoryState inventory in inventories.Values) inventory.Remove(id);
        foreach (EquipmentLoadoutState loadout in loadouts.Values)
            foreach ((EquipmentSlot slot, StableId itemId) in loadout.Slots.ToArray())
                if (itemId == id) loadout.Unequip(slot);
        foreach (StableId reservationId in reservations.Values.Where(value => value.ItemId == id).Select(static value => value.ReservationId).ToArray()) reservations.Remove(reservationId);
        Revision++;
        return true;
    }
    public ItemInstanceState GetItem(StableId id) => items.TryGetValue(id, out ItemInstanceState? item) ? item : throw new KeyNotFoundException($"Unknown item '{id}'.");
    public bool TryGetItem(StableId id, out ItemInstanceState? item) => items.TryGetValue(id, out item);
    public InventoryState EnsureInventory(StableId ownerId, int maximumMassGrams = int.MaxValue, int maximumBulkMilliunits = int.MaxValue) { if (!inventories.TryGetValue(ownerId, out InventoryState? inventory)) { inventory = new InventoryState(ownerId, maximumMassGrams, maximumBulkMilliunits); inventories.Add(ownerId, inventory); Revision++; } return inventory; }
    public InventoryState GetInventory(StableId ownerId) => inventories.TryGetValue(ownerId, out InventoryState? inventory) ? inventory : throw new KeyNotFoundException($"Unknown inventory '{ownerId}'.");
    public bool TryGetInventory(StableId ownerId, out InventoryState? inventory) => inventories.TryGetValue(ownerId, out inventory);
    public EquipmentLoadoutState EnsureLoadout(StableId actorId) { if (!loadouts.TryGetValue(actorId, out EquipmentLoadoutState? loadout)) { loadout = new EquipmentLoadoutState(actorId); loadouts.Add(actorId, loadout); Revision++; } return loadout; }
    public EquipmentLoadoutState GetLoadout(StableId actorId) => loadouts.TryGetValue(actorId, out EquipmentLoadoutState? loadout) ? loadout : throw new KeyNotFoundException($"Unknown loadout '{actorId}'.");
    public void AddReservation(ItemReservation reservation) { if (!reservations.TryAdd(reservation.ReservationId, reservation)) throw new InvalidOperationException($"Duplicate reservation '{reservation.ReservationId}'."); Revision++; }
    public bool ReleaseReservation(StableId id) { bool removed = reservations.Remove(id); if (removed) Revision++; return removed; }
    public int ReservedQuantity(StableId itemId, StableId? excludingReservation = null) => reservations.Values.Where(value => value.ItemId == itemId && (!excludingReservation.HasValue || value.ReservationId != excludingReservation.Value)).Sum(static value => value.Quantity);
    public void AddEconomy(SettlementEconomyState state) { if (!economies.TryAdd(state.SettlementId, state)) throw new InvalidOperationException($"Duplicate economy '{state.SettlementId}'."); Revision++; }
    public void UpsertEconomy(SettlementEconomyState state) { ArgumentNullException.ThrowIfNull(state); economies[state.SettlementId] = state; Revision++; }
    public SettlementEconomyState GetEconomy(StableId id) => economies.TryGetValue(id, out SettlementEconomyState? value) ? value : throw new KeyNotFoundException($"Unknown settlement economy '{id}'.");
    internal ItemWorldCheckpoint CreateItemCheckpoint() => new(
        items.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone()),
        inventories.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone()),
        loadouts.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone()),
        reservations.ToDictionary(static pair => pair.Key, static pair => pair.Value),
        Revision);
    internal void RestoreItemCheckpoint(ItemWorldCheckpoint checkpoint)
    {
        items.Clear(); foreach ((StableId id, ItemInstanceState item) in checkpoint.Items) items.Add(id, item);
        inventories.Clear(); foreach ((StableId id, InventoryState inventory) in checkpoint.Inventories) inventories.Add(id, inventory);
        loadouts.Clear(); foreach ((StableId id, EquipmentLoadoutState loadout) in checkpoint.Loadouts) loadouts.Add(id, loadout);
        reservations.Clear(); foreach ((StableId id, ItemReservation reservation) in checkpoint.Reservations) reservations.Add(id, reservation);
        Revision = checkpoint.Revision;
    }
    public void RestoreFrom(ReadOnlyMemory<byte> payload)
    {
        ItemWorldState source = ReadCanonical(payload);
        items.Clear(); foreach ((StableId id, ItemInstanceState value) in source.items) items.Add(id, value);
        inventories.Clear(); foreach ((StableId id, InventoryState value) in source.inventories) inventories.Add(id, value);
        loadouts.Clear(); foreach ((StableId id, EquipmentLoadoutState value) in source.loadouts) loadouts.Add(id, value);
        reservations.Clear(); foreach ((StableId id, ItemReservation value) in source.reservations) reservations.Add(id, value);
        economies.Clear(); foreach ((StableId id, SettlementEconomyState value) in source.economies) economies.Add(id, value);
        Revision = source.Revision;
    }
    public StateChecksum ComputeChecksum() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ComputeChecksum(); }
    public byte[] ToCanonicalPayload() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ToArray(); }
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteCount(items.Count); foreach (ItemInstanceState item in items.Values) item.WriteCanonical(writer); writer.WriteCount(inventories.Count); foreach (InventoryState value in inventories.Values) value.WriteCanonical(writer); writer.WriteCount(loadouts.Count); foreach (EquipmentLoadoutState value in loadouts.Values) value.WriteCanonical(writer); writer.WriteCount(reservations.Count); foreach (ItemReservation value in reservations.Values) value.WriteCanonical(writer); writer.WriteCount(economies.Count); foreach (SettlementEconomyState value in economies.Values) value.WriteCanonical(writer); writer.WriteUInt64(Revision);
    }
    public static ItemWorldState ReadCanonical(ReadOnlyMemory<byte> payload)
    {
        var reader = new CanonicalReader(payload); var result = new ItemWorldState();
        int itemCount = reader.ReadCount(); for (int i = 0; i < itemCount; i++) { ItemInstanceState value = ItemInstanceState.ReadCanonical(reader); result.items.Add(value.Id, value); }
        int inventoryCount = reader.ReadCount(); for (int i = 0; i < inventoryCount; i++) { InventoryState value = InventoryState.ReadCanonical(reader); result.inventories.Add(value.OwnerId, value); }
        int loadoutCount = reader.ReadCount(); for (int i = 0; i < loadoutCount; i++) { EquipmentLoadoutState value = EquipmentLoadoutState.ReadCanonical(reader); result.loadouts.Add(value.ActorId, value); }
        int reservationCount = reader.ReadCount(); for (int i = 0; i < reservationCount; i++) { ItemReservation value = ItemReservation.ReadCanonical(reader); result.reservations.Add(value.ReservationId, value); }
        int economyCount = reader.ReadCount(); for (int i = 0; i < economyCount; i++) { SettlementEconomyState value = SettlementEconomyState.ReadCanonical(reader); result.economies.Add(value.SettlementId, value); }
        result.Revision = reader.ReadUInt64(); reader.EnsureComplete(); return result;
    }
}
