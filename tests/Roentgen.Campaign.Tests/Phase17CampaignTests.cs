using Roentgen.Campaign;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Narrative;
using Roentgen.Persistence;
using Roentgen.Simulation;

namespace Roentgen.Campaign.Tests;

public sealed class Phase17CampaignTests
{
    private static readonly CampaignSeed Seed = new(0x524F454E5447454EUL);
    private static readonly StableId PlayerId = StableIdFactory.Create("player", "phase17-tests");
    private static readonly StableId CompanionId = StableIdFactory.Create("actor", "phase17-companion");

    [Fact]
    public void Generation_is_deterministic_and_meets_campaign_budgets()
    {
        GeneratedCampaignPlan first = GeneratePlan();
        GeneratedCampaignPlan second = GeneratePlan();
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.ComputeChecksum(), second.ComputeChecksum());
        Assert.Equal(6, first.History.Regions.Count);
        Assert.Equal(12, first.Milestones.Count);
        Assert.Equal(3, first.Revelations.Count);
        Assert.Equal(2, first.Choices.Count);
        Assert.True(first.EndingFamilies.Count >= 4);
        Assert.Equal(8, first.CriticalRoleAssignments.Count);
        Assert.Contains(first.EpilogueRules, static value => value.SubjectKind == "Player");
        Assert.Contains(first.EpilogueRules, static value => value.SubjectKind == "Region");
        Assert.Contains(first.EpilogueRules, static value => value.SubjectKind == "Companion");
        Assert.True(new CampaignPlanValidator().Validate(first).IsValid);
    }

    [Fact]
    public void Campaign_plan_and_runtime_round_trip_canonically()
    {
        GeneratedCampaignPlan plan = GeneratePlan();
        GeneratedCampaignPlan restoredPlan = GeneratedCampaignPlan.ReadCanonical(plan.ToCanonicalPayload());
        Assert.Equal(plan.ComputeChecksum(), restoredPlan.ComputeChecksum());
        var state = new CampaignWorldState(plan);
        CampaignWorldState restoredState = CampaignWorldState.ReadCanonical(state.ToCanonicalPayload());
        Assert.Equal(state.ComputeChecksum(), restoredState.ComputeChecksum());
    }

    [Fact]
    public void All_six_regression_builds_reach_an_ending()
    {
        GeneratedCampaignPlan plan = GeneratePlan();
        var agent = new CampaignAutoAgent();
        IReadOnlyList<CampaignAutoCompletionResult> results = agent.CompleteRegressionBuilds(
            plan,
            () => Phase15Content.CreateRegressionWorld(Seed, PlayerId),
            PlayerId);
        Assert.Equal(6, results.Count);
        Assert.All(results, result =>
        {
            Assert.True(result.Completed, $"{result.BuildId}: {string.Join("; ", result.Errors)}");
            Assert.False(result.EndingId.IsEmpty);
        });
    }

    [Fact]
    public void Missing_critical_actor_uses_deterministic_replacement_ladder()
    {
        GeneratedCampaignPlan plan = GeneratePlan();
        var campaign = new CampaignWorldState(plan);
        CriticalRoleRuntimeState first = campaign.CriticalRoles.OrderBy(static value => value.RoleId, StringComparer.Ordinal).First();
        var director = new CampaignDirector();
        IReadOnlyList<CriticalRoleReplacementRecord> repairs = director.RepairCriticalRoles(
            campaign,
            new CriticalRoleRepairContext(id => id != first.CurrentEntityId),
            120);
        Assert.Single(repairs);
        Assert.Equal(first.RoleId, repairs[0].RoleId);
        Assert.NotEqual(first.CurrentEntityId, repairs[0].ReplacementEntityId);
        Assert.True(campaign.GetCriticalRole(first.RoleId).Available);
    }

    [Fact]
    public void Phase17_snapshot_round_trip_validates_each_section_and_campaign_identity()
    {
        GeneratedCampaignPlan plan = GeneratePlan();
        var campaign = new CampaignWorldState(plan);
        CampaignSnapshotV17 snapshot = CampaignSnapshotV17Serializer.Create(
            Seed,
            777,
            17,
            plan.Fingerprint.ToString(),
            [1, 2, 3],
            [4, 5, 6],
            [7, 8, 9],
            campaign);
        byte[] bytes = CampaignSnapshotV17Serializer.WriteToBytes(snapshot);
        CampaignSnapshotV17 restored = CampaignSnapshotV17Serializer.ReadFromBytes(bytes);
        Assert.Equal(snapshot.CampaignSeed, restored.CampaignSeed);
        Assert.Equal(campaign.ComputeChecksum(), restored.ReadCampaignWorld().ComputeChecksum());

        bytes[^1] ^= 0x5A;
        Assert.Throws<InvalidDataException>(() => CampaignSnapshotV17Serializer.ReadFromBytes(bytes));
    }

    [Fact]
    public void Simulation_facade_advances_the_opening_without_presentation_dependencies()
    {
        GeneratedCampaignPlan plan = GeneratePlan();
        SocietyWorldState society = Phase15Content.CreateRegressionWorld(Seed, PlayerId);
        var runtime = new Phase17CampaignRuntime(new CampaignWorldState(plan), new NarrativeWorldState(), society, PlayerId);
        CampaignUpdateResult result = runtime.Initialize(1, 0);
        Assert.Single(result.OfferedMilestoneIds);
        GeneratedMilestonePlan opening = plan.Milestones.OrderBy(static value => value.Sequence).First();
        Assert.True(runtime.Narrative.TryGetQuest(opening.QuestPlan.Id, out QuestInstanceState? quest));
        Assert.NotNull(quest);
    }

    private static GeneratedCampaignPlan GeneratePlan()
    {
        SocietyWorldState society = Phase15Content.CreateRegressionWorld(Seed, PlayerId);
        var request = new CampaignGenerationRequest(
            Seed,
            PlayerId,
            society,
            Phase16Content.CreateRegressionBindingContext(),
            Phase16Content.CreateInitialKernels(),
            CompanionIds: [CompanionId]);
        return new CampaignGenerator().Generate(request);
    }
}
