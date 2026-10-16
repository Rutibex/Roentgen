using Roentgen.Audio.MonoGame;
using Roentgen.Audio.Playroma;
using Roentgen.Contracts;

namespace Roentgen.Audio.Tests;

public sealed class RenderWorkerRegressionTests
{
    [Fact]
    public void BackgroundWorkerStartsAndProducesNonSilentPlayromaPcm()
    {
        var settings = new AudioSettings();
        using var runtime = new PlayromaAudioRuntime(1234, settings);
        runtime.Start();
        Assert.Equal(AudioRuntimeState.Running, runtime.State);
        runtime.UpdateListener(AudioListenerState.Default);
        Assert.True(runtime.Submit(SemanticAudioEvent.Create(new StableId(1, 99), 1,
            AudioEventKind.FootContact, AudioEmitterState.At(AudioVector3.Zero))));
        runtime.PumpControl();
        var ring = new PcmBlockRingBuffer(12, settings.SubmittedBufferFrames * runtime.Channels * sizeof(short));
        using var worker = new PlayromaRenderWorker(runtime, ring, settings.SubmittedBufferFrames);
        worker.Start();
        Assert.True(SpinWait.SpinUntil(() => ring.Count >= 4, TimeSpan.FromSeconds(5)), "Background render thread did not produce PCM.");
        bool audible = false;
        for (int i = 0; i < 4; i++)
        {
            Assert.True(ring.TryRead(out var block));
            audible |= block.Any(value => value != 0);
        }
        Assert.True(audible, "Playroma produced only silence.");
        Assert.Equal(0, worker.NonFiniteSamples);
    }
}
