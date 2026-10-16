using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public sealed record SocietyTickResult(ulong Day, int ShipmentsDeparted, int ShipmentsResolved, int CrimesProcessed, int LeadershipChanges, int ConflictsChanged, StateChecksum Checksum);

public sealed class SocietySimulationEngine
{
    public const int SimulationVersion = 15;
    private readonly CampaignSeed campaignSeed;
    public SocietySimulationEngine(CampaignSeed campaignSeed) => this.campaignSeed = campaignSeed;

    public SocietyTickResult AdvanceDay(SocietyWorldState world)
    {
        ArgumentNullException.ThrowIfNull(world); ulong day = checked(world.Day + 1); int shipmentsResolved = ResolveShipments(world, day); PruneResolvedShipments(world, day); OperateServices(world, day); ApplyProductionAndConsumption(world, day); int shipmentsDeparted = DispatchShipments(world, day); ApplyPopulation(world, day); int leadershipChanges = ResolveLeadership(world, day); int conflictsChanged = ResolveConflicts(world, day); int crimesProcessed = ProcessCrimes(world, day); ApplyReputationEvidence(world, day); world.AdvanceDay();
        return new SocietyTickResult(day, shipmentsDeparted, shipmentsResolved, crimesProcessed, leadershipChanges, conflictsChanged, world.ComputeChecksum());
    }

    public IReadOnlyList<SocietyTickResult> AdvanceDays(SocietyWorldState world, int days)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days)); var results = new SocietyTickResult[days]; for (int index = 0; index < days; index++) results[index] = AdvanceDay(world); return results;
    }

    private static void OperateServices(SocietyWorldState world, ulong day)
    {
        foreach (SettlementState settlement in world.Settlements.OrderBy(static value => value.Id))
        {
            foreach (SettlementServiceState service in settlement.Services.OrderBy(static value => value.Id))
            {
                bool inputsAvailable = true;
                foreach ((SocietyCommodity commodity, long amount) in service.RequiredInputs.OrderBy(static value => value.Key))
                {
                    if (!settlement.TryGetStock(commodity, out ResourceStockState? stock) || stock is null || stock.QuantityMilliunits < amount) { inputsAvailable = false; break; }
                }
                if (inputsAvailable)
                {
                    foreach ((SocietyCommodity commodity, long amount) in service.RequiredInputs.OrderBy(static value => value.Key)) _ = settlement.GetStock(commodity).Consume(amount);
                }
                service.SetOperating(inputsAvailable && service.StaffingPermille > 100 && service.DamagePermille < 900);
                int demand = service.Kind switch
                {
                    SettlementServiceKind.Food or SettlementServiceKind.CleanWater => settlement.Population.TotalPopulation,
                    SettlementServiceKind.MedicalCare or SettlementServiceKind.ContaminationTreatment => settlement.Population.TotalPopulation / 3,
                    SettlementServiceKind.Security => settlement.Population.TotalPopulation / 2,
                    _ => settlement.Population.TotalPopulation / 5
                };
                service.SetQueuePressure(service.EffectiveCapacity <= 0 ? 2000 : Math.Clamp(demand * 1000 / Math.Max(1, service.EffectiveCapacity), 0, 2000));
                if (!service.Operating) world.RecordEvent(Event(world, SocietyEventKind.ServiceChanged, day, settlement.Id, service.Id, 0, $"{service.Kind} service is unavailable"));
            }
        }
    }

    private static void ApplyProductionAndConsumption(SocietyWorldState world, ulong day)
    {
        foreach (SettlementState settlement in world.Settlements.OrderBy(static value => value.Id))
        {
            foreach (ResourceProcessState process in settlement.Processes.OrderBy(static value => value.Id))
            {
                if (!settlement.TryGetStock(process.Commodity, out ResourceStockState? stock) || stock is null) continue;
                int serviceEfficiency = ResolveServiceEfficiency(settlement, process.RequiredServiceTag); long amount = process.AmountPerDayMilliunits * Math.Clamp(process.EfficiencyPermille, 0, 1000) / 1000 * serviceEfficiency / 1000;
                if (process.Production)
                {
                    long accepted = stock.Add(amount); if (accepted > 0) world.RecordEvent(Event(world, SocietyEventKind.Production, day, settlement.Id, StableId.Empty, accepted, $"Produced {accepted} milliunits of {process.Commodity}"));
                }
                else
                {
                    long demandMultiplier = Math.Max(1, settlement.Population.TotalPopulation) * 1000L / Math.Max(1, settlement.Plan.PopulationCapacity); long requested = Math.Max(0, amount * Math.Max(500, demandMultiplier) / 1000); long consumed = stock.Consume(requested);
                    world.RecordEvent(Event(world, SocietyEventKind.Consumption, day, settlement.Id, StableId.Empty, consumed, $"Consumed {consumed} milliunits of {process.Commodity}"));
                    if (consumed < requested) { settlement.AdjustStability(-25); settlement.Population.AdjustMigrationPressure(30); foreach (PopulationCohortState cohort in settlement.Population.Cohorts.Values) { cohort.AdjustHealth(-5); cohort.AdjustMorale(-8); } }
                }
            }
            ApplySettlementNeeds(settlement);
        }
    }

    private static int ResolveServiceEfficiency(SettlementState settlement, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return 1000;
        SettlementServiceKind? kind = tag switch { "medical" => SettlementServiceKind.MedicalCare, "diagnostic" => SettlementServiceKind.DiagnosticAnalysis, "water" => SettlementServiceKind.CleanWater, "repair" => SettlementServiceKind.Repair, "transport" => SettlementServiceKind.Transport, "security" => SettlementServiceKind.Security, "market" => SettlementServiceKind.Market, "food" => SettlementServiceKind.Food, _ => null };
        if (!kind.HasValue) return 1000; SettlementServiceState? service = settlement.Services.FirstOrDefault(value => value.Kind == kind.Value); return service?.Operating == true ? Math.Clamp(service.QualityPermille * service.StaffingPermille / 1000 * (1000 - service.DamagePermille) / 1000, 100, 1000) : 100;
    }

    private static void ApplySettlementNeeds(SettlementState settlement)
    {
        int foodScarcity = settlement.ScarcityPermille(SocietyCommodity.Food); int waterScarcity = settlement.ScarcityPermille(SocietyCommodity.Water); int medicineScarcity = settlement.ScarcityPermille(SocietyCommodity.Medicine); int average = (foodScarcity + waterScarcity + medicineScarcity) / 3;
        if (average > 600) settlement.AdjustStability(-15); else if (average < 200) settlement.AdjustStability(5); settlement.SetMerchantRisk(Math.Clamp(settlement.MerchantRiskPermille + average / 40 - 8, 0, 1000));
    }

    private int DispatchShipments(SocietyWorldState world, ulong day)
    {
        int count = 0;
        foreach (TradeRouteRuntimeState route in world.TradeRoutes.Where(static value => value.Active).OrderBy(static value => value.Id))
        {
            SettlementState exporter = world.GetSettlement(route.ExporterSettlementId); SettlementState importer = world.GetSettlement(route.ImporterSettlementId);
            if (!exporter.TryGetStock(route.Commodity, out ResourceStockState? source) || source is null || !importer.TryGetStock(route.Commodity, out ResourceStockState? destination) || destination is null) continue;
            long reserve = source.TargetMilliunits * 3 / 5; long available = Math.Max(0, source.QuantityMilliunits - reserve); long capacityAvailable = Math.Max(0, destination.StorageCapacityMilliunits - destination.QuantityMilliunits); long load = Math.Min(route.DailyCapacityMilliunits, Math.Min(available, capacityAvailable)); if (load <= 0) continue;
            long loaded = source.Consume(load); if (loaded <= 0) continue; StableId shipmentId = StableIdFactory.CreateChild(route.Id, "shipment", checked((long)day)); var shipment = new TradeShipmentState(shipmentId, route.Id, route.ExporterSettlementId, route.ImporterSettlementId, route.Commodity, loaded, day, checked(day + (ulong)route.TravelDays));
            int relationRisk = ResolveRelationRisk(world, exporter.ControllingFactionId, importer.ControllingFactionId); int risk = Math.Clamp(route.BaseRiskPermille + relationRisk - route.EscortPermille / 2 + exporter.MerchantRiskPermille / 4, 0, 950); shipment.SetRisk(risk); world.AddShipment(shipment); world.RecordEvent(Event(world, SocietyEventKind.ShipmentDeparted, day, exporter.Id, importer.Id, loaded, $"Shipment {shipment.Id} departed with {loaded} milliunits of {route.Commodity}")); count++;
        }
        return count;
    }

    private int ResolveShipments(SocietyWorldState world, ulong day)
    {
        int count = 0;
        foreach (TradeShipmentState shipment in world.Shipments.Where(value => !value.Resolved && value.ArrivalDay <= day).OrderBy(static value => value.Id).ToArray())
        {
            int roll = RollPermille(shipment.Id, day, "shipment.loss"); int effectiveLossPermille = roll < shipment.RiskPermille ? Math.Clamp(shipment.RiskPermille / 2 + RollPermille(shipment.Id, day, "shipment.loss.amount") / 2, 0, 1000) : 0;
            long lost = shipment.LoadedMilliunits * effectiveLossPermille / 1000; long delivered = shipment.LoadedMilliunits - lost; SettlementState importer = world.GetSettlement(shipment.ImporterSettlementId); long accepted = importer.GetStock(shipment.Commodity).Add(delivered); long overflowLoss = delivered - accepted; lost += overflowLoss; shipment.Resolve(accepted, lost);
            SocietyEventKind kind = lost > 0 ? SocietyEventKind.ShipmentLost : SocietyEventKind.ShipmentDelivered; world.RecordEvent(Event(world, kind, day, shipment.ExporterSettlementId, shipment.ImporterSettlementId, accepted, $"Shipment {shipment.Id} delivered {accepted} and lost {lost} milliunits of {shipment.Commodity}"));
            if (world.TryGetRelationship(world.GetSettlement(shipment.ExporterSettlementId).ControllingFactionId, importer.ControllingFactionId, out FactionRelationshipState? relationship) && relationship is not null) relationship.Adjust(trust: accepted > 0 ? 8 : -15, tradeReliance: accepted > 0 ? 10 : -5, grievance: lost > accepted ? 4 : 0);
            count++;
        }
        return count;
    }

    private static void PruneResolvedShipments(SocietyWorldState world, ulong day)
    {
        foreach (StableId id in world.Shipments.Where(value => value.Resolved && value.ArrivalDay + 30UL < day).Select(static value => value.Id).Order().ToArray())
        {
            world.RemoveResolvedShipment(id);
        }
    }

    private static int ResolveRelationRisk(SocietyWorldState world, StableId sourceFactionId, StableId targetFactionId)
    {
        if (!world.TryGetRelationship(sourceFactionId, targetFactionId, out FactionRelationshipState? relationship) || relationship is null) return 100; return relationship.DerivedKind switch { FactionRelationKind.Allied => -120, FactionRelationKind.Cooperative => -60, FactionRelationKind.Neutral => 0, FactionRelationKind.Suspicious => 80, FactionRelationKind.Rival => 160, FactionRelationKind.Hostile => 260, FactionRelationKind.AtWar => 500, FactionRelationKind.Embargoed => 600, _ => 100 };
    }

    private static void ApplyPopulation(SocietyWorldState world, ulong day)
    {
        foreach (SettlementState settlement in world.Settlements.OrderBy(static value => value.Id))
        {
            PopulationState population = settlement.Population; int scarcity = (settlement.ScarcityPermille(SocietyCommodity.Food) + settlement.ScarcityPermille(SocietyCommodity.Water)) / 2; int pressure = population.OvercrowdingPermille / 3 + scarcity / 2 + (1000 - settlement.StabilityPermille) / 3 - settlement.AccessibilityPermille / 8; population.AdjustMigrationPressure(pressure / 20 - 15);
            if (population.MigrationPressurePermille >= 500 && day % 7 == 0)
            {
                PopulationCohortState? workers = population.Cohorts.GetValueOrDefault(PopulationCohortKind.WorkingAdults); int leaving = Math.Min(workers?.Count ?? 0, Math.Max(1, population.TotalPopulation / 100)); workers?.AdjustCount(-leaving); world.RecordEvent(Event(world, SocietyEventKind.Migration, day, settlement.Id, StableId.Empty, -leaving, $"{leaving} residents departed {settlement.Plan.DisplayName}"));
            }
            else if (population.MigrationPressurePermille <= -350 && population.TotalPopulation < population.HousingCapacity && day % 7 == 0)
            {
                PopulationCohortState? refugees = population.Cohorts.GetValueOrDefault(PopulationCohortKind.Refugees); if (refugees is null) { refugees = new PopulationCohortState(PopulationCohortKind.Refugees, 0); population.Upsert(refugees); } int arriving = Math.Max(1, population.HousingCapacity / 100); refugees.AdjustCount(arriving); world.RecordEvent(Event(world, SocietyEventKind.Migration, day, settlement.Id, StableId.Empty, arriving, $"{arriving} migrants arrived at {settlement.Plan.DisplayName}"));
            }
        }
    }

    private static int ResolveLeadership(SocietyWorldState world, ulong day)
    {
        int changes = 0;
        foreach (LeadershipState leadership in world.Leadership.OrderBy(static value => value.FactionId))
        {
            FactionOfficeState? highest = leadership.HighestOffice; bool requiresSelection = highest is null || highest.Vacant || (highest.TermEndsDay != ulong.MaxValue && highest.TermEndsDay <= day); if (!requiresSelection) continue;
            StableId successor = SelectSuccessor(leadership, day); if (successor.IsEmpty && !leadership.Model.AllowsVacancy) successor = StableIdFactory.CreateChild(leadership.FactionId, "emergency-successor", checked((long)day)); if (!successor.IsEmpty) { leadership.TryInstallLeader(successor, day); world.GetFaction(leadership.FactionId).AdjustLegitimacy(highest is null || highest.Vacant ? -20 : 5); world.RecordEvent(Event(world, SocietyEventKind.LeadershipChanged, day, leadership.FactionId, successor, 0, $"Leadership changed under {leadership.Model.SelectionMethod}")); changes++; }
        }
        return changes;
    }

    private static StableId SelectSuccessor(LeadershipState leadership, ulong day)
    {
        InternalDivisionState? division = leadership.Divisions.OrderByDescending(static value => value.InfluencePermille * Math.Max(1, value.LoyaltyPermille)).ThenBy(static value => value.Id).FirstOrDefault(); return division?.LeaderActorId ?? StableIdFactory.CreateChild(leadership.FactionId, "successor", checked((long)day));
    }

    private int ResolveConflicts(SocietyWorldState world, ulong day)
    {
        int changes = 0;
        foreach (StrategicConflictState conflict in world.Conflicts.OrderBy(static value => value.Id))
        {
            if (conflict.ReservedForPlayableEncounter || conflict.Stage == StrategicConflictStage.Settlement) continue; int aggressorRoll = conflict.AggressorStrengthPermille + RollPermille(conflict.Id, day, "conflict.aggressor") / 3; int defenderRoll = conflict.DefenderStrengthPermille + RollPermille(conflict.Id, day, "conflict.defender") / 3;
            StrategicConflictStage next = conflict.Stage; if (Math.Abs(aggressorRoll - defenderRoll) < 80) next = conflict.Stage < StrategicConflictStage.Ceasefire ? (StrategicConflictStage)((int)conflict.Stage + 1) : StrategicConflictStage.Settlement; else if (aggressorRoll > defenderRoll + 250) next = conflict.Stage < StrategicConflictStage.SiegeOrBlockade ? (StrategicConflictStage)((int)conflict.Stage + 1) : StrategicConflictStage.Ceasefire; else if (defenderRoll > aggressorRoll + 250) next = conflict.Stage > StrategicConflictStage.Dispute ? (StrategicConflictStage)((int)conflict.Stage - 1) : StrategicConflictStage.Settlement;
            if (next != conflict.Stage) { conflict.Advance(next, day); world.RecordEvent(Event(world, SocietyEventKind.ConflictChanged, day, conflict.AggressorFactionId, conflict.DefenderFactionId, (long)next, $"Conflict advanced to {next}")); changes++; }
        }
        return changes;
    }

    private static int ProcessCrimes(SocietyWorldState world, ulong day)
    {
        int processed = 0;
        foreach (CrimeRecordState crime in world.Crimes.OrderBy(static value => value.Id))
        {
            if (crime.Status == CrimeStatus.Unreported && (crime.WitnessIds.Count > 0 || crime.EvidenceIds.Count > 0)) { crime.Report(); world.RecordEvent(Event(world, SocietyEventKind.CrimeReported, day, crime.JurisdictionFactionId, crime.SuspectedActorId, crime.SeverityPermille, $"{crime.Kind} was reported")); processed++; }
            if (crime.Status == CrimeStatus.Reported && crime.IdentityConfidencePermille >= 350)
            {
                crime.IssueWarrant(Math.Max(10, crime.SeverityPermille * 3));
                int reliability = Math.Clamp(crime.IdentityConfidencePermille, 0, 1000);
                int publicity = Math.Clamp(500 + crime.SeverityPermille / 2, 0, 1000);
                world.AddReputationEvidence(new ReputationEvidenceState(StableIdFactory.CreateChild(crime.Id, "reputation", "known-crime"), crime.SuspectedActorId, crime.JurisdictionFactionId, crime.Id, ReputationDimension.KnownCrimeSeverity, Math.Max(20, crime.SeverityPermille / 5), reliability, publicity, day, $"Warrant issued for {crime.Kind}."));
                world.AddReputationEvidence(new ReputationEvidenceState(StableIdFactory.CreateChild(crime.Id, "reputation", "trust"), crime.SuspectedActorId, crime.JurisdictionFactionId, crime.Id, ReputationDimension.Trust, -Math.Max(10, crime.SeverityPermille / 8), reliability, publicity, day, $"Reported responsibility for {crime.Kind}."));
                world.RecordEvent(Event(world, SocietyEventKind.WarrantIssued, day, crime.JurisdictionFactionId, crime.SuspectedActorId, crime.FineMinorUnits, $"Warrant issued for {crime.Kind}")); processed++;
            }
        }
        return processed;
    }

    private static void ApplyReputationEvidence(SocietyWorldState world, ulong day)
    {
        foreach (ReputationEvidenceState evidence in world.ReputationEvidence.Where(value => !value.Applied && value.CreatedDay <= day).OrderBy(static value => value.Id))
        {
            int applied = evidence.Delta * Math.Clamp(evidence.ReliabilityPermille, 0, 1000) / 1000 * Math.Clamp(evidence.PublicityPermille, 0, 1000) / 1000;
            if (applied != 0)
            {
                FactionReputationState reputation = world.GetOrCreateReputation(evidence.SubjectId, evidence.FactionId);
                reputation.Adjust(evidence.Dimension, applied);
                world.RecordEvent(Event(world, SocietyEventKind.ReputationChanged, day, evidence.SubjectId, evidence.FactionId, applied, evidence.Reason));
            }
            evidence.MarkApplied(day);
        }
    }

    private int RollPermille(StableId id, ulong day, string domain)
    {
        var writer = new CanonicalWriter(); writer.WriteString("roentgen.phase15.simulation-roll"); writer.WriteInt32(SimulationVersion); writer.WriteUInt64(campaignSeed.Value); writer.WriteStableId(id); writer.WriteUInt64(day); writer.WriteString(domain); return (int)(writer.ComputeChecksum().A % 1000UL);
    }
    private static SocietyEventRecord Event(SocietyWorldState world, SocietyEventKind kind, ulong day, StableId primary, StableId secondary, long amount, string description) => new(StableIdFactory.CreateChild(primary.IsEmpty ? StableIdFactory.Create("society-event", kind.ToString()) : primary, "society-event", $"{day}:{kind}:{world.Revision}:{world.Events.Count}"), kind, day, primary, secondary, amount, description);
}

public sealed class DisguiseEvaluator
{
    public DisguiseObservationState Evaluate(DisguiseIdentityState disguise, StableId observerId, StableId actualFactionId, int observerPerceptionPermille, int restrictedAreaPermille, int behaviorMismatchPermille, ulong day)
    {
        ArgumentNullException.ThrowIfNull(disguise); int equipmentSupport = Math.Min(300, disguise.EquipmentTags.Count * 80); int credentialSupport = Math.Min(350, disguise.CredentialTags.Count * 120); int faceSupport = disguise.FaceCovered ? 120 : 0; int presentation = disguise.QualityPermille + equipmentSupport + credentialSupport + faceSupport; int scrutiny = Math.Clamp(observerPerceptionPermille, 0, 1000) + Math.Clamp(restrictedAreaPermille, 0, 1000) / 2 + Math.Clamp(behaviorMismatchPermille, 0, 1000);
        int margin = presentation - scrutiny; DisguiseEvaluation evaluation = margin >= 500 ? DisguiseEvaluation.Accepted : margin >= 150 ? DisguiseEvaluation.Questioned : margin >= -150 ? DisguiseEvaluation.Suspicious : DisguiseEvaluation.Exposed; StableId believed = evaluation == DisguiseEvaluation.Exposed ? actualFactionId : disguise.PresentedFactionId; int confidence = Math.Clamp(Math.Abs(margin), 100, 1000); StableId id = StableIdFactory.CreateChild(disguise.Id, "observation", $"{observerId}:{day}"); return new DisguiseObservationState(id, observerId, disguise.ActorId, believed, confidence, evaluation, day, false);
    }
}
