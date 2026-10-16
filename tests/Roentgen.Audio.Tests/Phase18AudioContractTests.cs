
using Roentgen.Audio;
using Roentgen.Contracts;
using Roentgen.Core;
namespace Roentgen.Audio.Tests;
public sealed class Phase18AudioContractTests
{
    [Fact] public void Identity_is_stable_and_event_variation_is_isolated()
    {
        StableId item=StableIdFactory.Create("weapon","test");
        SemanticAudioEvent a=SemanticAudioEvent.Create(StableIdFactory.Create("event","a"),10,AudioEventKind.WeaponFire,AudioEmitterState.At(AudioVector3.Zero),itemId:item,parameters:new Dictionary<string,AudioValue>{{"variant",AudioValue.FromText("industrial-rifle")}});
        SemanticAudioEvent b=a with { EventId=StableIdFactory.Create("event","b"), SimulationTick=11 };
        AudioIdentity first=AudioIdentityFactory.Resolve(a); AudioIdentity again=AudioIdentityFactory.Resolve(a); AudioIdentity second=AudioIdentityFactory.Resolve(b);
        Assert.Equal(first,again); Assert.Equal(first.FamilySeed,second.FamilySeed); Assert.NotEqual(first.RootSeed,second.RootSeed);
    }
    [Fact] public void Registry_is_stable_and_priority_ordered()
    {
        var registry=new AudioEmitterRegistry();
        StableId low=StableIdFactory.Create("emitter","low"), high=StableIdFactory.Create("emitter","high");
        Assert.True(registry.Register(new(low,low,AudioEmitterKind.Machine,10,true,1,100),AudioEmitterState.At(new(1,0,0))));
        Assert.True(registry.Register(new(high,high,AudioEmitterKind.Weapon,90,true,1,100),AudioEmitterState.At(new(8,0,0))));
        Assert.Equal(high,registry.SelectAcousticUpdateCandidates(AudioVector3.Zero,1)[0].Descriptor.EmitterId);
    }
    [Fact] public void Settings_loader_reads_the_integrated_audio_section()
    {
        const string json = """{ "audio": { "enabled": true, "masterVolume": 0.75, "quality": "realtimeHigh", "maximumVoices": 64, "targetQueuedBuffers": 3, "renderBlockFrames": 512, "submittedBufferFrames": 1024, "pcmRingBlocks": 8, "occlusionRayBudgetPerFrame": 6, "dynamicRange": "night" } }""";
        AudioSettings settings = AudioSettingsLoader.LoadFromJson(json);
        Assert.Equal(0.75f, settings.MasterVolume);
        Assert.Equal(AudioDynamicRange.Night, settings.DynamicRange);
        Assert.Equal(64, settings.MaximumVoices);
    }

    [Fact] public void Silent_runtime_consumes_commands_without_gameplay_effects()
    {
        using var runtime=new SilentAudioRuntime(new AudioSettings()); runtime.Start();
        Assert.True(runtime.Submit(SemanticAudioEvent.Create(StableIdFactory.Create("event","silent"),1,AudioEventKind.InterfaceConfirm,AudioEmitterState.At(AudioVector3.Zero))));
        Assert.Equal(AudioRuntimeState.SilentFallback,runtime.State);
    }
}
