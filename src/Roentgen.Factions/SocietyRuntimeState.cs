using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public sealed class FactionState : ICanonicalWritable
{
    private readonly SortedDictionary<IdeologyAxis, int> ideology = [];
    private readonly SortedSet<StableId> controlledSettlements = [];
    private readonly SortedSet<StableId> memberActorIds = [];
    private readonly SortedDictionary<string, int> policyValues = new(StringComparer.Ordinal);
    private readonly SortedSet<string> activeObjectiveIds = new(StringComparer.Ordinal);

    public FactionState(GeneratedFactionPlan plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        foreach (IdeologyValue value in plan.Ideology) ideology[value.Axis] = Math.Clamp(value.PositionPermille, -1000, 1000);
        foreach (FactionObjectiveDefinition objective in plan.Objectives) activeObjectiveIds.Add(objective.Id);
    }

    public GeneratedFactionPlan Plan { get; }
    public StableId Id => Plan.Id;
    public IReadOnlyDictionary<IdeologyAxis, int> Ideology => ideology;
    public IReadOnlySet<StableId> ControlledSettlements => controlledSettlements;
    public IReadOnlySet<StableId> MemberActorIds => memberActorIds;
    public IReadOnlyDictionary<string, int> PolicyValues => policyValues;
    public IReadOnlySet<string> ActiveObjectiveIds => activeObjectiveIds;
    public long TreasuryMinorUnits { get; private set; }
    public int CohesionPermille { get; private set; } = 750;
    public int LegitimacyPermille { get; private set; } = 700;
    public ulong Revision { get; private set; }

    public void AddControlledSettlement(StableId settlementId) { if (controlledSettlements.Add(settlementId)) Revision++; }
    public void RemoveControlledSettlement(StableId settlementId) { if (controlledSettlements.Remove(settlementId)) Revision++; }
    public void AddMember(StableId actorId) { if (memberActorIds.Add(actorId)) Revision++; }
    public void RemoveMember(StableId actorId) { if (memberActorIds.Remove(actorId)) Revision++; }
    public void SetPolicy(string id, int valuePermille) { ArgumentException.ThrowIfNullOrWhiteSpace(id); policyValues[id] = Math.Clamp(valuePermille, -1000, 1000); Revision++; }
    public void AddTreasury(long amountMinorUnits) { TreasuryMinorUnits = Math.Max(0, checked(TreasuryMinorUnits + amountMinorUnits)); Revision++; }
    public bool TrySpend(long amountMinorUnits) { if (amountMinorUnits < 0 || TreasuryMinorUnits < amountMinorUnits) return false; TreasuryMinorUnits -= amountMinorUnits; Revision++; return true; }
    public void AdjustCohesion(int delta) { CohesionPermille = Math.Clamp(CohesionPermille + delta, 0, 1000); Revision++; }
    public void AdjustLegitimacy(int delta) { LegitimacyPermille = Math.Clamp(LegitimacyPermille + delta, 0, 1000); Revision++; }

    public void WriteCanonical(CanonicalWriter writer)
    {
        Plan.WriteCanonical(writer); writer.WriteCount(ideology.Count); foreach ((IdeologyAxis axis, int value) in ideology) { writer.WriteInt32((int)axis); writer.WriteInt32(value); }
        WriteIds(writer, controlledSettlements); WriteIds(writer, memberActorIds); writer.WriteCount(policyValues.Count); foreach ((string id, int value) in policyValues) { writer.WriteString(id); writer.WriteInt32(value); }
        writer.WriteCount(activeObjectiveIds.Count); foreach (string id in activeObjectiveIds) writer.WriteString(id); writer.WriteInt64(TreasuryMinorUnits); writer.WriteInt32(CohesionPermille); writer.WriteInt32(LegitimacyPermille); writer.WriteUInt64(Revision);
    }

    public static FactionState ReadCanonical(CanonicalReader reader)
    {
        var result = new FactionState(GeneratedFactionPlan.ReadCanonical(reader)); result.ideology.Clear(); int ideologyCount = reader.ReadCount(); for (int index = 0; index < ideologyCount; index++) result.ideology[(IdeologyAxis)reader.ReadInt32()] = reader.ReadInt32();
        ReadIds(reader, result.controlledSettlements); ReadIds(reader, result.memberActorIds); int policyCount = reader.ReadCount(); for (int index = 0; index < policyCount; index++) result.policyValues[reader.ReadRequiredString()] = reader.ReadInt32();
        result.activeObjectiveIds.Clear(); int objectiveCount = reader.ReadCount(); for (int index = 0; index < objectiveCount; index++) result.activeObjectiveIds.Add(reader.ReadRequiredString());
        result.TreasuryMinorUnits = reader.ReadInt64(); result.CohesionPermille = reader.ReadInt32(); result.LegitimacyPermille = reader.ReadInt32(); result.Revision = reader.ReadUInt64(); return result;
    }
    private static void WriteIds(CanonicalWriter writer, IReadOnlyCollection<StableId> ids) { writer.WriteCount(ids.Count); foreach (StableId id in ids.Order()) writer.WriteStableId(id); }
    private static void ReadIds(CanonicalReader reader, ISet<StableId> destination) { int count = reader.ReadCount(); for (int index = 0; index < count; index++) destination.Add(reader.ReadStableId()); }
}

public sealed record DiplomaticAgreementState(StableId Id, StableId GrantorFactionId, StableId RecipientFactionId, DiplomaticAgreementKind Kind, ulong StartsDay, ulong ExpiresDay, int StrengthPermille, bool Active) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(GrantorFactionId); writer.WriteStableId(RecipientFactionId); writer.WriteInt32((int)Kind); writer.WriteUInt64(StartsDay); writer.WriteUInt64(ExpiresDay); writer.WriteInt32(StrengthPermille); writer.WriteBoolean(Active); }
    public static DiplomaticAgreementState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (DiplomaticAgreementKind)reader.ReadInt32(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadInt32(), reader.ReadBoolean());
}

public sealed class FactionRelationshipState : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, DiplomaticAgreementState> agreements = [];
    public FactionRelationshipState(StableId sourceFactionId, StableId targetFactionId)
    {
        if (sourceFactionId.IsEmpty || targetFactionId.IsEmpty || sourceFactionId == targetFactionId) throw new ArgumentException("Directed faction relationship requires two distinct faction IDs.");
        SourceFactionId = sourceFactionId; TargetFactionId = targetFactionId;
    }
    public StableId SourceFactionId { get; }
    public StableId TargetFactionId { get; }
    public int TrustPermille { get; private set; }
    public int FearPermille { get; private set; }
    public int RespectPermille { get; private set; }
    public int IdeologicalCompatibilityPermille { get; private set; }
    public int MaterialDependencyPermille { get; private set; }
    public int HistoricalGrievancePermille { get; private set; }
    public int BorderTensionPermille { get; private set; }
    public int TradeReliancePermille { get; private set; }
    public int ObligationPermille { get; private set; }
    public int IntelligenceConfidencePermille { get; private set; } = 500;
    public IReadOnlyCollection<DiplomaticAgreementState> Agreements => agreements.Values;
    public ulong Revision { get; private set; }
    public FactionRelationKind DerivedKind
    {
        get
        {
            if (agreements.Values.Any(static value => value.Active && value.Kind == DiplomaticAgreementKind.Embargo)) return FactionRelationKind.Embargoed;
            int hostility = HistoricalGrievancePermille + BorderTensionPermille + FearPermille / 2 - TrustPermille - RespectPermille / 2;
            if (hostility >= 1400) return FactionRelationKind.AtWar;
            if (hostility >= 900) return FactionRelationKind.Hostile;
            if (hostility >= 500) return FactionRelationKind.Rival;
            if (TrustPermille >= 700 && RespectPermille >= 600) return FactionRelationKind.Allied;
            if (TrustPermille >= 350 || TradeReliancePermille >= 600) return FactionRelationKind.Cooperative;
            if (TrustPermille <= -250 || HistoricalGrievancePermille >= 400) return FactionRelationKind.Suspicious;
            return FactionRelationKind.Neutral;
        }
    }
    public void Adjust(int trust = 0, int fear = 0, int respect = 0, int compatibility = 0, int dependency = 0, int grievance = 0, int borderTension = 0, int tradeReliance = 0, int obligation = 0, int intelligenceConfidence = 0)
    {
        TrustPermille = Math.Clamp(TrustPermille + trust, -1000, 1000); FearPermille = Math.Clamp(FearPermille + fear, 0, 1000); RespectPermille = Math.Clamp(RespectPermille + respect, -1000, 1000);
        IdeologicalCompatibilityPermille = Math.Clamp(IdeologicalCompatibilityPermille + compatibility, -1000, 1000); MaterialDependencyPermille = Math.Clamp(MaterialDependencyPermille + dependency, 0, 1000); HistoricalGrievancePermille = Math.Clamp(HistoricalGrievancePermille + grievance, 0, 1000);
        BorderTensionPermille = Math.Clamp(BorderTensionPermille + borderTension, 0, 1000); TradeReliancePermille = Math.Clamp(TradeReliancePermille + tradeReliance, 0, 1000); ObligationPermille = Math.Clamp(ObligationPermille + obligation, -1000, 1000); IntelligenceConfidencePermille = Math.Clamp(IntelligenceConfidencePermille + intelligenceConfidence, 0, 1000); Revision++;
    }
    public void AddAgreement(DiplomaticAgreementState agreement) { agreements[agreement.Id] = agreement; Revision++; }
    public void RemoveAgreement(StableId id) { if (agreements.Remove(id)) Revision++; }
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(SourceFactionId); writer.WriteStableId(TargetFactionId); writer.WriteInt32(TrustPermille); writer.WriteInt32(FearPermille); writer.WriteInt32(RespectPermille); writer.WriteInt32(IdeologicalCompatibilityPermille); writer.WriteInt32(MaterialDependencyPermille); writer.WriteInt32(HistoricalGrievancePermille); writer.WriteInt32(BorderTensionPermille); writer.WriteInt32(TradeReliancePermille); writer.WriteInt32(ObligationPermille); writer.WriteInt32(IntelligenceConfidencePermille); writer.WriteCount(agreements.Count); foreach (DiplomaticAgreementState agreement in agreements.Values) agreement.WriteCanonical(writer); writer.WriteUInt64(Revision);
    }
    public static FactionRelationshipState ReadCanonical(CanonicalReader reader)
    {
        var result = new FactionRelationshipState(reader.ReadStableId(), reader.ReadStableId()) { TrustPermille = reader.ReadInt32(), FearPermille = reader.ReadInt32(), RespectPermille = reader.ReadInt32(), IdeologicalCompatibilityPermille = reader.ReadInt32(), MaterialDependencyPermille = reader.ReadInt32(), HistoricalGrievancePermille = reader.ReadInt32(), BorderTensionPermille = reader.ReadInt32(), TradeReliancePermille = reader.ReadInt32(), ObligationPermille = reader.ReadInt32(), IntelligenceConfidencePermille = reader.ReadInt32() };
        int count = reader.ReadCount(); for (int index = 0; index < count; index++) { DiplomaticAgreementState agreement = DiplomaticAgreementState.ReadCanonical(reader); result.agreements.Add(agreement.Id, agreement); } result.Revision = reader.ReadUInt64(); return result;
    }
}

public sealed record TerritoryClaimState(StableId Id, StableId LocationId, StableId FactionId, TerritoryAuthorityKind Kind, int StrengthPermille, StableId SupplySettlementId, ulong LastChangedDay) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(LocationId); writer.WriteStableId(FactionId); writer.WriteInt32((int)Kind); writer.WriteInt32(StrengthPermille); writer.WriteStableId(SupplySettlementId); writer.WriteUInt64(LastChangedDay); }
    public static TerritoryClaimState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (TerritoryAuthorityKind)reader.ReadInt32(), reader.ReadInt32(), reader.ReadStableId(), reader.ReadUInt64());
}

public sealed record FactionOfficeState(StableId Id, string OfficeId, StableId HolderActorId, ulong TermStartedDay, ulong TermEndsDay, int LegitimacyPermille, bool Vacant) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteString(OfficeId); writer.WriteStableId(HolderActorId); writer.WriteUInt64(TermStartedDay); writer.WriteUInt64(TermEndsDay); writer.WriteInt32(LegitimacyPermille); writer.WriteBoolean(Vacant); }
    public static FactionOfficeState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadInt32(), reader.ReadBoolean());
}

public sealed record InternalDivisionState(StableId Id, StableId FactionId, string Tag, StableId LeaderActorId, int InfluencePermille, int LoyaltyPermille, string ObjectiveId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(FactionId); writer.WriteString(Tag); writer.WriteStableId(LeaderActorId); writer.WriteInt32(InfluencePermille); writer.WriteInt32(LoyaltyPermille); writer.WriteString(ObjectiveId); }
    public static InternalDivisionState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadRequiredString());
}

public sealed class LeadershipState : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, FactionOfficeState> offices = [];
    private readonly SortedDictionary<StableId, InternalDivisionState> divisions = [];
    public LeadershipState(StableId factionId, LeadershipModelDefinition model) { FactionId = factionId; Model = model; }
    public StableId FactionId { get; }
    public LeadershipModelDefinition Model { get; }
    public IReadOnlyCollection<FactionOfficeState> Offices => offices.Values;
    public IReadOnlyCollection<InternalDivisionState> Divisions => divisions.Values;
    public ulong Revision { get; private set; }
    public void UpsertOffice(FactionOfficeState office) { offices[office.Id] = office; Revision++; }
    public void UpsertDivision(InternalDivisionState division) { divisions[division.Id] = division; Revision++; }
    public FactionOfficeState? HighestOffice => offices.Values.FirstOrDefault(value => string.Equals(value.OfficeId, Model.HighestOfficeId, StringComparison.Ordinal));
    public bool TryInstallLeader(StableId actorId, ulong day)
    {
        StableId id = StableIdFactory.CreateChild(FactionId, "office", Model.HighestOfficeId); ulong ends = Model.TermDays <= 0 ? ulong.MaxValue : checked(day + (ulong)Model.TermDays);
        offices[id] = new FactionOfficeState(id, Model.HighestOfficeId, actorId, day, ends, 700, false); Revision++; return true;
    }
    public void VacateLeader(ulong day)
    {
        FactionOfficeState? office = HighestOffice; if (office is null) return; offices[office.Id] = office with { HolderActorId = StableId.Empty, TermStartedDay = day, TermEndsDay = day, Vacant = true }; Revision++;
    }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(FactionId); Model.WriteCanonical(writer); writer.WriteCount(offices.Count); foreach (FactionOfficeState office in offices.Values) office.WriteCanonical(writer); writer.WriteCount(divisions.Count); foreach (InternalDivisionState division in divisions.Values) division.WriteCanonical(writer); writer.WriteUInt64(Revision); }
    public static LeadershipState ReadCanonical(CanonicalReader reader)
    {
        var result = new LeadershipState(reader.ReadStableId(), LeadershipModelDefinition.ReadCanonical(reader)); int officeCount = reader.ReadCount(); for (int index = 0; index < officeCount; index++) { FactionOfficeState office = FactionOfficeState.ReadCanonical(reader); result.offices.Add(office.Id, office); } int divisionCount = reader.ReadCount(); for (int index = 0; index < divisionCount; index++) { InternalDivisionState division = InternalDivisionState.ReadCanonical(reader); result.divisions.Add(division.Id, division); } result.Revision = reader.ReadUInt64(); return result;
    }
}

public sealed class PopulationCohortState : ICanonicalWritable
{
    public PopulationCohortState(PopulationCohortKind kind, int count, int healthPermille = 800, int moralePermille = 700) { Kind = kind; Count = Math.Max(0, count); HealthPermille = Math.Clamp(healthPermille, 0, 1000); MoralePermille = Math.Clamp(moralePermille, 0, 1000); }
    public PopulationCohortKind Kind { get; }
    public int Count { get; private set; }
    public int HealthPermille { get; private set; }
    public int MoralePermille { get; private set; }
    public int ContaminationPermille { get; private set; }
    public ulong Revision { get; private set; }
    public void AdjustCount(int delta) { Count = Math.Max(0, checked(Count + delta)); Revision++; }
    public void AdjustHealth(int delta) { HealthPermille = Math.Clamp(HealthPermille + delta, 0, 1000); Revision++; }
    public void AdjustMorale(int delta) { MoralePermille = Math.Clamp(MoralePermille + delta, 0, 1000); Revision++; }
    public void AdjustContamination(int delta) { ContaminationPermille = Math.Clamp(ContaminationPermille + delta, 0, 1000); Revision++; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Kind); writer.WriteInt32(Count); writer.WriteInt32(HealthPermille); writer.WriteInt32(MoralePermille); writer.WriteInt32(ContaminationPermille); writer.WriteUInt64(Revision); }
    public static PopulationCohortState ReadCanonical(CanonicalReader reader) => new((PopulationCohortKind)reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()) { ContaminationPermille = reader.ReadInt32(), Revision = reader.ReadUInt64() };
}

public sealed class PopulationState : ICanonicalWritable
{
    private readonly SortedDictionary<PopulationCohortKind, PopulationCohortState> cohorts = [];
    public PopulationState(StableId settlementId, int housingCapacity) { SettlementId = settlementId; HousingCapacity = Math.Max(1, housingCapacity); }
    public StableId SettlementId { get; }
    public int HousingCapacity { get; private set; }
    public int SecurityPermille { get; private set; } = 600;
    public int MigrationPressurePermille { get; private set; }
    public IReadOnlyDictionary<PopulationCohortKind, PopulationCohortState> Cohorts => cohorts;
    public int TotalPopulation => cohorts.Values.Sum(static value => value.Count);
    public int OvercrowdingPermille => TotalPopulation <= HousingCapacity ? 0 : Math.Clamp((TotalPopulation - HousingCapacity) * 1000 / HousingCapacity, 0, 2000);
    public ulong Revision { get; private set; }
    public void Upsert(PopulationCohortState cohort) { cohorts[cohort.Kind] = cohort; Revision++; }
    public void SetHousingCapacity(int value) { HousingCapacity = Math.Max(1, value); Revision++; }
    public void AdjustSecurity(int delta) { SecurityPermille = Math.Clamp(SecurityPermille + delta, 0, 1000); Revision++; }
    public void AdjustMigrationPressure(int delta) { MigrationPressurePermille = Math.Clamp(MigrationPressurePermille + delta, -1000, 1000); Revision++; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(SettlementId); writer.WriteInt32(HousingCapacity); writer.WriteInt32(SecurityPermille); writer.WriteInt32(MigrationPressurePermille); writer.WriteCount(cohorts.Count); foreach (PopulationCohortState cohort in cohorts.Values) cohort.WriteCanonical(writer); writer.WriteUInt64(Revision); }
    public static PopulationState ReadCanonical(CanonicalReader reader)
    {
        var result = new PopulationState(reader.ReadStableId(), reader.ReadInt32()) { SecurityPermille = reader.ReadInt32(), MigrationPressurePermille = reader.ReadInt32() }; int count = reader.ReadCount(); for (int index = 0; index < count; index++) { PopulationCohortState cohort = PopulationCohortState.ReadCanonical(reader); result.cohorts.Add(cohort.Kind, cohort); } result.Revision = reader.ReadUInt64(); return result;
    }
}

public sealed class ResourceStockState : ICanonicalWritable
{
    public ResourceStockState(SocietyCommodity commodity, long quantityMilliunits, long storageCapacityMilliunits, long targetMilliunits) { Commodity = commodity; QuantityMilliunits = Math.Clamp(quantityMilliunits, 0, Math.Max(1, storageCapacityMilliunits)); StorageCapacityMilliunits = Math.Max(1, storageCapacityMilliunits); TargetMilliunits = Math.Clamp(targetMilliunits, 1, StorageCapacityMilliunits); }
    public SocietyCommodity Commodity { get; }
    public long QuantityMilliunits { get; private set; }
    public long StorageCapacityMilliunits { get; }
    public long TargetMilliunits { get; }
    public ulong Revision { get; private set; }
    public long Add(long amount) { long before = QuantityMilliunits; QuantityMilliunits = Math.Clamp(checked(QuantityMilliunits + Math.Max(0, amount)), 0, StorageCapacityMilliunits); if (QuantityMilliunits != before) Revision++; return QuantityMilliunits - before; }
    public long Consume(long amount) { long consumed = Math.Min(QuantityMilliunits, Math.Max(0, amount)); QuantityMilliunits -= consumed; if (consumed > 0) Revision++; return consumed; }
    public int ScarcityPermille => QuantityMilliunits >= TargetMilliunits ? 0 : Math.Clamp((int)((TargetMilliunits - QuantityMilliunits) * 1000 / Math.Max(1, TargetMilliunits)), 0, 1000);
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Commodity); writer.WriteInt64(QuantityMilliunits); writer.WriteInt64(StorageCapacityMilliunits); writer.WriteInt64(TargetMilliunits); writer.WriteUInt64(Revision); }
    public static ResourceStockState ReadCanonical(CanonicalReader reader) => new((SocietyCommodity)reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64()) { Revision = reader.ReadUInt64() };
}

public sealed record ResourceProcessState(StableId Id, SocietyCommodity Commodity, long AmountPerDayMilliunits, bool Production, string RequiredServiceTag, int EfficiencyPermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteInt32((int)Commodity); writer.WriteInt64(AmountPerDayMilliunits); writer.WriteBoolean(Production); writer.WriteString(RequiredServiceTag); writer.WriteInt32(EfficiencyPermille); }
    public static ResourceProcessState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), (SocietyCommodity)reader.ReadInt32(), reader.ReadInt64(), reader.ReadBoolean(), reader.ReadRequiredString(), reader.ReadInt32());
}

public sealed class SettlementServiceState : ICanonicalWritable
{
    private readonly SortedDictionary<SocietyCommodity, long> requiredInputs = [];
    public SettlementServiceState(StableId id, StableId settlementId, SettlementServiceKind kind, StableId ownerFactionId, int capacity, int qualityPermille) { Id = id; SettlementId = settlementId; Kind = kind; OwnerFactionId = ownerFactionId; Capacity = Math.Max(0, capacity); QualityPermille = Math.Clamp(qualityPermille, 0, 1000); }
    public StableId Id { get; }
    public StableId SettlementId { get; }
    public SettlementServiceKind Kind { get; }
    public StableId OwnerFactionId { get; private set; }
    public StableId OperatorActorId { get; private set; }
    public int Capacity { get; private set; }
    public int QualityPermille { get; private set; }
    public int StaffingPermille { get; private set; } = 1000;
    public int DamagePermille { get; private set; }
    public int QueuePressurePermille { get; private set; }
    public bool Operating { get; private set; } = true;
    public IReadOnlyDictionary<SocietyCommodity, long> RequiredInputs => requiredInputs;
    public ulong Revision { get; private set; }
    public void SetOwnerFaction(StableId factionId) { if (factionId.IsEmpty) throw new ArgumentException("Service owner faction cannot be empty.", nameof(factionId)); OwnerFactionId = factionId; Revision++; }
    public void SetOperator(StableId actorId) { OperatorActorId = actorId; Revision++; }
    public void SetRequiredInput(SocietyCommodity commodity, long amountPerDayMilliunits) { requiredInputs[commodity] = Math.Max(0, amountPerDayMilliunits); Revision++; }
    public void SetOperating(bool value) { if (Operating != value) { Operating = value; Revision++; } }
    public void SetQueuePressure(int value) { QueuePressurePermille = Math.Clamp(value, 0, 2000); Revision++; }
    public void AdjustDamage(int delta) { DamagePermille = Math.Clamp(DamagePermille + delta, 0, 1000); Revision++; }
    public void SetStaffing(int value) { StaffingPermille = Math.Clamp(value, 0, 1000); Revision++; }
    public int EffectiveCapacity => Operating ? Capacity * QualityPermille / 1000 * StaffingPermille / 1000 * (1000 - DamagePermille) / 1000 : 0;
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(SettlementId); writer.WriteInt32((int)Kind); writer.WriteStableId(OwnerFactionId); writer.WriteStableId(OperatorActorId); writer.WriteInt32(Capacity); writer.WriteInt32(QualityPermille); writer.WriteInt32(StaffingPermille); writer.WriteInt32(DamagePermille); writer.WriteInt32(QueuePressurePermille); writer.WriteBoolean(Operating); writer.WriteCount(requiredInputs.Count); foreach ((SocietyCommodity commodity, long amount) in requiredInputs) { writer.WriteInt32((int)commodity); writer.WriteInt64(amount); } writer.WriteUInt64(Revision); }
    public static SettlementServiceState ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId(); StableId settlementId = reader.ReadStableId(); var kind = (SettlementServiceKind)reader.ReadInt32(); StableId owner = reader.ReadStableId(); StableId operatorId = reader.ReadStableId(); int capacity = reader.ReadInt32(); int quality = reader.ReadInt32();
        var result = new SettlementServiceState(id, settlementId, kind, owner, capacity, quality) { OperatorActorId = operatorId, StaffingPermille = reader.ReadInt32(), DamagePermille = reader.ReadInt32(), QueuePressurePermille = reader.ReadInt32(), Operating = reader.ReadBoolean() };
        int inputCount = reader.ReadCount(); for (int index = 0; index < inputCount; index++) result.requiredInputs[(SocietyCommodity)reader.ReadInt32()] = reader.ReadInt64(); result.Revision = reader.ReadUInt64(); return result;
    }
}


public sealed class SettlementState : ICanonicalWritable
{
    private readonly SortedDictionary<SocietyCommodity, ResourceStockState> stocks = [];
    private readonly SortedDictionary<StableId, ResourceProcessState> processes = [];
    private readonly SortedDictionary<StableId, SettlementServiceState> services = [];
    private readonly SortedSet<string> prohibitedGoods = new(StringComparer.Ordinal);
    private readonly SortedSet<string> preferredGoods = new(StringComparer.Ordinal);

    public SettlementState(SettlementGenerationProfile plan, PopulationState population)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan)); Population = population ?? throw new ArgumentNullException(nameof(population)); ControllingFactionId = plan.ControllingFactionId; CivicAuthorityFactionId = plan.ControllingFactionId;
    }
    public SettlementGenerationProfile Plan { get; }
    public StableId Id => Plan.SettlementId;
    public StableId ControllingFactionId { get; private set; }
    public StableId CivicAuthorityFactionId { get; private set; }
    public PopulationState Population { get; }
    public IReadOnlyDictionary<SocietyCommodity, ResourceStockState> Stocks => stocks;
    public IReadOnlyCollection<ResourceProcessState> Processes => processes.Values;
    public IReadOnlyCollection<SettlementServiceState> Services => services.Values;
    public IReadOnlySet<string> ProhibitedGoods => prohibitedGoods;
    public IReadOnlySet<string> PreferredGoods => preferredGoods;
    public int TaxPermille { get; private set; } = 80;
    public int MerchantRiskPermille { get; private set; } = 100;
    public string CurrencyPolicyId { get; private set; } = "mixed-scrip-barter";
    public long TreasuryMinorUnits { get; private set; }
    public int StabilityPermille { get; private set; } = 700;
    public int AccessibilityPermille { get; private set; } = 1000;
    public ulong Revision { get; private set; }

    public void ChangeControl(StableId factionId)
    {
        if (factionId.IsEmpty) throw new ArgumentException("Controlling faction cannot be empty.", nameof(factionId));
        StableId previous = ControllingFactionId;
        ControllingFactionId = factionId;
        if (CivicAuthorityFactionId.IsEmpty || CivicAuthorityFactionId == previous) CivicAuthorityFactionId = factionId;
        foreach (SettlementServiceState service in services.Values.Where(value => value.OwnerFactionId == previous)) service.SetOwnerFaction(factionId);
        Revision++;
    }
    public void SetCivicAuthority(StableId factionId) { CivicAuthorityFactionId = factionId; Revision++; }
    public void UpsertStock(ResourceStockState stock) { stocks[stock.Commodity] = stock; Revision++; }
    public ResourceStockState GetStock(SocietyCommodity commodity) => stocks.TryGetValue(commodity, out ResourceStockState? stock) ? stock : throw new KeyNotFoundException($"Settlement '{Id}' has no stock for {commodity}.");
    public bool TryGetStock(SocietyCommodity commodity, out ResourceStockState? stock) => stocks.TryGetValue(commodity, out stock);
    public void UpsertProcess(ResourceProcessState process) { processes[process.Id] = process; Revision++; }
    public void UpsertService(SettlementServiceState service) { services[service.Id] = service; Revision++; }
    public void AddProhibitedGood(string tag) { if (prohibitedGoods.Add(tag)) Revision++; }
    public void AddPreferredGood(string tag) { if (preferredGoods.Add(tag)) Revision++; }
    public void SetTax(int taxPermille) { TaxPermille = Math.Clamp(taxPermille, 0, 500); Revision++; }
    public void SetMerchantRisk(int value) { MerchantRiskPermille = Math.Clamp(value, 0, 1000); Revision++; }
    public void SetCurrencyPolicy(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); CurrencyPolicyId = value; Revision++; }
    public void AddTreasury(long amountMinorUnits) { TreasuryMinorUnits = Math.Max(0, checked(TreasuryMinorUnits + amountMinorUnits)); Revision++; }
    public void AdjustStability(int delta) { StabilityPermille = Math.Clamp(StabilityPermille + delta, 0, 1000); Revision++; }
    public void SetAccessibility(int value) { AccessibilityPermille = Math.Clamp(value, 0, 1000); Revision++; }
    public int ScarcityPermille(SocietyCommodity commodity) => stocks.TryGetValue(commodity, out ResourceStockState? stock) ? stock.ScarcityPermille : 1000;

    public void WriteCanonical(CanonicalWriter writer)
    {
        Plan.WriteCanonical(writer); writer.WriteStableId(ControllingFactionId); writer.WriteStableId(CivicAuthorityFactionId); Population.WriteCanonical(writer);
        writer.WriteCount(stocks.Count); foreach (ResourceStockState stock in stocks.Values) stock.WriteCanonical(writer); writer.WriteCount(processes.Count); foreach (ResourceProcessState process in processes.Values) process.WriteCanonical(writer); writer.WriteCount(services.Count); foreach (SettlementServiceState service in services.Values) service.WriteCanonical(writer);
        WriteStrings(writer, prohibitedGoods); WriteStrings(writer, preferredGoods); writer.WriteInt32(TaxPermille); writer.WriteInt32(MerchantRiskPermille); writer.WriteString(CurrencyPolicyId); writer.WriteInt64(TreasuryMinorUnits); writer.WriteInt32(StabilityPermille); writer.WriteInt32(AccessibilityPermille); writer.WriteUInt64(Revision);
    }
    public static SettlementState ReadCanonical(CanonicalReader reader)
    {
        SettlementGenerationProfile plan = SettlementGenerationProfile.ReadCanonical(reader); StableId controller = reader.ReadStableId(); StableId civic = reader.ReadStableId(); PopulationState population = PopulationState.ReadCanonical(reader);
        var result = new SettlementState(plan, population) { ControllingFactionId = controller, CivicAuthorityFactionId = civic };
        int stockCount = reader.ReadCount(); for (int index = 0; index < stockCount; index++) { ResourceStockState stock = ResourceStockState.ReadCanonical(reader); result.stocks.Add(stock.Commodity, stock); }
        int processCount = reader.ReadCount(); for (int index = 0; index < processCount; index++) { ResourceProcessState process = ResourceProcessState.ReadCanonical(reader); result.processes.Add(process.Id, process); }
        int serviceCount = reader.ReadCount(); for (int index = 0; index < serviceCount; index++) { SettlementServiceState service = SettlementServiceState.ReadCanonical(reader); result.services.Add(service.Id, service); }
        ReadStrings(reader, result.prohibitedGoods); ReadStrings(reader, result.preferredGoods); result.TaxPermille = reader.ReadInt32(); result.MerchantRiskPermille = reader.ReadInt32(); result.CurrencyPolicyId = reader.ReadRequiredString(); result.TreasuryMinorUnits = reader.ReadInt64(); result.StabilityPermille = reader.ReadInt32(); result.AccessibilityPermille = reader.ReadInt32(); result.Revision = reader.ReadUInt64(); return result;
    }
    private static void WriteStrings(CanonicalWriter writer, IReadOnlyCollection<string> values) { writer.WriteCount(values.Count); foreach (string value in values.Order(StringComparer.Ordinal)) writer.WriteString(value); }
    private static void ReadStrings(CanonicalReader reader, ISet<string> destination) { int count = reader.ReadCount(); for (int index = 0; index < count; index++) destination.Add(reader.ReadRequiredString()); }
}

public sealed class TradeRouteRuntimeState : ICanonicalWritable
{
    public TradeRouteRuntimeState(StableId id, StableId exporterSettlementId, StableId importerSettlementId, SocietyCommodity commodity, long dailyCapacityMilliunits, int travelDays, int baseRiskPermille)
    {
        if (exporterSettlementId == importerSettlementId) throw new ArgumentException("Trade route requires distinct settlements."); Id = id; ExporterSettlementId = exporterSettlementId; ImporterSettlementId = importerSettlementId; Commodity = commodity; DailyCapacityMilliunits = Math.Max(0, dailyCapacityMilliunits); TravelDays = Math.Max(1, travelDays); BaseRiskPermille = Math.Clamp(baseRiskPermille, 0, 1000);
    }
    public StableId Id { get; }
    public StableId ExporterSettlementId { get; }
    public StableId ImporterSettlementId { get; }
    public SocietyCommodity Commodity { get; }
    public long DailyCapacityMilliunits { get; }
    public int TravelDays { get; }
    public int BaseRiskPermille { get; private set; }
    public int EscortPermille { get; private set; }
    public bool Active { get; private set; } = true;
    public ulong Revision { get; private set; }
    public void SetActive(bool value) { if (Active != value) { Active = value; Revision++; } }
    public void SetEscort(int value) { EscortPermille = Math.Clamp(value, 0, 1000); Revision++; }
    public void AdjustRisk(int delta) { BaseRiskPermille = Math.Clamp(BaseRiskPermille + delta, 0, 1000); Revision++; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(ExporterSettlementId); writer.WriteStableId(ImporterSettlementId); writer.WriteInt32((int)Commodity); writer.WriteInt64(DailyCapacityMilliunits); writer.WriteInt32(TravelDays); writer.WriteInt32(BaseRiskPermille); writer.WriteInt32(EscortPermille); writer.WriteBoolean(Active); writer.WriteUInt64(Revision); }
    public static TradeRouteRuntimeState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (SocietyCommodity)reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt32()) { EscortPermille = reader.ReadInt32(), Active = reader.ReadBoolean(), Revision = reader.ReadUInt64() };
}

public sealed class TradeShipmentState : ICanonicalWritable
{
    public TradeShipmentState(StableId id, StableId routeId, StableId exporterSettlementId, StableId importerSettlementId, SocietyCommodity commodity, long loadedMilliunits, ulong departureDay, ulong arrivalDay)
    {
        Id = id; RouteId = routeId; ExporterSettlementId = exporterSettlementId; ImporterSettlementId = importerSettlementId; Commodity = commodity; LoadedMilliunits = Math.Max(0, loadedMilliunits); DepartureDay = departureDay; ArrivalDay = arrivalDay;
    }
    public StableId Id { get; }
    public StableId RouteId { get; }
    public StableId ExporterSettlementId { get; }
    public StableId ImporterSettlementId { get; }
    public SocietyCommodity Commodity { get; }
    public long LoadedMilliunits { get; }
    public ulong DepartureDay { get; }
    public ulong ArrivalDay { get; }
    public int RiskPermille { get; private set; }
    public long LostMilliunits { get; private set; }
    public long DeliveredMilliunits { get; private set; }
    public bool Resolved { get; private set; }
    public ulong Revision { get; private set; }
    public void SetRisk(int value) { RiskPermille = Math.Clamp(value, 0, 1000); Revision++; }
    public void Resolve(long deliveredMilliunits, long lostMilliunits) { if (Resolved) throw new InvalidOperationException("Shipment has already been resolved."); long delivered = Math.Clamp(deliveredMilliunits, 0, LoadedMilliunits); long lost = Math.Clamp(lostMilliunits, 0, LoadedMilliunits - delivered); DeliveredMilliunits = delivered; LostMilliunits = lost; Resolved = true; Revision++; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(RouteId); writer.WriteStableId(ExporterSettlementId); writer.WriteStableId(ImporterSettlementId); writer.WriteInt32((int)Commodity); writer.WriteInt64(LoadedMilliunits); writer.WriteUInt64(DepartureDay); writer.WriteUInt64(ArrivalDay); writer.WriteInt32(RiskPermille); writer.WriteInt64(LostMilliunits); writer.WriteInt64(DeliveredMilliunits); writer.WriteBoolean(Resolved); writer.WriteUInt64(Revision); }
    public static TradeShipmentState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (SocietyCommodity)reader.ReadInt32(), reader.ReadInt64(), reader.ReadUInt64(), reader.ReadUInt64()) { RiskPermille = reader.ReadInt32(), LostMilliunits = reader.ReadInt64(), DeliveredMilliunits = reader.ReadInt64(), Resolved = reader.ReadBoolean(), Revision = reader.ReadUInt64() };
}

public sealed class FactionReputationState : ICanonicalWritable
{
    private readonly SortedDictionary<ReputationDimension, int> values = [];
    public FactionReputationState(StableId subjectId, StableId factionId) { SubjectId = subjectId; FactionId = factionId; foreach (ReputationDimension dimension in Enum.GetValues<ReputationDimension>()) values[dimension] = 0; }
    public StableId SubjectId { get; }
    public StableId FactionId { get; }
    public IReadOnlyDictionary<ReputationDimension, int> Values => values;
    public ulong Revision { get; private set; }
    public int this[ReputationDimension dimension] => values[dimension];
    public void Adjust(ReputationDimension dimension, int delta) { int minimum = dimension is ReputationDimension.Fear or ReputationDimension.KnownCrimeSeverity or ReputationDimension.PublicFamiliarity ? 0 : -1000; values[dimension] = Math.Clamp(values[dimension] + delta, minimum, 1000); Revision++; }
    public string DerivedStanding
    {
        get
        {
            if (this[ReputationDimension.KnownCrimeSeverity] >= 700) return this[ReputationDimension.Respect] >= 500 ? "wanted-adversary" : "wanted";
            if (this[ReputationDimension.Fear] >= 700 && this[ReputationDimension.Trust] <= 0) return "feared";
            if (this[ReputationDimension.Trust] >= 600 && this[ReputationDimension.Obligation] >= 400) return "protected-ally";
            if (this[ReputationDimension.Respect] >= 600 && this[ReputationDimension.IdeologicalAlignment] <= -300) return "honorable-enemy";
            if (this[ReputationDimension.Trust] <= -400) return "hostile";
            if (this[ReputationDimension.PublicFamiliarity] <= 100) return "unknown";
            return "known-outsider";
        }
    }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(SubjectId); writer.WriteStableId(FactionId); writer.WriteCount(values.Count); foreach ((ReputationDimension dimension, int value) in values) { writer.WriteInt32((int)dimension); writer.WriteInt32(value); } writer.WriteUInt64(Revision); }
    public static FactionReputationState ReadCanonical(CanonicalReader reader) { var result = new FactionReputationState(reader.ReadStableId(), reader.ReadStableId()); result.values.Clear(); int count = reader.ReadCount(); for (int index = 0; index < count; index++) result.values[(ReputationDimension)reader.ReadInt32()] = reader.ReadInt32(); result.Revision = reader.ReadUInt64(); return result; }
}

public sealed class ReputationEvidenceState : ICanonicalWritable
{
    public ReputationEvidenceState(StableId id, StableId subjectId, StableId factionId, StableId sourceId, ReputationDimension dimension, int delta, int reliabilityPermille, int publicityPermille, ulong createdDay, string reason)
    {
        if (id.IsEmpty || subjectId.IsEmpty || factionId.IsEmpty || sourceId.IsEmpty) throw new ArgumentException("Reputation evidence IDs cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Id = id; SubjectId = subjectId; FactionId = factionId; SourceId = sourceId; Dimension = dimension; Delta = delta; ReliabilityPermille = Math.Clamp(reliabilityPermille, 0, 1000); PublicityPermille = Math.Clamp(publicityPermille, 0, 1000); CreatedDay = createdDay; Reason = reason;
    }
    public StableId Id { get; }
    public StableId SubjectId { get; }
    public StableId FactionId { get; }
    public StableId SourceId { get; }
    public ReputationDimension Dimension { get; }
    public int Delta { get; }
    public int ReliabilityPermille { get; }
    public int PublicityPermille { get; }
    public ulong CreatedDay { get; }
    public string Reason { get; }
    public bool Applied { get; private set; }
    public ulong AppliedDay { get; private set; }
    public void MarkApplied(ulong day) { if (Applied) return; Applied = true; AppliedDay = day; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(SubjectId); writer.WriteStableId(FactionId); writer.WriteStableId(SourceId); writer.WriteInt32((int)Dimension); writer.WriteInt32(Delta); writer.WriteInt32(ReliabilityPermille); writer.WriteInt32(PublicityPermille); writer.WriteUInt64(CreatedDay); writer.WriteString(Reason); writer.WriteBoolean(Applied); writer.WriteUInt64(AppliedDay); }
    public static ReputationEvidenceState ReadCanonical(CanonicalReader reader)
    {
        var result = new ReputationEvidenceState(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (ReputationDimension)reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt64(), reader.ReadRequiredString());
        result.Applied = reader.ReadBoolean(); result.AppliedDay = reader.ReadUInt64(); return result;
    }
}

public sealed class CrimeRecordState : ICanonicalWritable
{
    private readonly SortedSet<StableId> witnessIds = [];
    private readonly SortedSet<StableId> evidenceIds = [];
    public CrimeRecordState(StableId id, CrimeKind kind, StableId jurisdictionFactionId, StableId locationId, StableId suspectedActorId, int severityPermille, ulong occurredDay) { Id = id; Kind = kind; JurisdictionFactionId = jurisdictionFactionId; LocationId = locationId; SuspectedActorId = suspectedActorId; SeverityPermille = Math.Clamp(severityPermille, 0, 1000); OccurredDay = occurredDay; }
    public StableId Id { get; }
    public CrimeKind Kind { get; }
    public StableId JurisdictionFactionId { get; }
    public StableId LocationId { get; }
    public StableId SuspectedActorId { get; private set; }
    public int SeverityPermille { get; }
    public ulong OccurredDay { get; }
    public CrimeStatus Status { get; private set; }
    public int IdentityConfidencePermille { get; private set; }
    public int FineMinorUnits { get; private set; }
    public IReadOnlySet<StableId> WitnessIds => witnessIds;
    public IReadOnlySet<StableId> EvidenceIds => evidenceIds;
    public ulong Revision { get; private set; }
    public void AddWitness(StableId id, int identityConfidencePermille) { if (witnessIds.Add(id)) Revision++; IdentityConfidencePermille = Math.Max(IdentityConfidencePermille, Math.Clamp(identityConfidencePermille, 0, 1000)); }
    public void AddEvidence(StableId id) { if (evidenceIds.Add(id)) Revision++; }
    public void SetIdentityConfidence(int value) { int clamped = Math.Clamp(value, 0, 1000); if (IdentityConfidencePermille != clamped) { IdentityConfidencePermille = clamped; Revision++; } }
    public void Report() { if (Status == CrimeStatus.Unreported) { Status = CrimeStatus.Reported; Revision++; } }
    public void IssueWarrant(int fineMinorUnits) { Status = CrimeStatus.WarrantIssued; FineMinorUnits = Math.Max(0, fineMinorUnits); Revision++; }
    public void Resolve(bool forgiven) { Status = forgiven ? CrimeStatus.Forgiven : CrimeStatus.Resolved; Revision++; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteInt32((int)Kind); writer.WriteStableId(JurisdictionFactionId); writer.WriteStableId(LocationId); writer.WriteStableId(SuspectedActorId); writer.WriteInt32(SeverityPermille); writer.WriteUInt64(OccurredDay); writer.WriteInt32((int)Status); writer.WriteInt32(IdentityConfidencePermille); writer.WriteInt32(FineMinorUnits); WriteIds(writer, witnessIds); WriteIds(writer, evidenceIds); writer.WriteUInt64(Revision); }
    public static CrimeRecordState ReadCanonical(CanonicalReader reader) { var result = new CrimeRecordState(reader.ReadStableId(), (CrimeKind)reader.ReadInt32(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadInt32(), reader.ReadUInt64()) { Status = (CrimeStatus)reader.ReadInt32(), IdentityConfidencePermille = reader.ReadInt32(), FineMinorUnits = reader.ReadInt32() }; ReadIds(reader, result.witnessIds); ReadIds(reader, result.evidenceIds); result.Revision = reader.ReadUInt64(); return result; }
    private static void WriteIds(CanonicalWriter writer, IReadOnlyCollection<StableId> ids) { writer.WriteCount(ids.Count); foreach (StableId id in ids.Order()) writer.WriteStableId(id); }
    private static void ReadIds(CanonicalReader reader, ISet<StableId> ids) { int count = reader.ReadCount(); for (int index = 0; index < count; index++) ids.Add(reader.ReadStableId()); }
}

public sealed record DisguiseIdentityState(StableId Id, StableId ActorId, StableId PresentedFactionId, IReadOnlySet<string> CredentialTags, IReadOnlySet<string> EquipmentTags, int QualityPermille, bool FaceCovered) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(ActorId); writer.WriteStableId(PresentedFactionId); WriteStrings(writer, CredentialTags); WriteStrings(writer, EquipmentTags); writer.WriteInt32(QualityPermille); writer.WriteBoolean(FaceCovered); }
    public static DisguiseIdentityState ReadCanonical(CanonicalReader reader) { StableId id = reader.ReadStableId(); StableId actor = reader.ReadStableId(); StableId faction = reader.ReadStableId(); IReadOnlySet<string> credentials = ReadStrings(reader); IReadOnlySet<string> equipment = ReadStrings(reader); int quality = reader.ReadInt32(); bool covered = reader.ReadBoolean(); return new DisguiseIdentityState(id, actor, faction, credentials, equipment, quality, covered); }
    private static void WriteStrings(CanonicalWriter writer, IReadOnlySet<string> values) { writer.WriteCount(values.Count); foreach (string value in values.Order(StringComparer.Ordinal)) writer.WriteString(value); }
    private static IReadOnlySet<string> ReadStrings(CanonicalReader reader) { int count = reader.ReadCount(); var result = new SortedSet<string>(StringComparer.Ordinal); for (int index = 0; index < count; index++) result.Add(reader.ReadRequiredString()); return result; }
}

public sealed record DisguiseObservationState(StableId Id, StableId ObserverId, StableId ActorId, StableId BelievedFactionId, int ConfidencePermille, DisguiseEvaluation Evaluation, ulong ObservedDay, bool SharedWithFaction) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(ObserverId); writer.WriteStableId(ActorId); writer.WriteStableId(BelievedFactionId); writer.WriteInt32(ConfidencePermille); writer.WriteInt32((int)Evaluation); writer.WriteUInt64(ObservedDay); writer.WriteBoolean(SharedWithFaction); }
    public static DisguiseObservationState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadInt32(), (DisguiseEvaluation)reader.ReadInt32(), reader.ReadUInt64(), reader.ReadBoolean());
}

public sealed class StrategicConflictState : ICanonicalWritable
{
    public StrategicConflictState(StableId id, StableId aggressorFactionId, StableId defenderFactionId, StableId locationId, StrategicConflictStage stage) { Id = id; AggressorFactionId = aggressorFactionId; DefenderFactionId = defenderFactionId; LocationId = locationId; Stage = stage; }
    public StableId Id { get; }
    public StableId AggressorFactionId { get; }
    public StableId DefenderFactionId { get; }
    public StableId LocationId { get; }
    public StrategicConflictStage Stage { get; private set; }
    public int AggressorStrengthPermille { get; private set; } = 500;
    public int DefenderStrengthPermille { get; private set; } = 500;
    public int PlayerRelevancePermille { get; private set; }
    public bool ReservedForPlayableEncounter { get; private set; }
    public ulong LastChangedDay { get; private set; }
    public ulong Revision { get; private set; }
    public void SetStrength(int aggressor, int defender) { AggressorStrengthPermille = Math.Clamp(aggressor, 0, 2000); DefenderStrengthPermille = Math.Clamp(defender, 0, 2000); Revision++; }
    public void SetPlayerRelevance(int value) { PlayerRelevancePermille = Math.Clamp(value, 0, 1000); ReservedForPlayableEncounter = PlayerRelevancePermille >= 600; Revision++; }
    public void Advance(StrategicConflictStage stage, ulong day) { Stage = stage; LastChangedDay = day; Revision++; }
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteStableId(AggressorFactionId); writer.WriteStableId(DefenderFactionId); writer.WriteStableId(LocationId); writer.WriteInt32((int)Stage); writer.WriteInt32(AggressorStrengthPermille); writer.WriteInt32(DefenderStrengthPermille); writer.WriteInt32(PlayerRelevancePermille); writer.WriteBoolean(ReservedForPlayableEncounter); writer.WriteUInt64(LastChangedDay); writer.WriteUInt64(Revision); }
    public static StrategicConflictState ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadStableId(), (StrategicConflictStage)reader.ReadInt32()) { AggressorStrengthPermille = reader.ReadInt32(), DefenderStrengthPermille = reader.ReadInt32(), PlayerRelevancePermille = reader.ReadInt32(), ReservedForPlayableEncounter = reader.ReadBoolean(), LastChangedDay = reader.ReadUInt64(), Revision = reader.ReadUInt64() };
}

public sealed record SocietyEventRecord(StableId Id, SocietyEventKind Kind, ulong Day, StableId PrimaryId, StableId SecondaryId, long Amount, string Description) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteInt32((int)Kind); writer.WriteUInt64(Day); writer.WriteStableId(PrimaryId); writer.WriteStableId(SecondaryId); writer.WriteInt64(Amount); writer.WriteString(Description); }
    public static SocietyEventRecord ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), (SocietyEventKind)reader.ReadInt32(), reader.ReadUInt64(), reader.ReadStableId(), reader.ReadStableId(), reader.ReadInt64(), reader.ReadRequiredString());
}
