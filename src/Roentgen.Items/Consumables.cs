using Roentgen.Contracts;

namespace Roentgen.Items;

public sealed record ConsumableUseResult(bool Success, string Message, IReadOnlyList<ItemEffectDefinition> Effects, StableId ItemId);

public sealed class ConsumableService
{
    private readonly IReadOnlyDictionary<string, ConsumableDefinition> definitions;
    public ConsumableService(IReadOnlyDictionary<string, ConsumableDefinition> definitions) => this.definitions = definitions;
    public ConsumableUseResult Use(ItemWorldState world, StableId itemId, StableId userId)
    {
        ItemInstanceState item = world.GetItem(itemId); if (!definitions.TryGetValue(item.DefinitionId, out ConsumableDefinition? definition)) return new(false, "item is not consumable", [], itemId); if (item.Quantity <= 0) return new(false, "consumable is empty", [], itemId);
        if (item.Quantity == 1) world.RemoveItem(item.Id); else item.SetQuantity(item.Quantity - 1); return new(true, $"used by {userId}", definition.Effects, item.Id);
    }
}
