using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Generation;

public sealed class GenerationProvenanceBuilder
{
    private readonly List<string> _validationResults = [];
    private readonly List<string> _repairHistory = [];
    private readonly List<GenerationDecision> _decisions = [];

    public GenerationProvenanceBuilder(
        StableId objectId,
        StableId? parentId,
        CampaignSeed campaignSeed,
        DomainSeed domainSeed,
        string generatorId,
        int generatorSchemaVersion)
    {
        if (objectId.IsEmpty)
        {
            throw new ArgumentException("Object ID cannot be empty.", nameof(objectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(generatorId);
        if (generatorSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generatorSchemaVersion));
        }

        ObjectId = objectId;
        ParentId = parentId;
        CampaignSeed = campaignSeed;
        DomainSeed = domainSeed;
        GeneratorId = generatorId;
        GeneratorSchemaVersion = generatorSchemaVersion;
    }

    public StableId ObjectId { get; }
    public StableId? ParentId { get; }
    public CampaignSeed CampaignSeed { get; }
    public DomainSeed DomainSeed { get; }
    public string GeneratorId { get; }
    public int GeneratorSchemaVersion { get; }

    public void RecordDecision(string stepId, string decision, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(decision);
        _decisions.Add(new GenerationDecision(stepId, decision, detail));
    }

    public void RecordValidation(string result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        _validationResults.Add(result);
    }

    public void RecordRepair(string result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        _repairHistory.Add(result);
    }

    public GenerationProvenance Build() => new(
        ObjectId,
        ParentId,
        CampaignSeed,
        DomainSeed,
        GeneratorId,
        GeneratorSchemaVersion,
        _validationResults.ToArray(),
        _repairHistory.ToArray(),
        Pcg32Random.CurrentAlgorithmId,
        _decisions.ToArray());

    public StateChecksum ComputeFingerprint()
    {
        var writer = new CanonicalWriter();
        writer.WriteStableId(ObjectId);
        writer.WriteBoolean(ParentId.HasValue);
        if (ParentId.HasValue) writer.WriteStableId(ParentId.Value);
        writer.WriteUInt64(CampaignSeed.Value);
        writer.WriteUInt64(DomainSeed.Value);
        writer.WriteString(DomainSeed.Domain);
        writer.WriteInt32(DomainSeed.DerivationVersion);
        writer.WriteString(GeneratorId);
        writer.WriteInt32(GeneratorSchemaVersion);
        writer.WriteString(Pcg32Random.CurrentAlgorithmId);
        writer.WriteCount(_validationResults.Count);
        foreach (string item in _validationResults) writer.WriteString(item);
        writer.WriteCount(_repairHistory.Count);
        foreach (string item in _repairHistory) writer.WriteString(item);
        writer.WriteCount(_decisions.Count);
        foreach (GenerationDecision item in _decisions)
        {
            writer.WriteString(item.StepId);
            writer.WriteString(item.Decision);
            writer.WriteString(item.Detail);
        }

        return writer.ComputeChecksum();
    }
}
