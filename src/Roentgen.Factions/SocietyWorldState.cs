using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public readonly record struct FactionRelationshipKey(StableId SourceFactionId, StableId TargetFactionId) : IComparable<FactionRelationshipKey>
{
    public int CompareTo(FactionRelationshipKey other)
    {
        int source = SourceFactionId.CompareTo(other.SourceFactionId);
        return source != 0 ? source : TargetFactionId.CompareTo(other.TargetFactionId);
    }
}

public readonly record struct ReputationKey(StableId SubjectId, StableId FactionId) : IComparable<ReputationKey>
{
    public int CompareTo(ReputationKey other)
    {
        int subject = SubjectId.CompareTo(other.SubjectId);
        return subject != 0 ? subject : FactionId.CompareTo(other.FactionId);
    }
}

public sealed class SocietyWorldState : ICanonicalWritable
{
    public const int SchemaVersion = 15;
    private readonly SortedDictionary<StableId, FactionState> factions = [];
    private readonly SortedDictionary<StableId, SettlementState> settlements = [];
    private readonly SortedDictionary<FactionRelationshipKey, FactionRelationshipState> relationships = [];
    private readonly SortedDictionary<StableId, TerritoryClaimState> territoryClaims = [];
    private readonly SortedDictionary<StableId, LeadershipState> leadership = [];
    private readonly SortedDictionary<StableId, TradeRouteRuntimeState> tradeRoutes = [];
    private readonly SortedDictionary<StableId, TradeShipmentState> shipments = [];
    private readonly SortedDictionary<ReputationKey, FactionReputationState> reputations = [];
    private readonly SortedDictionary<StableId, ReputationEvidenceState> reputationEvidence = [];
    private readonly SortedDictionary<StableId, CrimeRecordState> crimes = [];
    private readonly SortedDictionary<StableId, DisguiseIdentityState> disguises = [];
    private readonly SortedDictionary<StableId, DisguiseObservationState> disguiseObservations = [];
    private readonly SortedDictionary<StableId, StrategicConflictState> conflicts = [];
    private readonly Queue<SocietyEventRecord> events = new();

    public IReadOnlyCollection<FactionState> Factions => factions.Values;
    public IReadOnlyCollection<SettlementState> Settlements => settlements.Values;
    public IReadOnlyCollection<FactionRelationshipState> Relationships => relationships.Values;
    public IReadOnlyCollection<TerritoryClaimState> TerritoryClaims => territoryClaims.Values;
    public IReadOnlyCollection<LeadershipState> Leadership => leadership.Values;
    public IReadOnlyCollection<TradeRouteRuntimeState> TradeRoutes => tradeRoutes.Values;
    public IReadOnlyCollection<TradeShipmentState> Shipments => shipments.Values;
    public IReadOnlyCollection<FactionReputationState> Reputations => reputations.Values;
    public IReadOnlyCollection<ReputationEvidenceState> ReputationEvidence => reputationEvidence.Values;
    public IReadOnlyCollection<CrimeRecordState> Crimes => crimes.Values;
    public IReadOnlyCollection<DisguiseIdentityState> Disguises => disguises.Values;
    public IReadOnlyCollection<DisguiseObservationState> DisguiseObservations => disguiseObservations.Values;
    public IReadOnlyCollection<StrategicConflictState> Conflicts => conflicts.Values;
    public IReadOnlyCollection<SocietyEventRecord> Events => events.ToArray();
    public ulong Day { get; private set; }
    public ulong Revision { get; private set; }

    public void AddFaction(FactionState faction) { ArgumentNullException.ThrowIfNull(faction); if (!factions.TryAdd(faction.Id, faction)) throw new InvalidOperationException($"Faction '{faction.Id}' already exists."); Revision++; }
    public void AddSettlement(SettlementState settlement) { ArgumentNullException.ThrowIfNull(settlement); if (!settlements.TryAdd(settlement.Id, settlement)) throw new InvalidOperationException($"Settlement '{settlement.Id}' already exists."); Revision++; }
    public void AddRelationship(FactionRelationshipState relationship) { ArgumentNullException.ThrowIfNull(relationship); relationships[new FactionRelationshipKey(relationship.SourceFactionId, relationship.TargetFactionId)] = relationship; Revision++; }
    public void AddTerritoryClaim(TerritoryClaimState claim) { territoryClaims[claim.Id] = claim; Revision++; }
    public void AddLeadership(LeadershipState state) { leadership[state.FactionId] = state; Revision++; }
    public void AddTradeRoute(TradeRouteRuntimeState route) { tradeRoutes[route.Id] = route; Revision++; }
    public void AddShipment(TradeShipmentState shipment) { if (!shipments.TryAdd(shipment.Id, shipment)) throw new InvalidOperationException($"Shipment '{shipment.Id}' already exists."); Revision++; }
    public void AddReputation(FactionReputationState reputation) { reputations[new ReputationKey(reputation.SubjectId, reputation.FactionId)] = reputation; Revision++; }
    public void AddReputationEvidence(ReputationEvidenceState evidence) { reputationEvidence[evidence.Id] = evidence; Revision++; }
    public void AddCrime(CrimeRecordState crime) { crimes[crime.Id] = crime; Revision++; }
    public void AddDisguise(DisguiseIdentityState disguise) { disguises[disguise.Id] = disguise; Revision++; }
    public void AddDisguiseObservation(DisguiseObservationState observation) { disguiseObservations[observation.Id] = observation; Revision++; }
    public void AddConflict(StrategicConflictState conflict) { conflicts[conflict.Id] = conflict; Revision++; }
    public void RemoveResolvedShipment(StableId id) { if (shipments.Remove(id)) Revision++; }
    public void TransferTerritoryControl(StableId locationId, StableId previousFactionId, StableId newFactionId, StableId supplySettlementId)
    {
        bool replaced = false;
        foreach (StableId claimId in territoryClaims.Values.Where(value => value.LocationId == locationId && value.Kind == TerritoryAuthorityKind.Control && value.FactionId == previousFactionId).Select(static value => value.Id).Order().ToArray())
        {
            TerritoryClaimState current = territoryClaims[claimId];
            territoryClaims[claimId] = current with { FactionId = newFactionId, SupplySettlementId = supplySettlementId, LastChangedDay = Day };
            replaced = true;
        }
        if (!replaced)
        {
            StableId id = StableIdFactory.CreateChild(locationId, "territory", "control");
            territoryClaims[id] = new TerritoryClaimState(id, locationId, newFactionId, TerritoryAuthorityKind.Control, 650, supplySettlementId, Day);
        }
        Revision++;
    }
    public void AdvanceDay() { Day++; Revision++; }
    public void SetDay(ulong day) { Day = day; Revision++; }
    public void Touch() { Revision++; }

    public FactionState GetFaction(StableId id) => factions.TryGetValue(id, out FactionState? value) ? value : throw new KeyNotFoundException($"Unknown faction '{id}'.");
    public bool TryGetFaction(StableId id, out FactionState? value) => factions.TryGetValue(id, out value);
    public SettlementState GetSettlement(StableId id) => settlements.TryGetValue(id, out SettlementState? value) ? value : throw new KeyNotFoundException($"Unknown settlement '{id}'.");
    public bool TryGetSettlement(StableId id, out SettlementState? value) => settlements.TryGetValue(id, out value);
    public FactionRelationshipState GetRelationship(StableId source, StableId target) => relationships.TryGetValue(new FactionRelationshipKey(source, target), out FactionRelationshipState? value) ? value : throw new KeyNotFoundException($"Unknown relationship '{source}' -> '{target}'.");
    public bool TryGetRelationship(StableId source, StableId target, out FactionRelationshipState? value) => relationships.TryGetValue(new FactionRelationshipKey(source, target), out value);
    public LeadershipState GetLeadership(StableId factionId) => leadership.TryGetValue(factionId, out LeadershipState? value) ? value : throw new KeyNotFoundException($"Unknown leadership state for faction '{factionId}'.");
    public TradeRouteRuntimeState GetTradeRoute(StableId id) => tradeRoutes.TryGetValue(id, out TradeRouteRuntimeState? value) ? value : throw new KeyNotFoundException($"Unknown trade route '{id}'.");
    public TradeShipmentState GetShipment(StableId id) => shipments.TryGetValue(id, out TradeShipmentState? value) ? value : throw new KeyNotFoundException($"Unknown shipment '{id}'.");
    public CrimeRecordState GetCrime(StableId id) => crimes.TryGetValue(id, out CrimeRecordState? value) ? value : throw new KeyNotFoundException($"Unknown crime '{id}'.");
    public StrategicConflictState GetConflict(StableId id) => conflicts.TryGetValue(id, out StrategicConflictState? value) ? value : throw new KeyNotFoundException($"Unknown conflict '{id}'.");
    public FactionReputationState GetOrCreateReputation(StableId subjectId, StableId factionId)
    {
        var key = new ReputationKey(subjectId, factionId); if (reputations.TryGetValue(key, out FactionReputationState? existing)) return existing; var created = new FactionReputationState(subjectId, factionId); reputations.Add(key, created); Revision++; return created;
    }
    public void RecordEvent(SocietyEventRecord value)
    {
        events.Enqueue(value); while (events.Count > 512) _ = events.Dequeue(); Revision++;
    }

    public byte[] ToCanonicalPayload() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ToArray(); }
    public StateChecksum ComputeChecksum() { var writer = new CanonicalWriter(); WriteCanonical(writer); return writer.ComputeChecksum(); }
    public void RestoreFrom(ReadOnlyMemory<byte> payload)
    {
        SocietyWorldState source = ReadCanonical(payload); factions.Clear(); foreach ((StableId id, FactionState value) in source.factions) factions.Add(id, value); settlements.Clear(); foreach ((StableId id, SettlementState value) in source.settlements) settlements.Add(id, value); relationships.Clear(); foreach ((FactionRelationshipKey key, FactionRelationshipState value) in source.relationships) relationships.Add(key, value); territoryClaims.Clear(); foreach ((StableId id, TerritoryClaimState value) in source.territoryClaims) territoryClaims.Add(id, value); leadership.Clear(); foreach ((StableId id, LeadershipState value) in source.leadership) leadership.Add(id, value); tradeRoutes.Clear(); foreach ((StableId id, TradeRouteRuntimeState value) in source.tradeRoutes) tradeRoutes.Add(id, value); shipments.Clear(); foreach ((StableId id, TradeShipmentState value) in source.shipments) shipments.Add(id, value); reputations.Clear(); foreach ((ReputationKey key, FactionReputationState value) in source.reputations) reputations.Add(key, value); reputationEvidence.Clear(); foreach ((StableId id, ReputationEvidenceState value) in source.reputationEvidence) reputationEvidence.Add(id, value); crimes.Clear(); foreach ((StableId id, CrimeRecordState value) in source.crimes) crimes.Add(id, value); disguises.Clear(); foreach ((StableId id, DisguiseIdentityState value) in source.disguises) disguises.Add(id, value); disguiseObservations.Clear(); foreach ((StableId id, DisguiseObservationState value) in source.disguiseObservations) disguiseObservations.Add(id, value); conflicts.Clear(); foreach ((StableId id, StrategicConflictState value) in source.conflicts) conflicts.Add(id, value); events.Clear(); foreach (SocietyEventRecord value in source.events) events.Enqueue(value); Day = source.Day; Revision = source.Revision;
    }
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32(SchemaVersion); writer.WriteUInt64(Day); writer.WriteCount(factions.Count); foreach (FactionState value in factions.Values) value.WriteCanonical(writer); writer.WriteCount(settlements.Count); foreach (SettlementState value in settlements.Values) value.WriteCanonical(writer);
        writer.WriteCount(relationships.Count); foreach (FactionRelationshipState value in relationships.Values) value.WriteCanonical(writer); writer.WriteCount(territoryClaims.Count); foreach (TerritoryClaimState value in territoryClaims.Values) value.WriteCanonical(writer); writer.WriteCount(leadership.Count); foreach (LeadershipState value in leadership.Values) value.WriteCanonical(writer);
        writer.WriteCount(tradeRoutes.Count); foreach (TradeRouteRuntimeState value in tradeRoutes.Values) value.WriteCanonical(writer); writer.WriteCount(shipments.Count); foreach (TradeShipmentState value in shipments.Values) value.WriteCanonical(writer); writer.WriteCount(reputations.Count); foreach (FactionReputationState value in reputations.Values) value.WriteCanonical(writer); writer.WriteCount(reputationEvidence.Count); foreach (ReputationEvidenceState value in reputationEvidence.Values) value.WriteCanonical(writer);
        writer.WriteCount(crimes.Count); foreach (CrimeRecordState value in crimes.Values) value.WriteCanonical(writer); writer.WriteCount(disguises.Count); foreach (DisguiseIdentityState value in disguises.Values) value.WriteCanonical(writer); writer.WriteCount(disguiseObservations.Count); foreach (DisguiseObservationState value in disguiseObservations.Values) value.WriteCanonical(writer); writer.WriteCount(conflicts.Count); foreach (StrategicConflictState value in conflicts.Values) value.WriteCanonical(writer); writer.WriteCount(events.Count); foreach (SocietyEventRecord value in events) value.WriteCanonical(writer); writer.WriteUInt64(Revision);
    }

    public static SocietyWorldState ReadCanonical(ReadOnlyMemory<byte> payload)
    {
        var reader = new CanonicalReader(payload); SocietyWorldState result = ReadCanonical(reader); reader.EnsureComplete(); return result;
    }
    public static SocietyWorldState ReadCanonical(CanonicalReader reader)
    {
        int schema = reader.ReadInt32(); if (schema != SchemaVersion) throw new InvalidDataException($"Unsupported society schema {schema}; expected {SchemaVersion}.");
        var result = new SocietyWorldState { Day = reader.ReadUInt64() };
        int factionCount = reader.ReadCount(); for (int index = 0; index < factionCount; index++) { FactionState value = FactionState.ReadCanonical(reader); result.factions.Add(value.Id, value); }
        int settlementCount = reader.ReadCount(); for (int index = 0; index < settlementCount; index++) { SettlementState value = SettlementState.ReadCanonical(reader); result.settlements.Add(value.Id, value); }
        int relationCount = reader.ReadCount(); for (int index = 0; index < relationCount; index++) { FactionRelationshipState value = FactionRelationshipState.ReadCanonical(reader); result.relationships.Add(new FactionRelationshipKey(value.SourceFactionId, value.TargetFactionId), value); }
        int territoryCount = reader.ReadCount(); for (int index = 0; index < territoryCount; index++) { TerritoryClaimState value = TerritoryClaimState.ReadCanonical(reader); result.territoryClaims.Add(value.Id, value); }
        int leadershipCount = reader.ReadCount(); for (int index = 0; index < leadershipCount; index++) { LeadershipState value = LeadershipState.ReadCanonical(reader); result.leadership.Add(value.FactionId, value); }
        int routeCount = reader.ReadCount(); for (int index = 0; index < routeCount; index++) { TradeRouteRuntimeState value = TradeRouteRuntimeState.ReadCanonical(reader); result.tradeRoutes.Add(value.Id, value); }
        int shipmentCount = reader.ReadCount(); for (int index = 0; index < shipmentCount; index++) { TradeShipmentState value = TradeShipmentState.ReadCanonical(reader); result.shipments.Add(value.Id, value); }
        int reputationCount = reader.ReadCount(); for (int index = 0; index < reputationCount; index++) { FactionReputationState value = FactionReputationState.ReadCanonical(reader); result.reputations.Add(new ReputationKey(value.SubjectId, value.FactionId), value); }
        int evidenceCount = reader.ReadCount(); for (int index = 0; index < evidenceCount; index++) { ReputationEvidenceState value = ReputationEvidenceState.ReadCanonical(reader); result.reputationEvidence.Add(value.Id, value); }
        int crimeCount = reader.ReadCount(); for (int index = 0; index < crimeCount; index++) { CrimeRecordState value = CrimeRecordState.ReadCanonical(reader); result.crimes.Add(value.Id, value); }
        int disguiseCount = reader.ReadCount(); for (int index = 0; index < disguiseCount; index++) { DisguiseIdentityState value = DisguiseIdentityState.ReadCanonical(reader); result.disguises.Add(value.Id, value); }
        int observationCount = reader.ReadCount(); for (int index = 0; index < observationCount; index++) { DisguiseObservationState value = DisguiseObservationState.ReadCanonical(reader); result.disguiseObservations.Add(value.Id, value); }
        int conflictCount = reader.ReadCount(); for (int index = 0; index < conflictCount; index++) { StrategicConflictState value = StrategicConflictState.ReadCanonical(reader); result.conflicts.Add(value.Id, value); }
        int eventCount = reader.ReadCount(); for (int index = 0; index < eventCount; index++) result.events.Enqueue(SocietyEventRecord.ReadCanonical(reader)); result.Revision = reader.ReadUInt64(); return result;
    }
}
