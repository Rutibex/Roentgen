using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record MerchantProfile(string Id, MerchantArchetype Archetype, IReadOnlySet<string> StockTags, int RefreshDays, int MaximumStockValueMinorUnits, int CashReserveMinorUnits) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)Archetype); writer.WriteCount(StockTags.Count); foreach (string value in StockTags.Order(StringComparer.Ordinal)) writer.WriteString(value); writer.WriteInt32(RefreshDays); writer.WriteInt32(MaximumStockValueMinorUnits); writer.WriteInt32(CashReserveMinorUnits); }
}
public sealed record TradeLine(StableId ItemId, int Quantity, int UnitPriceMinorUnits, bool MerchantSells) : ICanonicalWritable { public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(ItemId); writer.WriteInt32(Quantity); writer.WriteInt32(UnitPriceMinorUnits); writer.WriteBoolean(MerchantSells); } }
public sealed record TradeQuote(StableId QuoteId, StableId MerchantId, StableId BuyerId, IReadOnlyList<TradeLine> Lines, string CurrencyDefinitionId, int NetPaymentToMerchantMinorUnits, int TaxMinorUnits, ulong MerchantInventoryRevision, ulong BuyerInventoryRevision, ulong ExpiresTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(QuoteId); writer.WriteStableId(MerchantId); writer.WriteStableId(BuyerId); writer.WriteCount(Lines.Count); foreach (TradeLine value in Lines.OrderBy(static value => value.ItemId)) value.WriteCanonical(writer); writer.WriteString(CurrencyDefinitionId); writer.WriteInt32(NetPaymentToMerchantMinorUnits); writer.WriteInt32(TaxMinorUnits); writer.WriteUInt64(MerchantInventoryRevision); writer.WriteUInt64(BuyerInventoryRevision); writer.WriteUInt64(ExpiresTick); }
}
public sealed record TradeExecutionResult(bool Success, string Message, int NetPaymentMinorUnits);

public sealed class MerchantService
{
    public TradeExecutionResult Execute(ItemWorldState world, TradeQuote quote, ulong currentTick)
    {
        if (currentTick > quote.ExpiresTick) return new(false, "quote expired", 0);
        InventoryState merchant = world.GetInventory(quote.MerchantId);
        InventoryState buyer = world.GetInventory(quote.BuyerId);
        if (merchant.Revision != quote.MerchantInventoryRevision || buyer.Revision != quote.BuyerInventoryRevision) return new(false, "quote is stale", 0);
        foreach (IGrouping<StableId, TradeLine> group in quote.Lines.GroupBy(static value => value.ItemId))
        {
            ItemInstanceState item = world.GetItem(group.Key);
            bool merchantSells = group.First().MerchantSells;
            if (group.Any(value => value.MerchantSells != merchantSells)) return new(false, "one item cannot be bought and sold in the same quote", 0);
            StableId expected = merchantSells ? quote.MerchantId : quote.BuyerId;
            int quantity = group.Sum(static value => value.Quantity);
            if (item.Location.HolderId != expected || quantity <= 0 || quantity > item.Quantity - world.ReservedQuantity(item.Id)) return new(false, "trade line unavailable", 0);
        }
        StableId payer = quote.NetPaymentToMerchantMinorUnits >= 0 ? quote.BuyerId : quote.MerchantId;
        StableId payee = quote.NetPaymentToMerchantMinorUnits >= 0 ? quote.MerchantId : quote.BuyerId;
        int payment = Math.Abs(quote.NetPaymentToMerchantMinorUnits);
        if (CurrencyAvailable(world, payer, quote.CurrencyDefinitionId) < payment) return new(false, "insufficient currency", 0);
        int splitOrdinal = 0;
        foreach (TradeLine line in quote.Lines.OrderBy(static value => value.ItemId))
        {
            ItemInstanceState item = world.GetItem(line.ItemId);
            StableId destination = line.MerchantSells ? quote.BuyerId : quote.MerchantId;
            if (line.Quantity == item.Quantity)
            {
                world.GetInventory(item.Location.HolderId).Remove(item.Id);
                item.SetLocation(ItemLocation.InInventory(destination));
                world.EnsureInventory(destination).Add(item.Id);
            }
            else
            {
                StableId childId = StableIdFactory.CreateChild(quote.QuoteId, "trade-split", splitOrdinal++);
                item.SetQuantity(item.Quantity - line.Quantity);
                var child = new ItemInstanceState(childId, item.DefinitionId, item.Blueprint, ItemLocation.InInventory(destination), line.Quantity, item.Condition.Clone());
                world.AddItem(child);
                world.EnsureInventory(destination).Add(child.Id);
            }
        }
        TransferCurrency(world, quote.QuoteId, payer, payee, quote.CurrencyDefinitionId, payment);
        return new(true, "trade committed", quote.NetPaymentToMerchantMinorUnits);
    }
    private static int CurrencyAvailable(ItemWorldState world, StableId ownerId, string definitionId) => world.GetInventory(ownerId).ItemIds.Select(world.GetItem).Where(value => StringComparer.Ordinal.Equals(value.DefinitionId, definitionId)).Sum(value => value.Quantity - world.ReservedQuantity(value.Id));
    private static void TransferCurrency(ItemWorldState world, StableId quoteId, StableId payer, StableId payee, string definitionId, int quantity)
    {
        int remaining = quantity;
        foreach (ItemInstanceState stack in world.GetInventory(payer).ItemIds.Select(world.GetItem).Where(value => StringComparer.Ordinal.Equals(value.DefinitionId, definitionId)).OrderBy(static value => value.Id).ToArray())
        {
            int used = Math.Min(remaining, stack.Quantity - world.ReservedQuantity(stack.Id));
            if (used <= 0) continue;
            if (used == stack.Quantity) world.DestroyItem(stack.Id); else stack.SetQuantity(stack.Quantity - used);
            remaining -= used;
            if (remaining == 0) break;
        }
        if (quantity == 0) return;
        ItemInstanceState? destination = world.GetInventory(payee).ItemIds.Select(world.GetItem).FirstOrDefault(value => StringComparer.Ordinal.Equals(value.DefinitionId, definitionId));
        if (destination is null)
        {
            StableId id = StableIdFactory.CreateChild(quoteId, "currency-payment", 0);
            destination = new ItemInstanceState(id, definitionId, null, ItemLocation.InInventory(payee), quantity);
            world.AddItem(destination); world.GetInventory(payee).Add(id);
        }
        else destination.SetQuantity(destination.Quantity + quantity);
    }
}
