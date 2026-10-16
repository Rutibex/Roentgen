using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Narrative;
using Roentgen.Factions;
using FactionReputationDimension = Roentgen.Factions.ReputationDimension;
using Roentgen.Items;
using Roentgen.Simulation;
using Roentgen.Persistence;
using Roentgen.Rpg;

namespace Roentgen.Narrative.Tests;

public sealed class Phase16SystemTests
{
    private static readonly CampaignSeed Seed = new(0x524F454E5447454EUL);
    private static readonly StableId PlayerId = StableIdFactory.Create("player", "phase16-tests");

    [Fact]
    public void Initial_library_contains_thirty_valid_kernels()
    {
        IReadOnlyList<QuestKernelDefinition> kernels = Phase16Content.CreateInitialKernels();
        Assert.Equal(30, kernels.Count);
        var validator = new QuestKernelValidator();
        Assert.All(kernels, kernel => Assert.True(validator.Validate(kernel).IsValid, kernel.Id));
        Assert.All(kernels, kernel => Assert.True(kernel.Routes.Count >= 3));
    }

    [Fact]
    public void Binding_is_deterministic_and_uses_stable_ids()
    {
        QuestKernelDefinition kernel = Phase16Content.CreatePaleManifestKernel();
        QuestBindingContext context = Phase16Content.CreateRegressionBindingContext();
        StableId root = StableIdFactory.CreateCampaignRoot(Seed);
        QuestBindingResult first = new QuestBinder().Bind(kernel, context, root, Seed.Value);
        QuestBindingResult second = new QuestBinder().Bind(kernel, context, root, Seed.Value);
        Assert.True(first.Success); Assert.True(second.Success); Assert.NotNull(first.Plan); Assert.NotNull(second.Plan);
        Assert.Equal(first.Plan!.Id, second.Plan!.Id);
        Assert.Equal(first.Plan.BoundSlots, second.Plan.BoundSlots);
    }

    [Fact]
    public void Truth_belief_and_disclosure_are_distinct()
    {
        NarrativeWorldState world = Phase16Content.CreateRegressionWorld(Seed, PlayerId);
        FactRecord hidden = world.Facts.Single(fact => fact.PredicateId == "shipment.redirected-by");
        Assert.False(world.Knows(Phase16Content.ProspectCaretakerId, hidden.Id));
        FactRecord missing = world.Facts.Single(fact => fact.PredicateId == "shipment.missing");
        Assert.True(world.Knows(Phase16Content.ProspectCaretakerId, missing.Id));
        Assert.DoesNotContain(world.GetKnowledge(Phase16Content.ProspectCaretakerId), claim => claim.FactId == hidden.Id && claim.MayDisclose);
    }

    [Fact]
    public void Semantic_events_are_idempotent()
    {
        NarrativeWorldState world = Phase16Content.CreateRegressionWorld(Seed, PlayerId);
        StableId eventId = StableIdFactory.Create("event", "phase16-idempotent");
        var semanticEvent = new SemanticEventRecord(eventId, "LocationEntered", 10, StableIdFactory.Create("domain", "world"), Phase16Content.MeridianRelayId, [PlayerId], new Dictionary<string, NarrativeValue> { ["entered"] = NarrativeValue.FromBoolean(true) });
        var engine = new QuestRuntimeEngine();
        var query = new BasicNarrativeQueryContext(world, 10, skill: (_, _) => 70);
        _ = engine.IngestEvent(world, semanticEvent, query, new NoOpNarrativeExternalEffectRouter(), PlayerId);
        ulong afterFirst = world.Revision;
        IReadOnlyList<QuestUpdateResult> second = engine.IngestEvent(world, semanticEvent, query, new NoOpNarrativeExternalEffectRouter(), PlayerId);
        Assert.Empty(second);
        Assert.Equal(afterFirst, world.Revision);
        Assert.Single(world.ConsumedEventIds.Where(id => id == eventId));
    }

    [Fact]
    public void All_six_regression_builds_can_solve_the_pale_manifest()
    {
        GeneratedQuestPlan plan = Phase16Content.CreateRegressionWorld(Seed, PlayerId).Quests.Single().Plan;
        var solver = new AbstractQuestSolver();
        Assert.All(AbstractQuestSolver.CreateRegressionBuilds(), build => Assert.True(solver.Solve(plan, build).Solved, build.Id));
    }

    [Fact]
    public void Skill_attempt_with_never_retry_does_not_reroll()
    {
        NarrativeWorldState world = Phase16Content.CreateRegressionWorld(Seed, PlayerId);
        QuestInstanceState quest = world.Quests.Single();
        var engine = new QuestRuntimeEngine();
        SkillCheckAttemptRecord first = engine.ResolveSkillCheck(world, quest, PlayerId, "test-approach", SkillId.Influence, 60, RetryPolicy.Never, 45, true, 10);
        SkillCheckAttemptRecord second = engine.ResolveSkillCheck(world, quest, PlayerId, "test-approach", SkillId.Influence, 60, RetryPolicy.Never, 90, true, 20);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Single(world.SkillAttempts);
    }

    [Fact]
    public void Narrative_snapshot_round_trip_preserves_checksum()
    {
        NarrativeWorldState world = Phase16Content.CreateRegressionWorld(Seed, PlayerId);
        byte[] bytes = NarrativeWorldSnapshotSerializer.WriteToBytes(world);
        NarrativeWorldState restored = NarrativeWorldSnapshotSerializer.ReadFromBytes(bytes);
        Assert.Equal(world.ComputeChecksum(), restored.ComputeChecksum());
    }

    [Fact]
    public void Campaign_snapshot_keeps_item_society_and_narrative_sections_atomic()
    {
        byte[] narrative = NarrativeWorldSnapshotSerializer.WriteToBytes(Phase16Content.CreateRegressionWorld(Seed, PlayerId));
        var snapshot = new CampaignSnapshotV16(Seed, 123, 16, "test", [1, 2, 3], [4, 5], narrative);
        CampaignSnapshotV16 restored = CampaignSnapshotV16Serializer.ReadFromBytes(CampaignSnapshotV16Serializer.WriteToBytes(snapshot));
        Assert.Equal(snapshot.CampaignSeed, restored.CampaignSeed);
        Assert.Equal(snapshot.SimulationTick, restored.SimulationTick);
        Assert.Equal(snapshot.ItemSnapshot, restored.ItemSnapshot);
        Assert.Equal(snapshot.SocietySnapshot, restored.SocietySnapshot);
        Assert.Equal(snapshot.NarrativeSnapshot, restored.NarrativeSnapshot);
    }

    [Fact]
    public void Dead_requester_activates_deterministic_recovery()
    {
        NarrativeWorldState world = Phase16Content.CreateRegressionWorld(Seed, PlayerId);
        QuestInstanceState quest = world.Quests.Single();
        StableId requester = quest.Plan.ResolveSlot("REQUESTING_ACTOR");
        StableId replacement = quest.Plan.ResolveSlot("REPLACEMENT_ACTOR");
        IReadOnlyList<RecoveryRecord> recovery = new NarrativeRepairEngine().Repair(world, actor => actor != requester, _ => replacement, 50);
        Assert.Single(recovery);
        Assert.Equal(requester, recovery[0].ReplacedEntityId);
        Assert.Equal(replacement, recovery[0].ReplacementEntityId);
    }

    [Fact]
    public void Dialogue_skill_choice_records_one_attempt_and_applies_evidence()
    {
        NarrativeWorldState world = Phase16Content.CreateRegressionWorld(Seed, PlayerId);
        QuestInstanceState quest = world.Quests.Single();
        var runtime = new Roentgen.Simulation.Phase16NarrativeRuntime(world, PlayerId);
        var query = new BasicNarrativeQueryContext(world, 10, skill: (_, _) => 80);
        var profiles = new Dictionary<StableId, DialogueProfile> { [Phase16Content.ProspectCaretakerId] = new(Phase16Content.ProspectCaretakerId, "Caretaker Vey", "clinical", 700, 600, 800, new Dictionary<string, string>(), new HashSet<string>()) };
        var names = quest.Plan.BoundSlots.Values.Distinct().ToDictionary(static id => id, static id => id.ToString()[..8]);
        names[PlayerId] = "You"; names[Phase16Content.ProspectCaretakerId] = "Caretaker Vey";
        var presentation = new BasicDialoguePresentationContext(profiles, names);
        _ = runtime.StartConversation(quest.Id, [PlayerId, Phase16Content.ProspectCaretakerId], query, presentation);
        SkillCheckAttemptRecord? attempt = runtime.ChooseDialogue("ask-evidence", query, presentation);
        Assert.NotNull(attempt);
        Assert.Contains(world.Facts, fact => fact.PredicateId == "quest.evidence.discovered");
    }
    [Fact]
    public void Quest_consequence_updates_authoritative_faction_reputation()
    {
        NarrativeWorldState narrative = Phase16Content.CreateRegressionWorld(Seed, PlayerId);
        SocietyWorldState society = Phase15Content.CreateRegressionWorld(Seed, PlayerId);
        var items = new ItemWorldState();
        QuestInstanceState quest = narrative.Quests.Single();
        var router = new Phase16DomainEffectRouter(society, items);
        NarrativeEffectDefinition effect = quest.Plan.Kernel.Consequences.Single(value => value.Kind == NarrativeEffectKind.ChangeReputation);
        NarrativeEffectBatchResult result = new NarrativeEffectExecutor().Apply(narrative, quest, PlayerId, [effect], router, 100);
        Assert.True(result.Success);
        Assert.Equal(40, society.GetOrCreateReputation(PlayerId, Phase16Content.BlackwaterFactionId)[FactionReputationDimension.Trust]);
    }

}
