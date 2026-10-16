using Roentgen.Contracts;
using Roentgen.Factions;

namespace Roentgen.Items;

public sealed record SocietyMarketProjection(StableId SettlementId, StableId ControllingFactionId, int TaxPermille, int MerchantRiskPermille, string CurrencyPolicyId, IReadOnlyDictionary<EconomyCommodity, int> ScarcityPermille, IReadOnlySet<string> PreferredGoods, IReadOnlySet<string> ProhibitedGoods);

public sealed class Phase15SocietyEconomyBridge
{
    public IReadOnlyList<SocietyMarketProjection> Project(SocietyWorldState society, ItemWorldState items)
    {
        ArgumentNullException.ThrowIfNull(society); ArgumentNullException.ThrowIfNull(items); var projections = new List<SocietyMarketProjection>();
        foreach (SettlementState settlement in society.Settlements.OrderBy(static value => value.Id))
        {
            CurrencyPolicyKind kind = settlement.CurrencyPolicyId.Contains("barter", StringComparison.Ordinal) ? CurrencyPolicyKind.Barter : settlement.CurrencyPolicyId.Contains("credit", StringComparison.Ordinal) ? CurrencyPolicyKind.FactionCredit : settlement.CurrencyPolicyId.Contains("scrip", StringComparison.Ordinal) ? CurrencyPolicyKind.Scrip : CurrencyPolicyKind.Mixed;
            var economy = new SettlementEconomyState(settlement.Id, new CurrencyPolicy(kind, settlement.CurrencyPolicyId, 80), new TaxPolicy(settlement.TaxPermille, settlement.TaxPermille / 2, settlement.MerchantRiskPermille), settlement.MerchantRiskPermille);
            var scarcity = new SortedDictionary<EconomyCommodity, int>();
            foreach ((SocietyCommodity commodity, ResourceStockState stock) in settlement.Stocks.OrderBy(static value => value.Key))
            {
                EconomyCommodity itemCommodity = Map(commodity); economy.AddStock(new CommodityStockState(itemCommodity, stock.QuantityMilliunits, stock.TargetMilliunits, BasePrice(itemCommodity))); scarcity[itemCommodity] = stock.ScarcityPermille;
            }
            foreach (string value in settlement.PreferredGoods) economy.AddPreferredGood(value); foreach (string value in settlement.ProhibitedGoods) economy.AddProhibitedGood(value); items.UpsertEconomy(economy);
            projections.Add(new SocietyMarketProjection(settlement.Id, settlement.ControllingFactionId, settlement.TaxPermille, settlement.MerchantRiskPermille, settlement.CurrencyPolicyId, scarcity, settlement.PreferredGoods, settlement.ProhibitedGoods));
        }
        return projections;
    }

    public int ApplyScarcityToPrice(int basePriceMinorUnits, SocietyMarketProjection projection, EconomyCommodity commodity, bool merchantSells)
    {
        int scarcity = projection.ScarcityPermille.GetValueOrDefault(commodity); int scarcityAdjustment = merchantSells ? scarcity * 3 / 5 : scarcity / 4; int tax = merchantSells ? projection.TaxPermille : projection.TaxPermille / 2; int risk = projection.MerchantRiskPermille / 5; return Math.Max(1, basePriceMinorUnits * (1000 + scarcityAdjustment + tax + risk) / 1000);
    }

    private static EconomyCommodity Map(SocietyCommodity value) => value switch
    {
        SocietyCommodity.Food => EconomyCommodity.Food, SocietyCommodity.Water => EconomyCommodity.Water, SocietyCommodity.Medicine => EconomyCommodity.Medicine, SocietyCommodity.Fuel => EconomyCommodity.Fuel,
        SocietyCommodity.Ammunition => EconomyCommodity.Ammunition, SocietyCommodity.Metals => EconomyCommodity.Metals, SocietyCommodity.Chemicals => EconomyCommodity.Chemicals, SocietyCommodity.Electronics => EconomyCommodity.Electronics,
        SocietyCommodity.Textiles => EconomyCommodity.Textiles, SocietyCommodity.Weapons => EconomyCommodity.Weapons, SocietyCommodity.Armor => EconomyCommodity.Armor, SocietyCommodity.LuxuryGoods => EconomyCommodity.LuxuryGoods, SocietyCommodity.DiagnosticMaterials => EconomyCommodity.DiagnosticMaterials,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static int BasePrice(EconomyCommodity value) => value switch { EconomyCommodity.Water => 4, EconomyCommodity.Food => 6, EconomyCommodity.Medicine => 30, EconomyCommodity.Fuel => 14, EconomyCommodity.Ammunition => 12, EconomyCommodity.Metals => 9, EconomyCommodity.Chemicals => 18, EconomyCommodity.Electronics => 24, EconomyCommodity.Textiles => 8, EconomyCommodity.Weapons => 80, EconomyCommodity.Armor => 70, EconomyCommodity.LuxuryGoods => 40, EconomyCommodity.DiagnosticMaterials => 28, _ => 10 };
}
