using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Narrative;

public sealed record KnowledgeTransferResult(bool Success, KnowledgeClaim? Claim, string Message);

public sealed class KnowledgeSystem
{
    public KnowledgeTransferResult Transfer(NarrativeWorldState world, StableId sourceKnowerId, StableId recipientId, StableId factId, StableId transferEventId, KnowledgeSourceKind sourceKind, int transmissionLossPermille, ulong tick, bool mayDisclose = true)
    {
        KnowledgeClaim? source = world.GetKnowledge(sourceKnowerId).Where(claim => claim.FactId == factId && !claim.Contradicted && claim.MayDisclose).OrderByDescending(static claim => claim.ConfidencePermille).ThenBy(static claim => claim.Id).FirstOrDefault();
        if (source is null) return new KnowledgeTransferResult(false, null, "Source does not possess a disclosable claim.");
        int confidence = Math.Clamp(source.ConfidencePermille - Math.Max(0, transmissionLossPermille), 0, 1000);
        StableId claimId = StableIdFactory.CreateChild(transferEventId, "knowledge-transfer", $"{recipientId}:{factId}");
        var claim = new KnowledgeClaim(claimId, recipientId, factId, source.BelievedTruth, confidence, new KnowledgeSource(transferEventId, sourceKind, sourceKnowerId, "transferred claim", tick), Math.Clamp(source.SourceTrustPermille - transmissionLossPermille / 2, 0, 1000), tick, tick, source.Secrecy, source.Contradicted, mayDisclose);
        world.AddKnowledge(claim);
        return new KnowledgeTransferResult(true, claim, "Knowledge transferred.");
    }
}

public sealed class RumorSystem
{
    public RumorRecord Create(NarrativeWorldState world, StableId factId, StableId originatorId, ulong day, int confidencePermille, int distortionPermille, string templateId)
    {
        StableId id = StableIdFactory.CreateChild(factId, "rumor", $"{originatorId}:{day}:{templateId}");
        var rumor = new RumorRecord(id, factId, FactTruthState.Disputed, originatorId, [originatorId], Math.Clamp(confidencePermille, 0, 1000), Math.Clamp(distortionPermille, 0, 1000), day, day + 30, RumorStatus.Active, templateId);
        world.AddRumor(rumor);
        return rumor;
    }

    public RumorRecord Propagate(NarrativeWorldState world, RumorRecord rumor, StableId carrierId, StableId recipientId, ulong day)
    {
        StableId id = StableIdFactory.CreateChild(rumor.Id, "transmission", $"{carrierId}:{recipientId}:{day}");
        int confidence = Math.Max(50, rumor.ConfidencePermille - 60);
        int distortion = Math.Min(1000, rumor.DistortionPermille + 50);
        var propagated = rumor with { Id = id, TransmissionChain = rumor.TransmissionChain.Concat([carrierId, recipientId]).Distinct().ToArray(), ConfidencePermille = confidence, DistortionPermille = distortion, CreatedDay = day, ExpirationDay = day + 20 };
        world.AddRumor(propagated);
        return propagated;
    }

    public string Render(RumorRecord rumor, FactRecord fact, Func<StableId, string> displayName)
    {
        string certainty = rumor.ConfidencePermille >= 800 ? "Everyone knows" : rumor.ConfidencePermille >= 500 ? "People say" : "Someone claimed";
        string distortion = rumor.DistortionPermille >= 600 ? "though the details keep changing" : "and the account is fairly consistent";
        return $"{certainty} {displayName(fact.SubjectId)} is connected to {fact.PredicateId} ({fact.Object}); {distortion}.";
    }
}
