using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Rpg.Tests;

public sealed class CharacterProgressionTests
{
    [Fact]
    public void Derived_statistics_match_phase12_formulas()
    {
        CharacterProgressionState state = CreateState();
        DerivedStatSnapshot stats = new DerivedStatCalculator().Calculate(state);
        Assert.Equal(100, stats[DerivedStatId.MaximumHealth]);
        Assert.Equal(118, stats[DerivedStatId.MaximumStamina]);
        Assert.Equal(48_000, stats[DerivedStatId.CarryCapacityGrams]);
        Assert.Equal(81_000, stats[DerivedStatId.MaximumFocusMilliunits]);
        Assert.Equal(4000, stats[DerivedStatId.FocusRecoveryPerSecondMilliunits]);
    }

    [Fact]
    public void Skill_point_cost_changes_at_thresholds()
    {
        Assert.Equal(1, SkillSet.GetPointCost(50));
        Assert.Equal(2, SkillSet.GetPointCost(51));
        Assert.Equal(3, SkillSet.GetPointCost(76));
    }

    [Fact]
    public void Progression_round_trip_preserves_checksum()
    {
        CharacterProgressionState state = CreateState();
        var writer = new CanonicalWriter();
        state.WriteCanonical(writer);
        CharacterProgressionState restored = CharacterProgressionState.ReadCanonical(writer.ToArray());
        var restoredWriter = new CanonicalWriter(); restored.WriteCanonical(restoredWriter);
        Assert.Equal(writer.ComputeChecksum(), restoredWriter.ComputeChecksum());
    }

    [Fact]
    public void Experience_awards_cannot_be_duplicated()
    {
        CharacterProgressionState state = CreateState();
        var system = new ExperienceSystem();
        var award = new ExperienceAward(new(10, 20), "quest", "clinic", 500, ExperienceRepeatPolicy.Once, 1);
        Assert.True(system.Grant(state, award).Accepted);
        Assert.False(system.Grant(state, award).Accepted);
    }

    [Fact]
    public void Perk_catalog_contains_forty_valid_perks()
    {
        PerkCatalog catalog = Phase12Content.CreatePerkCatalog();
        PerkValidationReport report = catalog.Validate();
        Assert.Equal(40, catalog.All.Count);
        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(x => x.Message)));
        Assert.True(report.BehaviorOrOptionPercent >= 70);
    }

    [Fact]
    public void Six_build_gate_uses_six_distinct_routes()
    {
        BuildViabilityReport report = new BuildViabilitySimulator().EvaluateSixBuildGate(Phase12BuildFixtures.SixBuilds, Phase12BuildFixtures.ProspectClinicRoutes);
        Assert.True(report.Passed);
        Assert.Equal(6, report.Results.Select(x => x.RouteId).Distinct().Count());
    }

    [Fact]
    public void Skill_checks_support_costly_success_without_rerolling()
    {
        CharacterProgressionState state = CreateState();
        var request = new SkillCheckRequest(state.CharacterId, SkillId.Science, 30, true, "terminal-a", new(1, 1), []);
        SkillCheckResult first = new SkillCheckResolver().Resolve(state, request);
        SkillCheckResult second = new SkillCheckResolver().Resolve(state, request);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.EffectiveSkill, second.EffectiveSkill);
        Assert.Equal(first.Margin, second.Margin);
    }

    private static CharacterProgressionState CreateState()
    {
        var attributes = new AttributeSet();
        SkillSet skills = Phase12SkillDefinitions.CreateStartingSkills(attributes, [SkillId.Ballistics, SkillId.Engineering, SkillId.Influence]);
        var state = new CharacterProgressionState(new StableId(1, 2), attributes, skills);
        state.ConfigureCreation([SkillId.Ballistics, SkillId.Engineering, SkillId.Influence], "relay-technician", ["methodical", "adaptable"], []);
        return state;
    }
}
