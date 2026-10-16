namespace Roentgen.Contracts;

public readonly record struct CampaignSeed(ulong Value);

public readonly record struct DomainSeed(ulong Value, string Domain, int DerivationVersion = 1);

public sealed record GenerationDecision(
    string StepId,
    string Decision,
    string? Detail = null);

public sealed record GenerationProvenance(
    StableId ObjectId,
    StableId? ParentId,
    CampaignSeed CampaignSeed,
    DomainSeed DomainSeed,
    string GeneratorId,
    int GeneratorSchemaVersion,
    IReadOnlyList<string> ValidationResults,
    IReadOnlyList<string> RepairHistory,
    string RandomAlgorithmId = "pcg32-roentgen-v1",
    IReadOnlyList<GenerationDecision>? Decisions = null);

public interface IVersionedGenerator<in TRequest, out TResult>
{
    string GeneratorId { get; }
    int SchemaVersion { get; }
    TResult Generate(TRequest request, DomainSeed seed);
}

public interface IValidator<in T>
{
    ValidationReport Validate(T value);
}

public interface IRepairStage<T>
{
    string RepairId { get; }
    RepairResult<T> TryRepair(T value, ValidationReport report, DomainSeed repairSeed);
}

public sealed record ValidationIssue(string Code, string Message, bool IsCritical);

public sealed record ValidationReport(bool IsValid, IReadOnlyList<ValidationIssue> Issues);

public sealed record RepairResult<T>(bool Changed, T Value, string Summary);
