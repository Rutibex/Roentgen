using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Narrative;

public sealed record NarrativeExternalEffectRequest(
    StableId Id,
    StableId QuestId,
    StableId ResponsibleActorId,
    NarrativeEffectKind Kind,
    StableId TargetId,
    NarrativeValue Value,
    int Amount,
    string Argument,
    ulong ExpectedNarrativeRevision);
public sealed record NarrativeEffectReceipt(StableId RequestId, bool Success, string Message, ulong ResultRevision);

public interface INarrativeExternalEffectRouter
{
    IReadOnlyList<NarrativeEffectReceipt> PrepareAndCommit(IReadOnlyList<NarrativeExternalEffectRequest> requests);
}

public sealed class NoOpNarrativeExternalEffectRouter : INarrativeExternalEffectRouter
{
    public IReadOnlyList<NarrativeEffectReceipt> PrepareAndCommit(IReadOnlyList<NarrativeExternalEffectRequest> requests) => requests.Select(request => new NarrativeEffectReceipt(request.Id, true, "accepted by no-op integration router", request.ExpectedNarrativeRevision)).ToArray();
}

public sealed record NarrativeEffectBatchResult(bool Success, IReadOnlyList<NarrativeEffectReceipt> Receipts, IReadOnlyList<string> Errors);

public sealed class NarrativeEffectExecutor
{
    public NarrativeEffectBatchResult Apply(NarrativeWorldState world, QuestInstanceState quest, StableId responsibleActorId, IReadOnlyList<NarrativeEffectDefinition> effects, INarrativeExternalEffectRouter externalRouter, ulong tick)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(quest); ArgumentNullException.ThrowIfNull(effects); ArgumentNullException.ThrowIfNull(externalRouter);
        var external = new List<NarrativeExternalEffectRequest>();
        foreach (NarrativeEffectDefinition effect in effects.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            StableId consequenceId = StableIdFactory.CreateChild(quest.Id, "consequence", effect.Id);
            if (quest.AppliedConsequenceIds.Contains(consequenceId)) continue;
            StableId target = string.IsNullOrWhiteSpace(effect.TargetSlotId) ? StableId.Empty : quest.Plan.ResolveSlot(effect.TargetSlotId);
            if (IsInternal(effect.Kind)) continue;
            external.Add(new NarrativeExternalEffectRequest(consequenceId, quest.Id, responsibleActorId, effect.Kind, target, effect.Value, effect.Amount, effect.Argument, world.Revision));
        }

        IReadOnlyList<NarrativeEffectReceipt> receipts = externalRouter.PrepareAndCommit(external);
        string[] failures = receipts.Where(static receipt => !receipt.Success).Select(static receipt => receipt.Message).ToArray();
        if (failures.Length > 0 || receipts.Count != external.Count) return new NarrativeEffectBatchResult(false, receipts, failures.Length > 0 ? failures : ["External effect router returned an incomplete receipt set."]);

        foreach (NarrativeEffectDefinition effect in effects.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            StableId consequenceId = StableIdFactory.CreateChild(quest.Id, "consequence", effect.Id);
            if (!quest.MarkConsequenceApplied(consequenceId)) continue;
            StableId target = string.IsNullOrWhiteSpace(effect.TargetSlotId) ? StableId.Empty : quest.Plan.ResolveSlot(effect.TargetSlotId);
            ApplyInternal(world, quest, effect, target, responsibleActorId, consequenceId, tick);
            world.AddConsequence(new NarrativeConsequenceRecord(consequenceId, quest.Id, effect.Id, responsibleActorId, target.IsEmpty ? [] : [target], tick, effect.Kind is NarrativeEffectKind.ChangeReputation or NarrativeEffectKind.ChangeRelationship, effect.Argument));
        }
        return new NarrativeEffectBatchResult(true, receipts, []);
    }

    private static bool IsInternal(NarrativeEffectKind kind) => kind is NarrativeEffectKind.AssertFact or NarrativeEffectKind.RetractFact or NarrativeEffectKind.GrantKnowledge or NarrativeEffectKind.DiscloseFact or NarrativeEffectKind.ActivateObjective or NarrativeEffectKind.CompleteObjective or NarrativeEffectKind.FailObjective or NarrativeEffectKind.StartQuest or NarrativeEffectKind.CompleteQuest or NarrativeEffectKind.ScheduleFollowUp;

    private static void ApplyInternal(NarrativeWorldState world, QuestInstanceState quest, NarrativeEffectDefinition effect, StableId target, StableId responsibleActorId, StableId consequenceId, ulong tick)
    {
        switch (effect.Kind)
        {
            case NarrativeEffectKind.AssertFact:
                world.UpsertFact(new FactRecord(StableIdFactory.CreateChild(consequenceId, "fact", effect.PredicateId), effect.PredicateId, target, effect.Value, FactTruthState.True, FactScope.Quest, consequenceId, tick, null));
                break;
            case NarrativeEffectKind.RetractFact:
                foreach (FactRecord fact in world.FactDatabase.Query(effect.PredicateId, target).ToArray()) world.RemoveFact(fact.Id);
                break;
            case NarrativeEffectKind.GrantKnowledge:
            case NarrativeEffectKind.DiscloseFact:
                StableId factId = effect.Value.Kind == NarrativeValueKind.StableId ? effect.Value.StableIdValue : StableIdFactory.CreateChild(quest.Id, "fact-slot", effect.PredicateId);
                StableId claimId = StableIdFactory.CreateChild(consequenceId, "knowledge", target.ToString());
                world.AddKnowledge(new KnowledgeClaim(claimId, target, factId, FactTruthState.True, Math.Clamp(effect.Amount == 0 ? 800 : effect.Amount, 0, 1000), new KnowledgeSource(consequenceId, KnowledgeSourceKind.QuestConsequence, responsibleActorId, effect.Id, tick), 800, tick, tick, KnowledgeSecrecy.QuestSecret, false, true));
                break;
            case NarrativeEffectKind.ActivateObjective: quest.ActivateObjective(effect.Argument, tick); break;
            case NarrativeEffectKind.CompleteObjective: quest.CompleteObjective(effect.Argument, tick); break;
            case NarrativeEffectKind.FailObjective: quest.FailObjective(effect.Argument, tick); break;
            case NarrativeEffectKind.StartQuest: quest.Start(tick); break;
            case NarrativeEffectKind.CompleteQuest: quest.Complete(tick); break;
            case NarrativeEffectKind.ScheduleFollowUp:
                world.UpsertFact(new FactRecord(StableIdFactory.CreateChild(consequenceId, "follow-up", effect.Argument), "narrative.follow-up.available", quest.Id, NarrativeValue.FromToken(effect.Argument), FactTruthState.True, FactScope.Quest, consequenceId, tick, null));
                break;
        }
    }
}
