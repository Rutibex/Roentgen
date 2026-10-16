namespace Roentgen.Music.Playroma;

public sealed class TransitionBridgeGenerator
{
    public MusicTransitionPlan Plan(AdaptiveCueDocument source, AdaptiveCueDocument destination, MusicTransitionQuantization quantization, ulong tick, float defaultCrossfade)
    {
        double tempoDelta = Math.Abs(source.Plan.Harmony.Tempo - destination.Plan.Harmony.Tempo);
        bool sameKey = source.Plan.Harmony.KeyRoot == destination.Plan.Harmony.KeyRoot && source.Plan.Harmony.Scale == destination.Plan.Harmony.Scale;
        MusicTransitionKind kind = sameKey && tempoDelta < 8 ? MusicTransitionKind.Crossfade
            : tempoDelta > 35 ? (source.Plan.Harmony.Tempo < destination.Plan.Harmony.Tempo ? MusicTransitionKind.Accelerate : MusicTransitionKind.Decelerate)
            : source.Plan.Situation is MusicSituation.Revelation or MusicSituation.Tragedy ? MusicTransitionKind.DissonantRupture
            : MusicTransitionKind.HarmonicPivot;
        string bridge = $"bridge-{kind.ToString().ToLowerInvariant()}-{source.Plan.Identity.Seed:x8}-{destination.Plan.Identity.Seed:x8}";
        float crossfade = kind == MusicTransitionKind.Crossfade ? defaultCrossfade : Math.Max(0.35f, defaultCrossfade * 0.72f);
        return new MusicTransitionPlan(source.Plan.Identity, destination.Plan.Identity, kind, quantization, checked((long)tick), crossfade, bridge, $"{source.Plan.Harmony.Scale}/{source.Plan.Harmony.Tempo:0.#} to {destination.Plan.Harmony.Scale}/{destination.Plan.Harmony.Tempo:0.#}");
    }
}
