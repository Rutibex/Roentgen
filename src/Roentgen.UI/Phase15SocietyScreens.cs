using Roentgen.Contracts;
using Roentgen.Factions;
using FactionReputationDimension = Roentgen.Factions.ReputationDimension;

namespace Roentgen.UI;

public enum FactionScreenSection
{
    Overview = 0,
    Reputation = 1,
    Settlements = 2,
    Relations = 3,
    Crimes = 4,
    Trade = 5
}

public sealed record FactionSummaryRowModel(
    StableId FactionId,
    string Name,
    int CohesionPermille,
    int LegitimacyPermille,
    int ControlledSettlementCount,
    long TreasuryMinorUnits,
    string LeadershipStatus);

public sealed record FactionDetailScreenModel(
    StableId FactionId,
    string Name,
    string FoundingEvent,
    string SurvivalMethod,
    IReadOnlyList<string> Ideology,
    IReadOnlyList<string> MaterialNeeds,
    IReadOnlyList<string> Taboos,
    IReadOnlyList<string> Objectives,
    IReadOnlyList<string> Relationships,
    IReadOnlyList<string> InternalDivisions);

public sealed record SettlementSocietyScreenModel(
    StableId SettlementId,
    string Name,
    string Topology,
    StableId ControllingFactionId,
    string ControllingFactionName,
    int Population,
    int HousingCapacity,
    int StabilityPermille,
    int MigrationPressurePermille,
    int TaxPermille,
    IReadOnlyList<string> Resources,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> ActiveShipments);

public sealed record ReputationScreenModel(
    StableId SubjectId,
    StableId FactionId,
    string FactionName,
    string Standing,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Warrants);

public sealed record TerritoryScreenModel(
    IReadOnlyList<string> Claims,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> TradeRoutes);

/// <summary>Player-facing text helpers for the society simulation. Internal enum and stable-id vocabulary stays out of normal UI.</summary>
public static class SocietyUiText
{
    public static string Humanize(object? value)
    {
        string source = value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        source = source.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ');
        var chars = new List<char>(source.Length + 12);
        for (int index = 0; index < source.Length; index++)
        {
            char c = source[index];
            if (index > 0 && char.IsUpper(c) && char.IsLower(source[index - 1])) chars.Add(' ');
            chars.Add(c);
        }
        string text = string.Join(' ', new string(chars.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    public static string Percent(int permille) => $"{Math.Clamp(permille, 0, 1000) / 10.0:F0}%";
    public static string SignedPercent(int permille) => $"{Math.Clamp(permille, -1000, 1000) / 10.0:+0;-0;0}%";

    public static string Standing(string standing)
    {
        string human = Humanize(standing);
        return string.IsNullOrWhiteSpace(human) ? "Unknown" : human;
    }

    public static string ReputationLabel(FactionReputationDimension dimension) => dimension switch
    {
        FactionReputationDimension.Trust => "Trust",
        FactionReputationDimension.Fear => "Fear",
        FactionReputationDimension.Respect => "Respect",
        FactionReputationDimension.Obligation => "Obligation",
        FactionReputationDimension.IdeologicalAlignment => "Ideological affinity",
        FactionReputationDimension.KnownCrimeSeverity => "Known criminal concern",
        FactionReputationDimension.PublicFamiliarity => "Public familiarity",
        _ => Humanize(dimension)
    };

    public static string ReputationValue(FactionReputationDimension dimension, int value) => dimension switch
    {
        FactionReputationDimension.Fear or FactionReputationDimension.KnownCrimeSeverity or FactionReputationDimension.PublicFamiliarity => Percent(value),
        _ => SignedPercent(value)
    };

    public static string Bar(int permille, int cells = 12, bool signed = false)
    {
        int normalized = signed ? (Math.Clamp(permille, -1000, 1000) + 1000) / 2 : Math.Clamp(permille, 0, 1000);
        int filled = Math.Clamp((int)Math.Round(normalized / 1000.0 * cells), 0, cells);
        return "[" + new string('#', filled) + new string('.', cells - filled) + "]";
    }
}

public sealed class Phase15SocietyScreenBuilder
{
    public IReadOnlyList<FactionSummaryRowModel> BuildFactionList(SocietyWorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Factions
            .OrderBy(static value => value.Plan.DisplayName, StringComparer.Ordinal)
            .ThenBy(static value => value.Id)
            .Select(value =>
            {
                LeadershipState leadership = world.GetLeadership(value.Id);
                string status = leadership.HighestOffice is null
                    ? "No established leadership"
                    : leadership.HighestOffice.Vacant
                        ? $"{SocietyUiText.Humanize(leadership.HighestOffice.OfficeId)} vacant"
                        : $"{SocietyUiText.Humanize(leadership.HighestOffice.OfficeId)} occupied";
                return new FactionSummaryRowModel(value.Id, value.Plan.DisplayName, value.CohesionPermille, value.LegitimacyPermille, value.ControlledSettlements.Count, value.TreasuryMinorUnits, status);
            })
            .ToArray();
    }

    public FactionDetailScreenModel BuildFactionDetail(SocietyWorldState world, StableId factionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        FactionState faction = world.GetFaction(factionId);
        LeadershipState leadership = world.GetLeadership(factionId);
        Dictionary<string, string> objectiveNames = faction.Plan.Objectives.ToDictionary(static value => value.Id, static value => value.Description, StringComparer.Ordinal);
        string[] relationships = world.Relationships
            .Where(value => value.SourceFactionId == factionId)
            .OrderBy(static value => value.TargetFactionId)
            .Select(value => $"{world.GetFaction(value.TargetFactionId).Plan.DisplayName}: {SocietyUiText.Humanize(value.DerivedKind)} • trust {SocietyUiText.SignedPercent(value.TrustPermille)} • fear {SocietyUiText.Percent(value.FearPermille)} • respect {SocietyUiText.SignedPercent(value.RespectPermille)} • trade {SocietyUiText.Percent(value.TradeReliancePermille)}")
            .ToArray();
        return new FactionDetailScreenModel(
            faction.Id,
            faction.Plan.DisplayName,
            faction.Plan.FoundingEvent,
            faction.Plan.SurvivalMethod,
            faction.Ideology.OrderBy(static pair => pair.Key).Select(static pair => $"{SocietyUiText.Humanize(pair.Key)}: {SocietyUiText.SignedPercent(pair.Value)}").ToArray(),
            faction.Plan.MaterialNeeds.OrderBy(static value => value.Commodity).Select(static value => $"{SocietyUiText.Humanize(value.Commodity)}: {Math.Max(0, value.RequiredPerDayMilliunits) / 1000.0:F1}/day • priority {SocietyUiText.Percent(value.PriorityPermille)}").ToArray(),
            faction.Plan.Taboos.Order(StringComparer.Ordinal).Select(SocietyUiText.Humanize).ToArray(),
            faction.Plan.Objectives.OrderByDescending(static value => value.PriorityPermille).ThenBy(static value => value.Id, StringComparer.Ordinal).Select(static value => value.Description).ToArray(),
            relationships,
            leadership.Divisions.OrderByDescending(static value => value.InfluencePermille).ThenBy(static value => value.Id).Select(value =>
            {
                string objective = objectiveNames.GetValueOrDefault(value.ObjectiveId, SocietyUiText.Humanize(value.ObjectiveId));
                return $"{SocietyUiText.Humanize(value.Tag)} • influence {SocietyUiText.Percent(value.InfluencePermille)} • loyalty {SocietyUiText.Percent(value.LoyaltyPermille)} • priority: {objective}";
            }).ToArray());
    }

    public SettlementSocietyScreenModel BuildSettlement(SocietyWorldState world, StableId settlementId)
    {
        ArgumentNullException.ThrowIfNull(world);
        SettlementState settlement = world.GetSettlement(settlementId);
        return new SettlementSocietyScreenModel(
            settlement.Id,
            settlement.Plan.DisplayName,
            SocietyUiText.Humanize(settlement.Plan.Topology),
            settlement.ControllingFactionId,
            world.GetFaction(settlement.ControllingFactionId).Plan.DisplayName,
            settlement.Population.TotalPopulation,
            settlement.Population.HousingCapacity,
            settlement.StabilityPermille,
            settlement.Population.MigrationPressurePermille,
            settlement.TaxPermille,
            settlement.Stocks.Values.OrderBy(static value => value.Commodity).Select(static value => $"{SocietyUiText.Humanize(value.Commodity)}: {value.QuantityMilliunits / 1000.0:F1}/{value.StorageCapacityMilliunits / 1000.0:F1} stock • scarcity {SocietyUiText.Percent(value.ScarcityPermille)}").ToArray(),
            settlement.Services.OrderBy(static value => value.Kind).ThenBy(static value => value.Id).Select(static value => $"{SocietyUiText.Humanize(value.Kind)}: {(value.Operating ? "Open" : "Closed")} • quality {SocietyUiText.Percent(value.QualityPermille)} • queue {SocietyUiText.Percent(value.QueuePressurePermille)}").ToArray(),
            world.Shipments.Where(value => value.ExporterSettlementId == settlement.Id || value.ImporterSettlementId == settlement.Id).OrderBy(static value => value.Id).Select(value => $"{SocietyUiText.Humanize(value.Commodity)}: {world.GetSettlement(value.ExporterSettlementId).Plan.DisplayName} → {world.GetSettlement(value.ImporterSettlementId).Plan.DisplayName} • {(value.Resolved ? $"delivered {value.DeliveredMilliunits / 1000.0:F1}, lost {value.LostMilliunits / 1000.0:F1}" : $"arrival day {value.ArrivalDay}")}").ToArray());
    }

    public ReputationScreenModel BuildReputation(SocietyWorldState world, StableId subjectId, StableId factionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        FactionReputationState reputation = world.GetOrCreateReputation(subjectId, factionId);
        string[] evidence = world.ReputationEvidence
            .Where(value => value.SubjectId == subjectId && value.FactionId == factionId)
            .OrderByDescending(static value => value.CreatedDay)
            .ThenBy(static value => value.Id)
            .Select(static value => $"Day {value.CreatedDay}: {SocietyUiText.ReputationLabel(value.Dimension)} {value.Delta:+#;-#;0} — {SocietyUiText.Humanize(value.Reason)} • reliability {SocietyUiText.Percent(value.ReliabilityPermille)} • publicity {SocietyUiText.Percent(value.PublicityPermille)}")
            .ToArray();
        string[] warrants = world.Crimes
            .Where(value => value.SuspectedActorId == subjectId && value.JurisdictionFactionId == factionId && value.Status is CrimeStatus.WarrantIssued or CrimeStatus.FineOutstanding or CrimeStatus.DetentionOrdered)
            .OrderByDescending(static value => value.SeverityPermille)
            .ThenBy(static value => value.Id)
            .Select(static value => $"{SocietyUiText.Humanize(value.Kind)} • severity {SocietyUiText.Percent(value.SeverityPermille)} • identity confidence {SocietyUiText.Percent(value.IdentityConfidencePermille)} • fine {value.FineMinorUnits} scrip")
            .ToArray();
        return new ReputationScreenModel(
            subjectId,
            factionId,
            world.GetFaction(factionId).Plan.DisplayName,
            SocietyUiText.Standing(reputation.DerivedStanding),
            reputation.Values.OrderBy(static pair => pair.Key).Select(static pair => $"{SocietyUiText.ReputationLabel(pair.Key)}: {SocietyUiText.ReputationValue(pair.Key, pair.Value)}").ToArray(),
            evidence,
            warrants);
    }

    public TerritoryScreenModel BuildTerritory(SocietyWorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new TerritoryScreenModel(
            world.TerritoryClaims.OrderBy(static value => value.LocationId).ThenBy(static value => value.FactionId).Select(value => $"{LocationName(world, value.LocationId)}: {world.GetFaction(value.FactionId).Plan.DisplayName} • {SocietyUiText.Humanize(value.Kind)} • strength {SocietyUiText.Percent(value.StrengthPermille)}").ToArray(),
            world.Conflicts.OrderBy(static value => value.LocationId).ThenBy(static value => value.Id).Select(value => $"{world.GetFaction(value.AggressorFactionId).Plan.DisplayName} vs {world.GetFaction(value.DefenderFactionId).Plan.DisplayName} at {LocationName(world, value.LocationId)} • {SocietyUiText.Humanize(value.Stage)} • player relevance {SocietyUiText.Percent(value.PlayerRelevancePermille)}").ToArray(),
            world.TradeRoutes.OrderBy(static value => value.Id).Select(value => $"{world.GetSettlement(value.ExporterSettlementId).Plan.DisplayName} → {world.GetSettlement(value.ImporterSettlementId).Plan.DisplayName}: {SocietyUiText.Humanize(value.Commodity)} • capacity {value.DailyCapacityMilliunits / 1000.0:F1}/day • risk {SocietyUiText.Percent(value.BaseRiskPermille)} • {(value.Active ? "Active" : "Suspended")}").ToArray());
    }

    private static string LocationName(SocietyWorldState world, StableId id)
        => world.TryGetSettlement(id, out SettlementState? settlement) && settlement is not null ? settlement.Plan.DisplayName : "Contested site";
}
