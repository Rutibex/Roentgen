namespace Roentgen.Campaign;

public enum HistoricalEventKind
{
    PreCollapseInfrastructure = 0,
    CollapseExposure = 1,
    Destruction = 2,
    Abandonment = 3,
    Repurposing = 4,
    SurvivorMigration = 5,
    FactionFounded = 6,
    ResourceFailure = 7,
    Conflict = 8,
    Discovery = 9,
    Concealment = 10,
    LeadershipChange = 11
}

public enum CampaignPremiseKind
{
    ResourceConflict = 0,
    DiagnosticFailure = 1,
    BuriedSystem = 2,
    FoundingMyth = 3,
    TransportDirective = 4,
    MedicalTransformation = 5,
    ConvergentCause = 6
}

public enum CampaignMilestoneRole
{
    Opening = 0,
    RegionalCrisis = 1,
    FactionIntroduction = 2,
    Revelation = 3,
    RegionalExpansion = 4,
    FactionRealignment = 5,
    IrreversibleChoice = 6,
    Counteraction = 7,
    FinalConvergence = 8,
    Ending = 9
}

public enum CampaignMilestoneStatus
{
    Planned = 0,
    Eligible = 1,
    Offered = 2,
    Active = 3,
    AwaitingDecision = 4,
    Resolved = 5,
    Failed = 6,
    Recovered = 7,
    Superseded = 8,
    LockedOut = 9
}

public enum RevelationStatus
{
    Hidden = 0,
    Suspected = 1,
    Proven = 2,
    Disputed = 3,
    Suppressed = 4
}

public enum ConvergenceForm
{
    MultiFactionNegotiation = 0,
    SiegeOrAssault = 1,
    Evacuation = 2,
    SystemRepair = 3,
    SystemShutdown = 4,
    DeliberateExposure = 5,
    Containment = 6,
    TransferOfControl = 7,
    Destruction = 8,
    ControlledTransformation = 9
}

public enum EndingTone
{
    Restoration = 0,
    UneasyPeace = 1,
    Fragmentation = 2,
    AuthoritarianOrder = 3,
    Exodus = 4,
    Transformation = 5,
    Containment = 6,
    CatastropheAverted = 7
}

public enum EndingRequirementKind
{
    CampaignFlag = 0,
    CommitmentOption = 1,
    RevelationProven = 2,
    FactionExists = 3,
    FactionTrustAtLeast = 4,
    FactionFearAtLeast = 5,
    ConvergenceForm = 6,
    MilestoneResolved = 7
}

public enum CriticalRoleFallbackKind
{
    NamedReplacement = 0,
    QualifiedFactionMember = 1,
    AlternateFactionOffice = 2,
    DocumentOrRecording = 3,
    TerminalOrMachine = 4,
    AftermathRoute = 5
}

public enum CampaignEventKind
{
    MilestoneEligible = 0,
    MilestoneOffered = 1,
    MilestoneResolved = 2,
    MilestoneFailed = 3,
    RevelationChanged = 4,
    CommitmentRecorded = 5,
    CriticalRoleReplaced = 6,
    ConvergenceResolved = 7,
    EndingSelected = 8,
    EpilogueGenerated = 9
}
