
using Roentgen.Audio;
using Roentgen.Audio.Playroma;
using Roentgen.Core;
namespace Roentgen.Audio.StressTests;
public sealed class Phase18StressTests
{
    [Fact] public void Voice_storm_remains_finite_and_bounded()
    {
        using var runtime=new PlayromaAudioRuntime(0x524F454E5447454EUL,new AudioSettings{MaximumVoices=64}); runtime.Start();
        float[] block=new float[runtime.RenderBlockFrames*runtime.Channels];
        AudioEventKind[] eventKinds =
        [
            AudioEventKind.WeaponFire,
            AudioEventKind.Impact,
            AudioEventKind.FootContact,
            AudioEventKind.Injury,
            AudioEventKind.Explosion,
            AudioEventKind.MeleeContact,
            AudioEventKind.MachineMalfunction,
            AudioEventKind.FocusTarget
        ];
        for(int i=0;i<600;i++)
        {
            for(int j=0;j<eventKinds.Length;j++) runtime.Submit(SemanticAudioEvent.Create(StableIdFactory.Create("stress.event",$"{i}:{j}"),(ulong)i,eventKinds[j],AudioEmitterState.At(new(j,0,-j)),parameters:new Dictionary<string,AudioValue>{{"variant",AudioValue.FromText("industrial-rifle")},{"surface",AudioValue.FromInt64((long)AudioSurface.Concrete)},{"gait",AudioValue.FromInt64((long)AudioGait.Run)}}));
            runtime.Render(block,runtime.RenderBlockFrames); runtime.PumpControl(); Assert.All(block,static sample=>Assert.True(float.IsFinite(sample)));
        }
        Assert.Equal(0,runtime.Telemetry.NonFiniteSamples); Assert.Equal(0,runtime.Telemetry.FaultedVoices);
    }
}
