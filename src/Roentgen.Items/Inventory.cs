using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed class InventoryState : ICanonicalWritable
{
    private readonly SortedSet<StableId> itemIds = [];
    public InventoryState(StableId ownerId, int maximumMassGrams, int maximumBulkMilliunits) { if (ownerId.IsEmpty) throw new ArgumentException("Inventory owner cannot be empty.", nameof(ownerId)); OwnerId = ownerId; MaximumMassGrams = Math.Max(0, maximumMassGrams); MaximumBulkMilliunits = Math.Max(0, maximumBulkMilliunits); }
    public StableId OwnerId { get; }
    public int MaximumMassGrams { get; private set; }
    public int MaximumBulkMilliunits { get; private set; }
    public IReadOnlyCollection<StableId> ItemIds => itemIds;
    public ulong Revision { get; private set; }
    public bool Add(StableId itemId) { bool changed = itemIds.Add(itemId); if (changed) Revision++; return changed; }
    public bool Remove(StableId itemId) { bool changed = itemIds.Remove(itemId); if (changed) Revision++; return changed; }
    public void SetCapacity(int maximumMassGrams, int maximumBulkMilliunits) { MaximumMassGrams = Math.Max(0, maximumMassGrams); MaximumBulkMilliunits = Math.Max(0, maximumBulkMilliunits); Revision++; }
    internal InventoryState Clone()
    {
        var copy = new InventoryState(OwnerId, MaximumMassGrams, MaximumBulkMilliunits);
        foreach (StableId id in itemIds) copy.itemIds.Add(id);
        copy.Revision = Revision;
        return copy;
    }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(OwnerId); writer.WriteInt32(MaximumMassGrams); writer.WriteInt32(MaximumBulkMilliunits); writer.WriteCount(itemIds.Count); foreach (StableId id in itemIds) writer.WriteStableId(id); writer.WriteUInt64(Revision); }
    public static InventoryState ReadCanonical(CanonicalReader reader)
    {
        StableId owner = reader.ReadStableId(); int mass = reader.ReadInt32(); int bulk = reader.ReadInt32(); var result = new InventoryState(owner, mass, bulk); int count = reader.ReadCount(); for (int i = 0; i < count; i++) result.itemIds.Add(reader.ReadStableId()); result.Revision = reader.ReadUInt64(); return result;
    }
}

public sealed class EquipmentLoadoutState : ICanonicalWritable
{
    private readonly SortedDictionary<EquipmentSlot, StableId> slots = [];
    public EquipmentLoadoutState(StableId actorId) { if (actorId.IsEmpty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId)); ActorId = actorId; }
    public StableId ActorId { get; }
    public IReadOnlyDictionary<EquipmentSlot, StableId> Slots => slots;
    public ulong Revision { get; private set; }
    public bool TryGet(EquipmentSlot slot, out StableId itemId) => slots.TryGetValue(slot, out itemId);
    public void Equip(EquipmentSlot slot, StableId itemId) { if (itemId.IsEmpty) throw new ArgumentException("Item ID cannot be empty.", nameof(itemId)); slots[slot] = itemId; Revision++; }
    public bool Unequip(EquipmentSlot slot) { bool changed = slots.Remove(slot); if (changed) Revision++; return changed; }
    internal EquipmentLoadoutState Clone()
    {
        var copy = new EquipmentLoadoutState(ActorId);
        foreach ((EquipmentSlot slot, StableId itemId) in slots) copy.slots.Add(slot, itemId);
        copy.Revision = Revision;
        return copy;
    }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(ActorId); writer.WriteCount(slots.Count); foreach ((EquipmentSlot slot, StableId itemId) in slots) { writer.WriteInt32((int)slot); writer.WriteStableId(itemId); } writer.WriteUInt64(Revision); }
    public static EquipmentLoadoutState ReadCanonical(CanonicalReader reader)
    {
        StableId actor = reader.ReadStableId(); var result = new EquipmentLoadoutState(actor); int count = reader.ReadCount(); for (int i = 0; i < count; i++) result.slots.Add((EquipmentSlot)reader.ReadInt32(), reader.ReadStableId()); result.Revision = reader.ReadUInt64(); return result;
    }
}

public sealed record InventoryUsage(int MassGrams, int BulkMilliunits, bool ExceedsMass, bool ExceedsBulk);

public sealed class InventoryCapacityService
{
    public InventoryUsage Calculate(InventoryState inventory, ItemWorldState world, ItemDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(inventory); ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(catalog);
        var visited = new HashSet<StableId>(); int mass = 0; int bulk = 0;
        foreach (StableId id in inventory.ItemIds) Accumulate(id, world, catalog, visited, ref mass, ref bulk, 0);
        return new InventoryUsage(mass, bulk, mass > inventory.MaximumMassGrams, bulk > inventory.MaximumBulkMilliunits);
    }
    private static void Accumulate(StableId id, ItemWorldState world, ItemDefinitionCatalog catalog, HashSet<StableId> visited, ref int mass, ref int bulk, int depth)
    {
        if (depth > 8) throw new InvalidOperationException("Container nesting exceeds the supported depth."); if (!visited.Add(id)) throw new InvalidOperationException("Container cycle detected.");
        ItemInstanceState item = world.GetItem(id); ItemDefinition definition = catalog.Get(item.DefinitionId); mass = checked(mass + definition.MassGrams * item.Quantity); bulk = checked(bulk + definition.BulkMilliunits * item.Quantity);
        foreach (ItemInstanceState child in world.Items.Where(value => value.Location.Kind == ItemLocationKind.Container && value.Location.HolderId == id)) Accumulate(child.Id, world, catalog, visited, ref mass, ref bulk, depth + 1);
        visited.Remove(id);
    }
}

public static class StackRules
{
    public static string Signature(ItemInstanceState item, ItemDefinition definition)
    {
        var writer = new CanonicalWriter(); writer.WriteString(item.DefinitionId); writer.WriteString(item.Blueprint?.Fingerprint.ToString()); writer.WriteInt32(item.Condition.OverallPermille / 50); writer.WriteInt32(item.Condition.ContaminationPermille / 50); writer.WriteCount(item.Charges.Count); foreach ((string key, int value) in item.Charges) { writer.WriteString(key); writer.WriteInt32(value); } return writer.ComputeChecksum().ToString();
    }
    public static bool CanMerge(ItemInstanceState left, ItemInstanceState right, ItemDefinition definition) => definition.StackPolicy != StackPolicy.Never && StringComparer.Ordinal.Equals(Signature(left, definition), Signature(right, definition)) && left.Quantity + right.Quantity <= definition.MaximumStackSize;
}
