using Xunit;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Music;

namespace Roentgen.Music.Tests;

public sealed class MusicContractTests
{
    [Fact] public void DirectorUsesHysteresisAndEntersCombat()
    {
        var director = new MusicDirector(new MusicSettings { MinimumSituationDwellTicks = 0, TransitionCooldownTicks = 0 });
        Assert.Equal(MusicSituation.WastelandExploration, director.Evaluate(MusicContext.Default));
        Assert.Equal(MusicSituation.EliteCombat, director.Evaluate(MusicContext.Default with { SimulationTick = 1, CombatIntensity = 0.95f, Threat = MusicThreatLevel.Overwhelming }));
    }

    [Fact] public void MotifsAreStableForOwner()
    {
        StableId campaign = StableIdFactory.Create("test", "campaign");
        StableId owner = StableIdFactory.Create("test", "faction");
        var registry = new CampaignMotifRegistry(campaign);
        MusicMotifDefinition first = registry.GetOrCreate(owner, "faction");
        MusicMotifDefinition second = registry.GetOrCreate(owner, "faction");
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(8, first.ScaleDegreeContour.Count);
    }

    [Fact] public void SnapshotRoundTrips()
    {
        var silent = new SilentMusicRuntime();
        byte[] bytes = MusicSnapshotV19Serializer.Write(silent.CaptureSnapshot());
        MusicSnapshotV19 copy = MusicSnapshotV19Serializer.Read(bytes);
        Assert.Equal(19, copy.SchemaVersion);
    }
}
