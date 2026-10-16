using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record LootSlotDefinition(string Id, string? RequiredTag, string? ExactDefinitionId, int MinimumQuantity, int MaximumQuantity, int ChancePermille, int MinimumConditionPermille, int MaximumConditionPermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteString(RequiredTag); writer.WriteString(ExactDefinitionId); writer.WriteInt32(MinimumQuantity); writer.WriteInt32(MaximumQuantity); writer.WriteInt32(ChancePermille); writer.WriteInt32(MinimumConditionPermille); writer.WriteInt32(MaximumConditionPermille); }
}
public sealed record LootProfile(string Id, IReadOnlyList<LootSlotDefinition> Slots, IReadOnlySet<string> RequiredLocationTags, IReadOnlySet<string> ForbiddenTags, int ValueBudgetMinorUnits) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteCount(Slots.Count); foreach (LootSlotDefinition value in Slots) value.WriteCanonical(writer); writer.WriteCount(RequiredLocationTags.Count); foreach (string value in RequiredLocationTags.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteCount(ForbiddenTags.Count); foreach (string value in ForbiddenTags.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteInt32(ValueBudgetMinorUnits); }
}
public sealed record GeneratedLoot(IReadOnlyList<ItemInstanceState> Items, int TotalValueMinorUnits, StateChecksum Fingerprint);

public sealed class LootGenerator
{
    public GeneratedLoot Generate(CampaignSeed seed, StableId containerId, LootProfile profile, ItemDefinitionCatalog catalog)
    {
        var generated = new List<ItemInstanceState>(); int value = 0; int ordinal = 0;
        foreach (LootSlotDefinition slot in profile.Slots.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            int roll = Roll(seed, containerId, $"{slot.Id}.chance", 1000); if (roll >= slot.ChancePermille) continue;
            ItemDefinition[] candidates = slot.ExactDefinitionId is not null ? [catalog.Get(slot.ExactDefinitionId)] : catalog.Definitions.Where(value => slot.RequiredTag is null || value.Tags.Contains(slot.RequiredTag)).OrderBy(static value => value.Id, StringComparer.Ordinal).ToArray(); if (candidates.Length == 0) continue;
            ItemDefinition definition = candidates[Roll(seed, containerId, $"{slot.Id}.item", candidates.Length)]; int quantity = slot.MinimumQuantity + Roll(seed, containerId, $"{slot.Id}.quantity", Math.Max(1, slot.MaximumQuantity - slot.MinimumQuantity + 1)); int itemValue = definition.BaseValueMinorUnits * quantity; if (value + itemValue > profile.ValueBudgetMinorUnits) continue;
            int condition = slot.MinimumConditionPermille + Roll(seed, containerId, $"{slot.Id}.condition", Math.Max(1, slot.MaximumConditionPermille - slot.MinimumConditionPermille + 1)); StableId id = StableIdFactory.CreateChild(containerId, "loot", ordinal++); generated.Add(new ItemInstanceState(id, definition.Id, null, ItemLocation.InContainer(containerId), quantity, new ItemConditionState(condition, condition, condition))); value += itemValue;
        }
        var writer = new CanonicalWriter(); writer.WriteStableId(containerId); writer.WriteString(profile.Id); writer.WriteCount(generated.Count); foreach (ItemInstanceState item in generated) item.WriteCanonical(writer); return new GeneratedLoot(generated, value, writer.ComputeChecksum());
    }
    private static int Roll(CampaignSeed seed, StableId parent, string domain, int range) { var writer = new CanonicalWriter(); writer.WriteUInt64(seed.Value); writer.WriteStableId(parent); writer.WriteString(domain); return (int)(writer.ComputeChecksum().A % (ulong)Math.Max(1, range)); }
}
