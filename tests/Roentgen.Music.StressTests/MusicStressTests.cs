using Xunit;
using Roentgen.Music;
using Roentgen.Music.Playroma;

namespace Roentgen.Music.StressTests;

public sealed class MusicStressTests
{
    [Fact]
    public void RuntimeRendersFiniteBlocksAcrossSituationChanges()
    {
        string root = FindRoot();
        var settings = new MusicSettings { MinimumSituationDwellTicks = 0, TransitionCooldownTicks = 0 };
        using var runtime = new AdaptiveMusicRuntime(0x524F454E5447454EUL, settings, root);
        runtime.Start();
        float[] block = new float[settings.RenderBlockFrames * 2];
        for (int i = 0; i < 300; i++)
        {
            float intensity = (i / 50) % 2 == 0 ? 0.05f : 0.95f;
            runtime.UpdateContext(MusicContext.Default with { SimulationTick = (ulong)i * 20UL, CombatIntensity = intensity, Threat = intensity > 0.5f ? MusicThreatLevel.Overwhelming : MusicThreatLevel.None });
            runtime.PumpControl();
            runtime.Render(block, settings.RenderBlockFrames);
            Assert.All(block, static value => Assert.True(float.IsFinite(value)));
            Thread.Sleep(1);
        }
        Assert.Equal(MusicRuntimeState.Running, runtime.State);
    }

    private static string FindRoot()
    {
        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++) { if (File.Exists(Path.Combine(current, "content", "music", "soundfonts", "default.sf2"))) return current; DirectoryInfo? parent = Directory.GetParent(current); if (parent is null) break; current = parent.FullName; }
        return AppContext.BaseDirectory;
    }
}
