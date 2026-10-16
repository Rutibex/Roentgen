using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public sealed class SocietyValidator
{
    public SocietyValidationReport Validate(SocietyWorldState world, IReadOnlySet<string>? availableWorldTags = null)
    {
        ArgumentNullException.ThrowIfNull(world); availableWorldTags ??= new SortedSet<string>(StringComparer.Ordinal); var issues = new List<SocietyValidationIssue>();
        ValidateFactions(world, availableWorldTags, issues); ValidateSettlements(world, issues); ValidateRelations(world, issues); ValidateTrade(world, issues); ValidateLeadership(world, issues); ValidateCrimes(world, issues); ValidateConflicts(world, issues);
        return new SocietyValidationReport(!issues.Any(static value => value.IsCritical), issues.OrderBy(static value => value.Code, StringComparer.Ordinal).ThenBy(static value => value.SubjectId).ToArray(), 0);
    }

    private static void ValidateFactions(SocietyWorldState world, IReadOnlySet<string> worldTags, ICollection<SocietyValidationIssue> issues)
    {
        foreach (FactionState faction in world.Factions.OrderBy(static value => value.Id))
        {
            if (faction.Id.IsEmpty) issues.Add(new SocietyValidationIssue("faction.empty-id", "Faction ID is empty.", true, faction.Id));
            if (string.IsNullOrWhiteSpace(faction.Plan.DisplayName)) issues.Add(new SocietyValidationIssue("faction.missing-name", "Faction has no display name.", true, faction.Id));
            if (faction.Plan.Ideology.Count != Enum.GetValues<IdeologyAxis>().Length) issues.Add(new SocietyValidationIssue("faction.ideology-incomplete", "Faction does not define every ideology axis.", true, faction.Id));
            if (faction.Plan.Objectives.Count == 0) issues.Add(new SocietyValidationIssue("faction.no-objective", "Faction has no strategic objective.", true, faction.Id));
            foreach (FactionObjectiveDefinition objective in faction.Plan.Objectives)
            {
                foreach (string requiredTag in objective.RequiredWorldTags)
                    if (worldTags.Count > 0 && !worldTags.Contains(requiredTag)) issues.Add(new SocietyValidationIssue("faction.objective-unreachable", $"Objective '{objective.Id}' requires unavailable world tag '{requiredTag}'.", true, faction.Id));
            }
            if (faction.ControlledSettlements.Count == 0) issues.Add(new SocietyValidationIssue("faction.no-base", "Faction controls no settlement or organizational base.", true, faction.Id));
        }
    }

    private static void ValidateSettlements(SocietyWorldState world, ICollection<SocietyValidationIssue> issues)
    {
        foreach (SettlementState settlement in world.Settlements.OrderBy(static value => value.Id))
        {
            if (!world.TryGetFaction(settlement.ControllingFactionId, out _)) issues.Add(new SocietyValidationIssue("settlement.unknown-controller", "Settlement controlling faction does not exist.", true, settlement.Id));
            if (settlement.Population.TotalPopulation < 0) issues.Add(new SocietyValidationIssue("settlement.negative-population", "Settlement population is negative.", true, settlement.Id));
            if (settlement.Population.OvercrowdingPermille > 0 && settlement.Population.MigrationPressurePermille <= -500) issues.Add(new SocietyValidationIssue("settlement.overcrowding-hidden", "Overcrowded settlement has implausibly strong inward migration pressure.", false, settlement.Id));
            foreach (ResourceStockState stock in settlement.Stocks.Values)
            {
                if (stock.QuantityMilliunits < 0 || stock.QuantityMilliunits > stock.StorageCapacityMilliunits) issues.Add(new SocietyValidationIssue("resource.bounds", $"Resource {stock.Commodity} is outside storage bounds.", true, settlement.Id));
            }
            foreach (SettlementServiceState service in settlement.Services)
            {
                if (service.SettlementId != settlement.Id) issues.Add(new SocietyValidationIssue("service.wrong-settlement", "Service belongs to a different settlement.", true, service.Id));
                if (service.Operating && service.EffectiveCapacity <= 0) issues.Add(new SocietyValidationIssue("service.false-operation", "Service reports operating but has no effective capacity.", true, service.Id));
            }
        }
    }

    private static void ValidateRelations(SocietyWorldState world, ICollection<SocietyValidationIssue> issues)
    {
        foreach (FactionRelationshipState relationship in world.Relationships)
        {
            if (!world.TryGetFaction(relationship.SourceFactionId, out _) || !world.TryGetFaction(relationship.TargetFactionId, out _)) issues.Add(new SocietyValidationIssue("relationship.unknown-faction", "Relationship references an unknown faction.", true, relationship.SourceFactionId));
            if (relationship.SourceFactionId == relationship.TargetFactionId) issues.Add(new SocietyValidationIssue("relationship.self", "Faction relationship cannot target itself.", true, relationship.SourceFactionId));
        }
    }

    private static void ValidateTrade(SocietyWorldState world, ICollection<SocietyValidationIssue> issues)
    {
        foreach (TradeRouteRuntimeState route in world.TradeRoutes)
        {
            if (!world.TryGetSettlement(route.ExporterSettlementId, out SettlementState? exporter) || exporter is null || !world.TryGetSettlement(route.ImporterSettlementId, out SettlementState? importer) || importer is null) { issues.Add(new SocietyValidationIssue("trade.unknown-settlement", "Trade route references an unknown settlement.", true, route.Id)); continue; }
            if (route.ExporterSettlementId == route.ImporterSettlementId) issues.Add(new SocietyValidationIssue("trade.self-route", "Trade route exports to the same settlement.", true, route.Id));
            if (!exporter.Stocks.ContainsKey(route.Commodity) || !importer.Stocks.ContainsKey(route.Commodity)) issues.Add(new SocietyValidationIssue("trade.missing-stock", $"Trade route commodity {route.Commodity} is not represented at both endpoints.", true, route.Id));
        }
        foreach (TradeShipmentState shipment in world.Shipments)
        {
            if (shipment.DeliveredMilliunits + shipment.LostMilliunits > shipment.LoadedMilliunits) issues.Add(new SocietyValidationIssue("shipment.nonconservative", "Shipment delivered and lost more than it loaded.", true, shipment.Id));
        }
    }

    private static void ValidateLeadership(SocietyWorldState world, ICollection<SocietyValidationIssue> issues)
    {
        foreach (FactionState faction in world.Factions)
        {
            LeadershipState leadership;
            try { leadership = world.GetLeadership(faction.Id); }
            catch (KeyNotFoundException) { issues.Add(new SocietyValidationIssue("leadership.missing", "Faction has no leadership state.", true, faction.Id)); continue; }
            if (leadership.HighestOffice is null && !leadership.Model.AllowsVacancy) issues.Add(new SocietyValidationIssue("leadership.no-successor", "Leadership model forbids vacancy but highest office is absent.", true, faction.Id));
        }
    }

    private static void ValidateCrimes(SocietyWorldState world, ICollection<SocietyValidationIssue> issues)
    {
        foreach (CrimeRecordState crime in world.Crimes)
        {
            if (!world.TryGetFaction(crime.JurisdictionFactionId, out _)) issues.Add(new SocietyValidationIssue("crime.unknown-jurisdiction", "Crime references an unknown jurisdiction.", true, crime.Id));
            if (crime.Status >= CrimeStatus.Reported && crime.WitnessIds.Count == 0 && crime.EvidenceIds.Count == 0) issues.Add(new SocietyValidationIssue("crime.omniscient-report", "Reported crime has no witness or evidence source.", true, crime.Id));
        }
    }

    private static void ValidateConflicts(SocietyWorldState world, ICollection<SocietyValidationIssue> issues)
    {
        foreach (StrategicConflictState conflict in world.Conflicts)
        {
            if (conflict.AggressorFactionId == conflict.DefenderFactionId) issues.Add(new SocietyValidationIssue("conflict.self", "Faction conflict cannot target itself.", true, conflict.Id));
            if (!world.TryGetFaction(conflict.AggressorFactionId, out _) || !world.TryGetFaction(conflict.DefenderFactionId, out _)) issues.Add(new SocietyValidationIssue("conflict.unknown-faction", "Conflict references an unknown faction.", true, conflict.Id));
        }
    }
}

public sealed class SocietyRepairEngine
{
    public SocietyValidationReport Repair(SocietyWorldState world, IReadOnlySet<string>? availableWorldTags = null, int maximumPasses = 4)
    {
        if (maximumPasses < 1) throw new ArgumentOutOfRangeException(nameof(maximumPasses)); int repairs = 0; var validator = new SocietyValidator();
        for (int pass = 0; pass < maximumPasses; pass++)
        {
            SocietyValidationReport report = validator.Validate(world, availableWorldTags); if (report.IsValid) return report with { RepairCount = repairs }; bool changed = false;
            foreach (SocietyValidationIssue issue in report.Issues.Where(static value => value.IsCritical))
            {
                switch (issue.Code)
                {
                    case "leadership.no-successor":
                        LeadershipState leadership = world.GetLeadership(issue.SubjectId); leadership.TryInstallLeader(StableIdFactory.CreateChild(issue.SubjectId, "repair-leader", pass), world.Day); repairs++; changed = true; break;
                    case "crime.omniscient-report":
                        world.GetCrime(issue.SubjectId).Resolve(forgiven: true); repairs++; changed = true; break;
                }
            }
            if (!changed) return report with { RepairCount = repairs };
        }
        SocietyValidationReport final = validator.Validate(world, availableWorldTags); return final with { RepairCount = repairs };
    }
}
