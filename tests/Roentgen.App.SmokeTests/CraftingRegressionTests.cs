using Roentgen.App;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Items;
using Roentgen.Rpg;

namespace Roentgen.App.SmokeTests;

public sealed class CraftingRegressionTests
{
    [Fact]
    public void Repeated_crafts_on_same_paused_tick_get_unique_output_ids()
    {
        StableId playerId = StableIdFactory.Create("player", "crafting-regression");
        var runtime = Phase13ItemRuntime.CreateIntegrated(
            playerId,
            new CampaignSeed(4242),
            new StartingEquipmentGrant("test", []),
            75_000,
            new FixedSkillProvider(100));

        const ulong pausedTick = 1234;
        CraftingResult first = runtime.CraftKnownRecipe("assemble-light-cartridges", pausedTick);
        CraftingResult second = runtime.CraftKnownRecipe("assemble-light-cartridges", pausedTick);

        Assert.True(first.Success, first.Message);
        Assert.True(second.Success, second.Message);
        Assert.Single(first.OutputItemIds);
        Assert.Single(second.OutputItemIds);
        Assert.NotEqual(first.OutputItemIds[0], second.OutputItemIds[0]);
        Assert.True(runtime.World.TryGetItem(first.OutputItemIds[0], out _));
        Assert.True(runtime.World.TryGetItem(second.OutputItemIds[0], out _));
    }

    private sealed class FixedSkillProvider(int skill) : IItemSkillCapabilityProvider
    {
        public int GetEffectiveSkill(StableId actorId, SkillId requestedSkill) => skill;
    }
}
