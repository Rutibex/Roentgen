
using System.Numerics;
using Playroma.Sound.Graph.Serialization;
using Playroma.Sound.Streaming.Runtime;
using Playroma.Sound.Streaming.Spatial;
using SoundGraphDefinition = global::Playroma.Sound.Graph.Definitions.SoundGraphDefinition;
using SoundSceneSnapshot = global::Playroma.Sound.Streaming.Snapshots.SoundSceneSnapshot;
using Roentgen.Audio;
using Roentgen.Audio.Playroma;
using Roentgen.Core;
namespace Roentgen.Audio.Playroma.Tests;
public sealed class Phase18PlayromaTests
{
    [Fact] public void Integrated_catalog_is_large_deterministic_and_serializable()
    {
        var first=IntegratedSoundCatalog.Build(1234,AudioQuality.RealtimeHigh); var second=IntegratedSoundCatalog.Build(1234,AudioQuality.RealtimeHigh);
        Assert.True(first.Count>=90); Assert.Equal(first.Keys.Order(),second.Keys.Order());
        var serializer=new SoundGraphSerializer(); foreach(string id in first.Keys) Assert.Equal(serializer.Serialize(first[id]),serializer.Serialize(second[id]));
    }
    [Fact] public void Upgraded_host_supports_zoned_voices_portals_lifecycle_and_snapshots()
    {
        var settings = new AudioSettings();
        IReadOnlyDictionary<string, SoundGraphDefinition> assets = IntegratedSoundCatalog.Build(77, AudioQuality.RealtimeHigh);
        using var host = new StreamingSceneHost(PlayromaSceneFactory.CreateGlobal(assets, settings, 77), 128, 8);
        host.Start();
        Assert.True(host.TrySetListener(new SoundListenerState(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, Vector3.Zero)));
        Assert.True(host.TrySetListenerZone("outdoor"));
        Assert.True(host.TrySetPortalState("portal.outdoor-small", 0.35f, 0.72f));
        var emitter = new SoundEmitterState(true, new Vector3(0f, 0f, 2f), Vector3.Zero, 1f, 50f, 0.4f, 0.2f);
        Assert.True(host.TryPlaySound("ui.confirm", new VoiceStartOptions("interface", 1f, 0f, 90, false, true, emitter, "small-room"), out HostedVoiceHandle voice));
        Assert.True(host.TrySetVoiceAcousticState(voice, emitter, "small-room"));
        Assert.True(host.TryRequestSnapshot(out HostedSnapshotRequest snapshotRequest));
        float[] block = new float[host.Format.SamplesPerBlock];
        bool started = false;
        SoundSceneSnapshot? snapshot = null;
        for (int i = 0; i < 16; i++)
        {
            host.Render(block, host.Format.BlockFrames);
            while (host.TryDequeueVoiceLifecycle(out HostedVoiceLifecycleEvent lifecycle))
                started |= lifecycle.Handle == voice && lifecycle.Kind == VoiceLifecycleKind.Started;
            if (snapshot is null) host.TryGetSnapshot(snapshotRequest, out snapshot);
        }
        Assert.True(started);
        Assert.NotNull(snapshot);
        Assert.Equal(host.CurrentMetadata.Id, snapshot!.SceneId);
    }

    [Fact] public void Runtime_renders_finite_procedural_audio()
    {
        using var runtime=new PlayromaAudioRuntime(1234,new AudioSettings()); runtime.Start();
        var evt=SemanticAudioEvent.Create(StableIdFactory.Create("event","fire"),1,AudioEventKind.WeaponFire,AudioEmitterState.At(new(0,0,-2)),itemId:StableIdFactory.Create("weapon","rifle"),parameters:new Dictionary<string,AudioValue>{{"variant",AudioValue.FromText("industrial-rifle")}});
        Assert.True(runtime.Submit(evt)); float[] data=new float[runtime.RenderBlockFrames*runtime.Channels];
        for(int i=0;i<24;i++) runtime.Render(data,runtime.RenderBlockFrames);
        Assert.All(data,static value=>Assert.True(float.IsFinite(value))); Assert.Contains(data,static value=>Math.Abs(value)>0.000001f);
    }
}
