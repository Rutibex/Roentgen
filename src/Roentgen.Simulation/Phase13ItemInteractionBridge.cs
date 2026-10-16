using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Items;

namespace Roentgen.Simulation;

public sealed class Phase13ItemInteractionBridge
{
    private readonly ItemWorldState items;
    private readonly ItemDefinitionCatalog definitions;
    public Phase13ItemInteractionBridge(ItemWorldState items, ItemDefinitionCatalog definitions) { this.items = items ?? throw new ArgumentNullException(nameof(items)); this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions)); }
    public IReadOnlyList<PortableObjectState> BuildLegacyPresentationView()
    {
        return items.Items.Where(static item => item.Location.Kind is ItemLocationKind.World or ItemLocationKind.Inventory or ItemLocationKind.Container)
            .OrderBy(static item => item.Id)
            .Select(item => new PortableObjectState(item.Id, item.DefinitionId, definitions.Get(item.DefinitionId).DisplayName, item.Location.HolderId, item.Quantity, item.Location.Kind == ItemLocationKind.World, checked((long)Math.Min((ulong)long.MaxValue, item.Revision))))
            .ToArray();
    }
    public ItemTransactionResult TakeWorldItem(StableId actorId, StableId itemId, ulong tick)
    {
        ItemInstanceState item = items.GetItem(itemId); if (item.Location.Kind != ItemLocationKind.World) return new ItemTransactionResult(ItemTransactionStatus.Rejected, "item is not in the world", []);
        var transaction = new ItemTransaction(StableIdFactory.CreateChild(itemId, "take", checked((long)tick)), tick, [new TransferItemOperation(itemId, ItemLocation.InInventory(actorId))], [new ItemRevisionRequirement(itemId, item.Revision)]);
        return new ItemTransactionService(definitions).Execute(items, transaction);
    }
}
