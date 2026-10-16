using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed class ItemConditionState : ICanonicalWritable
{
    public ItemConditionState(int structuralPermille = 1000, int calibrationPermille = 1000, int cleanlinessPermille = 1000, int contaminationPermille = 0)
    {
        StructuralPermille = Clamp(structuralPermille); CalibrationPermille = Clamp(calibrationPermille); CleanlinessPermille = Clamp(cleanlinessPermille); ContaminationPermille = Clamp(contaminationPermille);
    }
    public int StructuralPermille { get; private set; }
    public int CalibrationPermille { get; private set; }
    public int CleanlinessPermille { get; private set; }
    public int ContaminationPermille { get; private set; }
    public ulong Revision { get; private set; }
    public int OverallPermille => Math.Min(StructuralPermille, Math.Min(CalibrationPermille, CleanlinessPermille));
    public void Damage(int structural, int calibration = 0, int cleanliness = 0, int contamination = 0) { StructuralPermille = Clamp(StructuralPermille - Math.Max(0, structural)); CalibrationPermille = Clamp(CalibrationPermille - Math.Max(0, calibration)); CleanlinessPermille = Clamp(CleanlinessPermille - Math.Max(0, cleanliness)); ContaminationPermille = Clamp(ContaminationPermille + Math.Max(0, contamination)); Revision++; }
    public void Repair(int structural, int calibration = 0, int cleanliness = 0, int contaminationReduction = 0, int maximumPermille = 1000) { maximumPermille = Clamp(maximumPermille); StructuralPermille = Math.Min(maximumPermille, StructuralPermille + Math.Max(0, structural)); CalibrationPermille = Math.Min(maximumPermille, CalibrationPermille + Math.Max(0, calibration)); CleanlinessPermille = Math.Min(maximumPermille, CleanlinessPermille + Math.Max(0, cleanliness)); ContaminationPermille = Math.Max(0, ContaminationPermille - Math.Max(0, contaminationReduction)); Revision++; }
    public ItemConditionState Clone() => new(StructuralPermille, CalibrationPermille, CleanlinessPermille, ContaminationPermille) { Revision = this.Revision };
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32(StructuralPermille); writer.WriteInt32(CalibrationPermille); writer.WriteInt32(CleanlinessPermille); writer.WriteInt32(ContaminationPermille); writer.WriteUInt64(Revision); }
    public static ItemConditionState ReadCanonical(CanonicalReader reader) => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()) { Revision = reader.ReadUInt64() };
    private static int Clamp(int value) => Math.Clamp(value, 0, 1000);
}

public sealed class AmmunitionLoadState : ICanonicalWritable
{
    public AmmunitionLoadState(int capacity) { if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity)); Capacity = capacity; }
    public int Capacity { get; }
    public string? AmmunitionDefinitionId { get; private set; }
    public int LoadedCount { get; private set; }
    public bool Chambered { get; private set; }
    public ulong Revision { get; private set; }
    public int Load(string ammunitionDefinitionId, int quantity) { ArgumentException.ThrowIfNullOrWhiteSpace(ammunitionDefinitionId); if (quantity <= 0) return 0; if (AmmunitionDefinitionId is not null && !StringComparer.Ordinal.Equals(AmmunitionDefinitionId, ammunitionDefinitionId)) throw new InvalidOperationException("A feed cannot mix ammunition definitions."); int loaded = Math.Min(quantity, Capacity - LoadedCount); if (loaded <= 0) return 0; AmmunitionDefinitionId = ammunitionDefinitionId; LoadedCount += loaded; Chambered = LoadedCount > 0; Revision++; return loaded; }
    public int Unload(int quantity) { int unloaded = Math.Min(Math.Max(0, quantity), LoadedCount); LoadedCount -= unloaded; if (LoadedCount == 0) { AmmunitionDefinitionId = null; Chambered = false; } Revision++; return unloaded; }
    public bool ConsumeOne() { if (LoadedCount <= 0) return false; LoadedCount--; Chambered = LoadedCount > 0; if (LoadedCount == 0) AmmunitionDefinitionId = null; Revision++; return true; }
    public AmmunitionLoadState Clone() { var copy = new AmmunitionLoadState(Capacity); if (AmmunitionDefinitionId is not null) copy.Load(AmmunitionDefinitionId, LoadedCount); copy.Revision = Revision; return copy; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32(Capacity); writer.WriteString(AmmunitionDefinitionId); writer.WriteInt32(LoadedCount); writer.WriteBoolean(Chambered); writer.WriteUInt64(Revision); }
    public static AmmunitionLoadState ReadCanonical(CanonicalReader reader)
    {
        int capacity = reader.ReadInt32(); string? definition = reader.ReadString(); int count = reader.ReadInt32(); bool chambered = reader.ReadBoolean(); ulong revision = reader.ReadUInt64();
        var result = new AmmunitionLoadState(capacity);
        if (definition is not null && count > 0) result.Load(definition, count);
        result.Chambered = chambered; result.Revision = revision;
        return result;
    }
}

public sealed class ItemInstanceState : ICanonicalWritable
{
    private readonly SortedDictionary<string, int> charges = new(StringComparer.Ordinal);
    private readonly SortedSet<string> runtimeTags = new(StringComparer.Ordinal);
    private readonly SortedDictionary<WeaponComponentSlot, StableId> installedComponents = [];
    public ItemInstanceState(StableId id, string definitionId, GeneratedItemBlueprint? blueprint, ItemLocation location, int quantity, ItemConditionState? condition = null)
    {
        if (id.IsEmpty) throw new ArgumentException("Item ID cannot be empty.", nameof(id)); ArgumentException.ThrowIfNullOrWhiteSpace(definitionId); if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Id = id; DefinitionId = definitionId; Blueprint = blueprint; Location = location; Quantity = quantity; Condition = condition ?? new ItemConditionState();
    }
    public StableId Id { get; }
    public string DefinitionId { get; }
    public GeneratedItemBlueprint? Blueprint { get; }
    public ItemLocation Location { get; private set; }
    public int Quantity { get; private set; }
    public ItemConditionState Condition { get; }
    public string? CustomName { get; private set; }
    public AmmunitionLoadState? AmmunitionLoad { get; private set; }
    public IReadOnlyDictionary<string, int> Charges => charges;
    public IReadOnlySet<string> RuntimeTags => runtimeTags;
    public IReadOnlyDictionary<WeaponComponentSlot, StableId> InstalledComponents => installedComponents;
    public ulong Revision { get; private set; }
    public void Touch() => Revision++;
    public void SetLocation(ItemLocation location) { Location = location; Revision++; }
    public void SetQuantity(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Quantity = value; Revision++; }
    public void Rename(string? name) { CustomName = string.IsNullOrWhiteSpace(name) ? null : name.Trim(); Revision++; }
    public void AddRuntimeTag(string tag) { ArgumentException.ThrowIfNullOrWhiteSpace(tag); if (runtimeTags.Add(tag)) Revision++; }
    public void RemoveRuntimeTag(string tag) { if (runtimeTags.Remove(tag)) Revision++; }
    public void SetCharge(string id, int value) { ArgumentException.ThrowIfNullOrWhiteSpace(id); if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); charges[id] = value; Revision++; }
    public void InitializeAmmunitionLoad(int capacity) { AmmunitionLoad = new AmmunitionLoadState(capacity); Revision++; }
    public void InstallComponent(WeaponComponentSlot slot, StableId componentId) { if (componentId.IsEmpty) throw new ArgumentException("Component ID cannot be empty.", nameof(componentId)); installedComponents[slot] = componentId; Revision++; }
    public bool RemoveComponent(WeaponComponentSlot slot) { bool removed = installedComponents.Remove(slot); if (removed) Revision++; return removed; }
    public ItemInstanceState Clone()
    {
        var copy = new ItemInstanceState(Id, DefinitionId, Blueprint, Location, Quantity, Condition.Clone()) { CustomName = CustomName, Revision = this.Revision };
        foreach ((string key, int value) in charges) copy.charges[key] = value; foreach (string tag in runtimeTags) copy.runtimeTags.Add(tag); foreach ((WeaponComponentSlot slot, StableId value) in installedComponents) copy.installedComponents[slot] = value;
        if (AmmunitionLoad is not null) copy.AmmunitionLoad = AmmunitionLoad.Clone();
        return copy;
    }
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteString(DefinitionId); writer.WriteBoolean(Blueprint is not null); Blueprint?.WriteCanonical(writer); Location.WriteCanonical(writer); writer.WriteInt32(Quantity); Condition.WriteCanonical(writer); writer.WriteString(CustomName);
        writer.WriteBoolean(AmmunitionLoad is not null); AmmunitionLoad?.WriteCanonical(writer); writer.WriteCount(charges.Count); foreach ((string key, int value) in charges) { writer.WriteString(key); writer.WriteInt32(value); }
        writer.WriteCount(runtimeTags.Count); foreach (string tag in runtimeTags) writer.WriteString(tag); writer.WriteCount(installedComponents.Count); foreach ((WeaponComponentSlot slot, StableId value) in installedComponents) { writer.WriteInt32((int)slot); writer.WriteStableId(value); } writer.WriteUInt64(Revision);
    }

    public static ItemInstanceState ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId(); string definition = reader.ReadRequiredString();
        GeneratedItemBlueprint? blueprint = reader.ReadBoolean() ? GeneratedItemBlueprint.ReadCanonical(reader) : null;
        ItemLocation location = ItemLocation.ReadCanonical(reader); int quantity = reader.ReadInt32(); ItemConditionState condition = ItemConditionState.ReadCanonical(reader); string? name = reader.ReadString();
        AmmunitionLoadState? load = reader.ReadBoolean() ? AmmunitionLoadState.ReadCanonical(reader) : null;
        var result = new ItemInstanceState(id, definition, blueprint, location, quantity, condition) { CustomName = name, AmmunitionLoad = load };
        int chargeCount = reader.ReadCount(); for (int i = 0; i < chargeCount; i++) result.charges.Add(reader.ReadRequiredString(), reader.ReadInt32());
        int tagCount = reader.ReadCount(); for (int i = 0; i < tagCount; i++) result.runtimeTags.Add(reader.ReadRequiredString());
        int componentCount = reader.ReadCount(); for (int i = 0; i < componentCount; i++) result.installedComponents.Add((WeaponComponentSlot)reader.ReadInt32(), reader.ReadStableId());
        result.Revision = reader.ReadUInt64();
        return result;
    }
}
