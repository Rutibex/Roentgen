using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record ArmorLayerCapability(StableId ItemId, ItemArmorLayer Layer, IReadOnlySet<string> CoveredRegions, IReadOnlyList<ArmorProtection> Protection, int ConditionPermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(ItemId); writer.WriteInt32((int)Layer); writer.WriteCount(CoveredRegions.Count); foreach (string value in CoveredRegions.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteCount(Protection.Count); foreach (ArmorProtection value in Protection.OrderBy(static value => value.Type)) value.WriteCanonical(writer); writer.WriteInt32(ConditionPermille); }
}

public sealed record ArmorCapabilitySnapshot(StableId ActorId, IReadOnlyList<ArmorLayerCapability> Layers, StateChecksum Fingerprint) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(ActorId); writer.WriteCount(Layers.Count); foreach (ArmorLayerCapability layer in Layers) layer.WriteCanonical(writer); writer.WriteChecksum(Fingerprint); }
}

public sealed class ArmorCapabilityCompiler
{
    public ArmorCapabilitySnapshot Compile(StableId actorId, ItemWorldState world, IReadOnlyDictionary<string, ArmorItemDefinition> definitions)
    {
        EquipmentLoadoutState loadout = world.GetLoadout(actorId); var layers = new List<ArmorLayerCapability>();
        foreach ((EquipmentSlot _, StableId itemId) in loadout.Slots) { ItemInstanceState item = world.GetItem(itemId); if (!definitions.TryGetValue(item.DefinitionId, out ArmorItemDefinition? definition)) continue; ArmorProtection[] scaled = definition.Protection.Select(value => new ArmorProtection(value.Type, value.Threshold * item.Condition.StructuralPermille / 1000, value.ResistancePermille * item.Condition.StructuralPermille / 1000)).ToArray(); layers.Add(new ArmorLayerCapability(item.Id, definition.Layer, definition.CoveredRegions, scaled, item.Condition.StructuralPermille)); }
        ArmorLayerCapability[] ordered = layers.OrderByDescending(static value => value.Layer).ThenBy(static value => value.ItemId).ToArray(); var writer = new CanonicalWriter(); writer.WriteStableId(actorId); writer.WriteCount(ordered.Length); foreach (ArmorLayerCapability layer in ordered) layer.WriteCanonical(writer); return new ArmorCapabilitySnapshot(actorId, ordered, writer.ComputeChecksum());
    }
}
