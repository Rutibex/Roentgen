using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed class CommodityStockState : ICanonicalWritable
{
    public CommodityStockState(EconomyCommodity commodity, long quantityMilliunits, long targetMilliunits, int basePriceMinorUnits) { Commodity = commodity; QuantityMilliunits = Math.Max(0, quantityMilliunits); TargetMilliunits = Math.Max(1, targetMilliunits); BasePriceMinorUnits = Math.Max(1, basePriceMinorUnits); }
    public EconomyCommodity Commodity { get; }
    public long QuantityMilliunits { get; private set; }
    public long TargetMilliunits { get; }
    public int BasePriceMinorUnits { get; }
    public ulong Revision { get; private set; }
    public void Add(long amount) { QuantityMilliunits = Math.Max(0, checked(QuantityMilliunits + amount)); Revision++; }
    public bool TryConsume(long amount) { if (amount < 0 || QuantityMilliunits < amount) return false; QuantityMilliunits -= amount; Revision++; return true; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Commodity); writer.WriteInt64(QuantityMilliunits); writer.WriteInt64(TargetMilliunits); writer.WriteInt32(BasePriceMinorUnits); writer.WriteUInt64(Revision); }
    public static CommodityStockState ReadCanonical(CanonicalReader reader) => new((EconomyCommodity)reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32()) { Revision = reader.ReadUInt64() };
}
public sealed record EconomyProcess(EconomyCommodity Commodity, long AmountPerDayMilliunits) : ICanonicalWritable { public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Commodity); writer.WriteInt64(AmountPerDayMilliunits); } public static EconomyProcess ReadCanonical(CanonicalReader reader) => new((EconomyCommodity)reader.ReadInt32(), reader.ReadInt64()); }
public sealed record TradeRouteState(StableId RouteId, StableId PartnerSettlementId, EconomyCommodity Commodity, long CapacityPerDayMilliunits, int RiskPermille, bool Active) : ICanonicalWritable { public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(RouteId); writer.WriteStableId(PartnerSettlementId); writer.WriteInt32((int)Commodity); writer.WriteInt64(CapacityPerDayMilliunits); writer.WriteInt32(RiskPermille); writer.WriteBoolean(Active); } public static TradeRouteState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), (EconomyCommodity)reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt32(), reader.ReadBoolean()); }
public sealed record CurrencyPolicy(CurrencyPolicyKind Kind, string CurrencyDefinitionId, int ExchangeSpreadPermille) : ICanonicalWritable { public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Kind); writer.WriteString(CurrencyDefinitionId); writer.WriteInt32(ExchangeSpreadPermille); } public static CurrencyPolicy ReadCanonical(CanonicalReader reader) => new((CurrencyPolicyKind)reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadInt32()); }
public sealed record TaxPolicy(int PurchaseTaxPermille, int SaleTaxPermille, int ContrabandRiskPermille) : ICanonicalWritable { public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32(PurchaseTaxPermille); writer.WriteInt32(SaleTaxPermille); writer.WriteInt32(ContrabandRiskPermille); } public static TaxPolicy ReadCanonical(CanonicalReader reader) => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()); }

public sealed class SettlementEconomyState : ICanonicalWritable
{
    private readonly SortedDictionary<EconomyCommodity, CommodityStockState> stocks = [];
    private readonly List<EconomyProcess> production = [];
    private readonly List<EconomyProcess> consumption = [];
    private readonly SortedDictionary<StableId, TradeRouteState> tradeRoutes = [];
    private readonly SortedSet<string> preferredGoods = new(StringComparer.Ordinal);
    private readonly SortedSet<string> prohibitedGoods = new(StringComparer.Ordinal);
    public SettlementEconomyState(StableId settlementId, CurrencyPolicy currencyPolicy, TaxPolicy taxPolicy, int merchantRiskPermille) { if (settlementId.IsEmpty) throw new ArgumentException("Settlement ID cannot be empty.", nameof(settlementId)); SettlementId = settlementId; CurrencyPolicy = currencyPolicy; TaxPolicy = taxPolicy; MerchantRiskPermille = Math.Clamp(merchantRiskPermille, 0, 1000); }
    public StableId SettlementId { get; }
    public CurrencyPolicy CurrencyPolicy { get; }
    public TaxPolicy TaxPolicy { get; }
    public int MerchantRiskPermille { get; }
    public IReadOnlyDictionary<EconomyCommodity, CommodityStockState> Stocks => stocks;
    public IReadOnlyList<EconomyProcess> Production => production;
    public IReadOnlyList<EconomyProcess> Consumption => consumption;
    public IReadOnlyList<TradeRouteState> TradeRoutes => tradeRoutes.Values.ToArray();
    public IReadOnlySet<string> PreferredGoods => preferredGoods;
    public IReadOnlySet<string> ProhibitedGoods => prohibitedGoods;
    public ulong Day { get; private set; }
    public ulong Revision { get; private set; }
    public void AddStock(CommodityStockState state) { stocks[state.Commodity] = state; Revision++; }
    public void AddProduction(EconomyProcess process) { production.Add(process); Revision++; }
    public void AddConsumption(EconomyProcess process) { consumption.Add(process); Revision++; }
    public void AddTradeRoute(TradeRouteState route) { tradeRoutes[route.RouteId] = route; Revision++; }
    public void AddPreferredGood(string tag) { preferredGoods.Add(tag); Revision++; }
    public void AddProhibitedGood(string tag) { prohibitedGoods.Add(tag); Revision++; }
    public void AdvanceDay()
    {
        foreach (EconomyProcess process in production.OrderBy(static value => value.Commodity)) if (stocks.TryGetValue(process.Commodity, out CommodityStockState? stock)) stock.Add(process.AmountPerDayMilliunits);
        foreach (EconomyProcess process in consumption.OrderBy(static value => value.Commodity)) if (stocks.TryGetValue(process.Commodity, out CommodityStockState? stock)) stock.TryConsume(Math.Min(stock.QuantityMilliunits, process.AmountPerDayMilliunits));
        foreach (TradeRouteState route in tradeRoutes.Values.Where(static value => value.Active).OrderBy(static value => value.RouteId)) if (stocks.TryGetValue(route.Commodity, out CommodityStockState? stock)) stock.Add(route.CapacityPerDayMilliunits * (1000 - route.RiskPermille) / 1000);
        Day++; Revision++;
    }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(SettlementId); CurrencyPolicy.WriteCanonical(writer); TaxPolicy.WriteCanonical(writer); writer.WriteInt32(MerchantRiskPermille); writer.WriteCount(stocks.Count); foreach (CommodityStockState value in stocks.Values) value.WriteCanonical(writer); writer.WriteCount(production.Count); foreach (EconomyProcess value in production.OrderBy(static value => value.Commodity)) value.WriteCanonical(writer); writer.WriteCount(consumption.Count); foreach (EconomyProcess value in consumption.OrderBy(static value => value.Commodity)) value.WriteCanonical(writer); writer.WriteCount(tradeRoutes.Count); foreach (TradeRouteState value in tradeRoutes.Values) value.WriteCanonical(writer); writer.WriteCount(preferredGoods.Count); foreach (string value in preferredGoods) writer.WriteString(value); writer.WriteCount(prohibitedGoods.Count); foreach (string value in prohibitedGoods) writer.WriteString(value); writer.WriteUInt64(Day); writer.WriteUInt64(Revision); }

    public static SettlementEconomyState ReadCanonical(CanonicalReader reader)
    {
        StableId settlement = reader.ReadStableId(); CurrencyPolicy currency = CurrencyPolicy.ReadCanonical(reader); TaxPolicy tax = TaxPolicy.ReadCanonical(reader); int risk = reader.ReadInt32();
        var result = new SettlementEconomyState(settlement, currency, tax, risk);
        int stockCount = reader.ReadCount(); for (int i = 0; i < stockCount; i++) { CommodityStockState value = CommodityStockState.ReadCanonical(reader); result.stocks.Add(value.Commodity, value); }
        int productionCount = reader.ReadCount(); for (int i = 0; i < productionCount; i++) result.production.Add(EconomyProcess.ReadCanonical(reader));
        int consumptionCount = reader.ReadCount(); for (int i = 0; i < consumptionCount; i++) result.consumption.Add(EconomyProcess.ReadCanonical(reader));
        int routeCount = reader.ReadCount(); for (int i = 0; i < routeCount; i++) { TradeRouteState value = TradeRouteState.ReadCanonical(reader); result.tradeRoutes.Add(value.RouteId, value); }
        int preferredCount = reader.ReadCount(); for (int i = 0; i < preferredCount; i++) result.preferredGoods.Add(reader.ReadRequiredString());
        int prohibitedCount = reader.ReadCount(); for (int i = 0; i < prohibitedCount; i++) result.prohibitedGoods.Add(reader.ReadRequiredString());
        result.Day = reader.ReadUInt64(); result.Revision = reader.ReadUInt64();
        return result;
    }
}

public sealed record PriceBreakdown(int BaseValue, int ConditionPermille, int ScarcityPermille, int MerchantSpreadPermille, int TaxPermille, int RiskPermille, int RelationshipPermille, int CommercePermille, int FinalPriceMinorUnits);

public sealed class EconomyPriceCalculator
{
    public PriceBreakdown Calculate(ItemDefinition definition, ItemInstanceState item, SettlementEconomyState economy, EconomyCommodity commodity, bool merchantSelling, int relationshipPermille, int commercePermille)
    {
        CommodityStockState stock = economy.Stocks[commodity]; long ratioPermille = stock.TargetMilliunits * 1000L / Math.Max(1L, stock.QuantityMilliunits); int scarcity = Math.Clamp((int)ratioPermille, 500, 2500); int condition = Math.Clamp(250 + item.Condition.OverallPermille * 750 / 1000, 250, 1000); int spread = merchantSelling ? 1200 : 700; int tax = merchantSelling ? 1000 + economy.TaxPolicy.PurchaseTaxPermille : 1000 - economy.TaxPolicy.SaleTaxPermille; int risk = 1000 + economy.MerchantRiskPermille / 3; int relationship = Math.Clamp(relationshipPermille, 800, 1200); int commerce = Math.Clamp(commercePermille, 850, 1150); long value = definition.BaseValueMinorUnits; foreach (int factor in new[] { condition, scarcity, spread, tax, risk, relationship, merchantSelling ? 2000 - commerce : commerce }) value = value * factor / 1000L; int final = Math.Clamp((int)Math.Min(int.MaxValue, value), Math.Max(1, definition.BaseValueMinorUnits / 10), Math.Max(1, definition.BaseValueMinorUnits * 10)); return new PriceBreakdown(definition.BaseValueMinorUnits, condition, scarcity, spread, tax, risk, relationship, commerce, final);
    }
}
