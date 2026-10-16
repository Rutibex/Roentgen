using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public enum IdeologyAxis
{
    PreservationTransformation = 0,
    OpennessSecrecy = 1,
    CentralAuthorityLocalAutonomy = 2,
    HumanPurityDeliberateAlteration = 3,
    RepairReplacement = 4,
    CollectiveDutyPersonalLiberty = 5,
    IsolationExpansion = 6,
    MaterialPragmatismRitualConviction = 7
}

public enum LeadershipSelectionMethod { Appointment = 0, Election = 1, Seniority = 2, Inheritance = 3, Trial = 4, TechnicalQualification = 5, MilitarySuccession = 6, Consensus = 7, DiagnosticSelection = 8 }
public enum FactionRelationKind { Allied = 0, Cooperative = 1, Neutral = 2, Suspicious = 3, Rival = 4, Hostile = 5, AtWar = 6, Ceasefire = 7, Tributary = 8, Protected = 9, Embargoed = 10 }
public enum DiplomaticAgreementKind { TradeAccess = 0, SafePassage = 1, MutualDefense = 2, ResourceConcession = 3, MedicalAccess = 4, PrisonerExchange = 5, Nonaggression = 6, Tribute = 7, Embargo = 8, SharedControl = 9 }
public enum TerritoryAuthorityKind { Claim = 0, Control = 1, Influence = 2, Access = 3 }
public enum SettlementTopologyGrammar { LinearRoad = 0, Chokepoint = 1, ConcentricFortified = 2, CliffTerrace = 3, FloodPlatform = 4, Excavation = 5, RuinInfill = 6, DistributedAgricultural = 7, IndustrialCampus = 8, MedicalCompound = 9 }
public enum PopulationCohortKind { Children = 0, WorkingAdults = 1, Elders = 2, Guards = 3, Specialists = 4, Dependants = 5, TransientWorkers = 6, Refugees = 7, Prisoners = 8, Visitors = 9 }
public enum SettlementServiceKind { MedicalCare = 0, Food = 1, CleanWater = 2, Lodging = 3, Repair = 4, Crafting = 5, Market = 6, Storage = 7, Security = 8, Transport = 9, Information = 10, Training = 11, ContaminationTreatment = 12, DiagnosticAnalysis = 13 }
public enum SocietyCommodity { Food = 0, Water = 1, Medicine = 2, Fuel = 3, Ammunition = 4, Metals = 5, Chemicals = 6, Electronics = 7, Textiles = 8, Weapons = 9, Armor = 10, LuxuryGoods = 11, DiagnosticMaterials = 12 }
public enum StrategicConflictStage { Dispute = 0, Pressure = 1, Embargo = 2, Sabotage = 3, Raids = 4, BorderFighting = 5, OpenConflict = 6, SiegeOrBlockade = 7, Ceasefire = 8, Settlement = 9 }
public enum StrategicForceKind { Patrol = 0, CaravanEscort = 1, RaiderBand = 2, Militia = 3, Expedition = 4, Garrison = 5, SaboteurCell = 6, MedicalRecoveryTeam = 7 }
public enum CrimeKind { Trespass = 0, Theft = 1, Assault = 2, Murder = 3, Sabotage = 4, ContrabandPossession = 5, ContrabandTrade = 6, UnauthorizedMedicalAlteration = 7, ContaminationOffense = 8, Espionage = 9, PrisonerRelease = 10, FactionImpersonation = 11, CriticalInfrastructureDestruction = 12 }
public enum CrimeStatus { Unreported = 0, Reported = 1, Investigating = 2, WarrantIssued = 3, FineOutstanding = 4, DetentionOrdered = 5, Forgiven = 6, Resolved = 7 }
public enum ReputationDimension { Trust = 0, Fear = 1, Respect = 2, Obligation = 3, IdeologicalAlignment = 4, KnownCrimeSeverity = 5, PublicFamiliarity = 6 }
public enum DisguiseEvaluation { Accepted = 0, Questioned = 1, Suspicious = 2, Exposed = 3 }
public enum SocietyEventKind { Production = 0, Consumption = 1, ShipmentDeparted = 2, ShipmentDelivered = 3, ShipmentLost = 4, LeadershipChanged = 5, TerritoryChanged = 6, ConflictChanged = 7, CrimeReported = 8, WarrantIssued = 9, ReputationChanged = 10, Migration = 11, ServiceChanged = 12, AgreementChanged = 13 }
public enum SocietyCommandStatus { Rejected = 0, Committed = 1 }

public static class FactionIds
{
    public static StableId Player { get; } = StableIdFactory.Create("faction", "player");
    public static StableId Civilians { get; } = StableIdFactory.Create("faction", "civilians");
    public static StableId ProspectClinicCompact { get; } = StableIdFactory.Create("faction", "prospect-clinic-compact");
    public static StableId BlackwaterRecoveryAuthority { get; } = StableIdFactory.Create("faction", "blackwater-recovery-authority");
    public static StableId RedQuarryCooperative { get; } = StableIdFactory.Create("faction", "red-quarry-cooperative");
    public static StableId FromLegacyLabel(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value switch
        {
            "player" => Player,
            "civilian" or "civilians" => Civilians,
            "friendly" or "prospect-clinic" => ProspectClinicCompact,
            "hostile" or "enemy" or "blackwater-hostiles" => BlackwaterRecoveryAuthority,
            "red-quarry" => RedQuarryCooperative,
            _ => StableIdFactory.Create("faction.legacy", value)
        };
    }
}

public readonly record struct IdeologyValue(IdeologyAxis Axis, int PositionPermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Axis); writer.WriteInt32(Math.Clamp(PositionPermille, -1000, 1000)); }
    public static IdeologyValue ReadCanonical(CanonicalReader reader) => new((IdeologyAxis)reader.ReadInt32(), reader.ReadInt32());
}

public sealed record FactionNeed(SocietyCommodity Commodity, long RequiredPerDayMilliunits, int PriorityPermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Commodity); writer.WriteInt64(RequiredPerDayMilliunits); writer.WriteInt32(PriorityPermille); }
    public static FactionNeed ReadCanonical(CanonicalReader reader) => new((SocietyCommodity)reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt32());
}

public sealed record FactionObjectiveDefinition(string Id, string Description, int PriorityPermille, IReadOnlySet<string> RequiredWorldTags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id); writer.WriteString(Description); writer.WriteInt32(PriorityPermille); writer.WriteCount(RequiredWorldTags.Count);
        foreach (string tag in RequiredWorldTags.Order(StringComparer.Ordinal)) writer.WriteString(tag);
    }
    public static FactionObjectiveDefinition ReadCanonical(CanonicalReader reader)
    {
        string id = reader.ReadRequiredString(); string description = reader.ReadRequiredString(); int priority = reader.ReadInt32(); int count = reader.ReadCount();
        var tags = new SortedSet<string>(StringComparer.Ordinal); for (int index = 0; index < count; index++) tags.Add(reader.ReadRequiredString());
        return new FactionObjectiveDefinition(id, description, priority, tags);
    }
}

public sealed record LeadershipModelDefinition(LeadershipSelectionMethod SelectionMethod, string HighestOfficeId, int TermDays, bool AllowsVacancy, IReadOnlySet<string> EligibilityTags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)SelectionMethod); writer.WriteString(HighestOfficeId); writer.WriteInt32(TermDays); writer.WriteBoolean(AllowsVacancy); writer.WriteCount(EligibilityTags.Count);
        foreach (string tag in EligibilityTags.Order(StringComparer.Ordinal)) writer.WriteString(tag);
    }
    public static LeadershipModelDefinition ReadCanonical(CanonicalReader reader)
    {
        var method = (LeadershipSelectionMethod)reader.ReadInt32(); string office = reader.ReadRequiredString(); int term = reader.ReadInt32(); bool vacancy = reader.ReadBoolean(); int count = reader.ReadCount();
        var tags = new SortedSet<string>(StringComparer.Ordinal); for (int index = 0; index < count; index++) tags.Add(reader.ReadRequiredString());
        return new LeadershipModelDefinition(method, office, term, vacancy, tags);
    }
}

public sealed record FactionKernelDefinition(
    string Id,
    string DisplayNamePattern,
    string FoundingEvent,
    string SurvivalMethod,
    IReadOnlyList<IdeologyValue> Ideology,
    IReadOnlyList<FactionNeed> MaterialNeeds,
    IReadOnlySet<string> Taboos,
    LeadershipModelDefinition Leadership,
    IReadOnlySet<string> EquipmentTags,
    IReadOnlySet<string> SettlementTags,
    IReadOnlySet<string> SpeechTags,
    IReadOnlyList<FactionObjectiveDefinition> Objectives,
    IReadOnlySet<string> InternalDivisionTags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id); writer.WriteString(DisplayNamePattern); writer.WriteString(FoundingEvent); writer.WriteString(SurvivalMethod);
        writer.WriteCount(Ideology.Count); foreach (IdeologyValue value in Ideology.OrderBy(static value => value.Axis)) value.WriteCanonical(writer);
        writer.WriteCount(MaterialNeeds.Count); foreach (FactionNeed value in MaterialNeeds.OrderBy(static value => value.Commodity)) value.WriteCanonical(writer);
        WriteStrings(writer, Taboos); Leadership.WriteCanonical(writer); WriteStrings(writer, EquipmentTags); WriteStrings(writer, SettlementTags); WriteStrings(writer, SpeechTags);
        writer.WriteCount(Objectives.Count); foreach (FactionObjectiveDefinition value in Objectives.OrderBy(static value => value.Id, StringComparer.Ordinal)) value.WriteCanonical(writer);
        WriteStrings(writer, InternalDivisionTags);
    }
    public static FactionKernelDefinition ReadCanonical(CanonicalReader reader)
    {
        string id = reader.ReadRequiredString(); string name = reader.ReadRequiredString(); string founding = reader.ReadRequiredString(); string survival = reader.ReadRequiredString();
        int ideologyCount = reader.ReadCount(); var ideology = new IdeologyValue[ideologyCount]; for (int index = 0; index < ideologyCount; index++) ideology[index] = IdeologyValue.ReadCanonical(reader);
        int needCount = reader.ReadCount(); var needs = new FactionNeed[needCount]; for (int index = 0; index < needCount; index++) needs[index] = FactionNeed.ReadCanonical(reader);
        IReadOnlySet<string> taboos = ReadStrings(reader); LeadershipModelDefinition leadership = LeadershipModelDefinition.ReadCanonical(reader);
        IReadOnlySet<string> equipment = ReadStrings(reader); IReadOnlySet<string> settlement = ReadStrings(reader); IReadOnlySet<string> speech = ReadStrings(reader);
        int objectiveCount = reader.ReadCount(); var objectives = new FactionObjectiveDefinition[objectiveCount]; for (int index = 0; index < objectiveCount; index++) objectives[index] = FactionObjectiveDefinition.ReadCanonical(reader);
        IReadOnlySet<string> divisions = ReadStrings(reader);
        return new FactionKernelDefinition(id, name, founding, survival, ideology, needs, taboos, leadership, equipment, settlement, speech, objectives, divisions);
    }
    private static void WriteStrings(CanonicalWriter writer, IReadOnlySet<string> values) { writer.WriteCount(values.Count); foreach (string value in values.Order(StringComparer.Ordinal)) writer.WriteString(value); }
    private static IReadOnlySet<string> ReadStrings(CanonicalReader reader) { int count = reader.ReadCount(); var result = new SortedSet<string>(StringComparer.Ordinal); for (int index = 0; index < count; index++) result.Add(reader.ReadRequiredString()); return result; }
}

public sealed record GeneratedFactionPlan(
    StableId Id,
    string KernelId,
    string DisplayName,
    string FoundingEvent,
    string SurvivalMethod,
    IReadOnlyList<IdeologyValue> Ideology,
    IReadOnlyList<FactionNeed> MaterialNeeds,
    IReadOnlySet<string> Taboos,
    LeadershipModelDefinition Leadership,
    IReadOnlySet<string> EquipmentTags,
    IReadOnlySet<string> SettlementTags,
    IReadOnlySet<string> SpeechTags,
    IReadOnlyList<FactionObjectiveDefinition> Objectives,
    IReadOnlySet<string> InternalDivisionTags,
    string ArchitecturalProfileId,
    string MusicalMotifDomain,
    int GeneratorVersion,
    StateChecksum Fingerprint) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteString(KernelId); writer.WriteString(DisplayName); writer.WriteString(FoundingEvent); writer.WriteString(SurvivalMethod);
        writer.WriteCount(Ideology.Count); foreach (IdeologyValue value in Ideology.OrderBy(static value => value.Axis)) value.WriteCanonical(writer);
        writer.WriteCount(MaterialNeeds.Count); foreach (FactionNeed value in MaterialNeeds.OrderBy(static value => value.Commodity)) value.WriteCanonical(writer);
        WriteStrings(writer, Taboos); Leadership.WriteCanonical(writer); WriteStrings(writer, EquipmentTags); WriteStrings(writer, SettlementTags); WriteStrings(writer, SpeechTags);
        writer.WriteCount(Objectives.Count); foreach (FactionObjectiveDefinition value in Objectives.OrderBy(static value => value.Id, StringComparer.Ordinal)) value.WriteCanonical(writer);
        WriteStrings(writer, InternalDivisionTags); writer.WriteString(ArchitecturalProfileId); writer.WriteString(MusicalMotifDomain); writer.WriteInt32(GeneratorVersion); writer.WriteChecksum(Fingerprint);
    }
    public static GeneratedFactionPlan ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId(); string kernel = reader.ReadRequiredString(); string name = reader.ReadRequiredString(); string founding = reader.ReadRequiredString(); string survival = reader.ReadRequiredString();
        int ideologyCount = reader.ReadCount(); var ideology = new IdeologyValue[ideologyCount]; for (int index = 0; index < ideologyCount; index++) ideology[index] = IdeologyValue.ReadCanonical(reader);
        int needCount = reader.ReadCount(); var needs = new FactionNeed[needCount]; for (int index = 0; index < needCount; index++) needs[index] = FactionNeed.ReadCanonical(reader);
        IReadOnlySet<string> taboos = ReadStrings(reader); LeadershipModelDefinition leadership = LeadershipModelDefinition.ReadCanonical(reader);
        IReadOnlySet<string> equipment = ReadStrings(reader); IReadOnlySet<string> settlements = ReadStrings(reader); IReadOnlySet<string> speech = ReadStrings(reader);
        int objectiveCount = reader.ReadCount(); var objectives = new FactionObjectiveDefinition[objectiveCount]; for (int index = 0; index < objectiveCount; index++) objectives[index] = FactionObjectiveDefinition.ReadCanonical(reader);
        IReadOnlySet<string> divisions = ReadStrings(reader); string architecture = reader.ReadRequiredString(); string motif = reader.ReadRequiredString(); int version = reader.ReadInt32(); StateChecksum fingerprint = reader.ReadChecksum();
        return new GeneratedFactionPlan(id, kernel, name, founding, survival, ideology, needs, taboos, leadership, equipment, settlements, speech, objectives, divisions, architecture, motif, version, fingerprint);
    }
    private static void WriteStrings(CanonicalWriter writer, IReadOnlySet<string> values) { writer.WriteCount(values.Count); foreach (string value in values.Order(StringComparer.Ordinal)) writer.WriteString(value); }
    private static IReadOnlySet<string> ReadStrings(CanonicalReader reader) { int count = reader.ReadCount(); var result = new SortedSet<string>(StringComparer.Ordinal); for (int index = 0; index < count; index++) result.Add(reader.ReadRequiredString()); return result; }
}

public sealed record SettlementGenerationProfile(
    StableId SettlementId,
    string DisplayName,
    SettlementTopologyGrammar Topology,
    StableId ControllingFactionId,
    string FoundingPurpose,
    string EconomicRole,
    string CurrentCrisis,
    string DefenseDoctrine,
    string ArchitecturalProfileId,
    IReadOnlySet<string> RequiredDistrictTags,
    IReadOnlySet<string> RequiredServiceTags,
    int PopulationCapacity,
    int PopulationPressurePermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(SettlementId); writer.WriteString(DisplayName); writer.WriteInt32((int)Topology); writer.WriteStableId(ControllingFactionId); writer.WriteString(FoundingPurpose); writer.WriteString(EconomicRole); writer.WriteString(CurrentCrisis); writer.WriteString(DefenseDoctrine); writer.WriteString(ArchitecturalProfileId);
        WriteStrings(writer, RequiredDistrictTags); WriteStrings(writer, RequiredServiceTags); writer.WriteInt32(PopulationCapacity); writer.WriteInt32(PopulationPressurePermille);
    }
    public static SettlementGenerationProfile ReadCanonical(CanonicalReader reader)
    {
        StableId id = reader.ReadStableId(); string name = reader.ReadRequiredString(); var topology = (SettlementTopologyGrammar)reader.ReadInt32(); StableId faction = reader.ReadStableId(); string founding = reader.ReadRequiredString(); string role = reader.ReadRequiredString(); string crisis = reader.ReadRequiredString(); string defense = reader.ReadRequiredString(); string architecture = reader.ReadRequiredString();
        IReadOnlySet<string> districts = ReadStrings(reader); IReadOnlySet<string> services = ReadStrings(reader); int capacity = reader.ReadInt32(); int pressure = reader.ReadInt32();
        return new SettlementGenerationProfile(id, name, topology, faction, founding, role, crisis, defense, architecture, districts, services, capacity, pressure);
    }
    private static void WriteStrings(CanonicalWriter writer, IReadOnlySet<string> values) { writer.WriteCount(values.Count); foreach (string value in values.Order(StringComparer.Ordinal)) writer.WriteString(value); }
    private static IReadOnlySet<string> ReadStrings(CanonicalReader reader) { int count = reader.ReadCount(); var result = new SortedSet<string>(StringComparer.Ordinal); for (int index = 0; index < count; index++) result.Add(reader.ReadRequiredString()); return result; }
}

public sealed record SocietyGenerationRequest(CampaignSeed CampaignSeed, StableId CampaignId, IReadOnlySet<string> AvailableWorldTags, int MajorFactionCount = 3, int SettlementCount = 3);
public sealed record SocietyValidationIssue(string Code, string Message, bool IsCritical, StableId SubjectId);
public sealed record SocietyValidationReport(bool IsValid, IReadOnlyList<SocietyValidationIssue> Issues, int RepairCount);
