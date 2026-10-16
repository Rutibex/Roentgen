namespace Roentgen.Audio;

public sealed record AudioSettings
{
    public bool Enabled { get; init; } = true;
    public float MasterVolume { get; init; } = 1f;
    public float MusicVolume { get; init; } = 0.8f;
    public float InterfaceVolume { get; init; } = 0.9f;
    public float WeaponsVolume { get; init; } = 1f;
    public float CreaturesVolume { get; init; } = 1f;
    public float FoleyVolume { get; init; } = 0.9f;
    public float AmbienceVolume { get; init; } = 0.8f;
    public float FocusVolume { get; init; } = 0.9f;
    public AudioQuality Quality { get; init; } = AudioQuality.RealtimeHigh;
    public int MaximumVoices { get; init; } = 96;
    public int TargetQueuedBuffers { get; init; } = 4;
    public int RenderBlockFrames { get; init; } = 512;
    public int SubmittedBufferFrames { get; init; } = 1024;
    public int PcmRingBlocks { get; init; } = 12;
    public int OcclusionRayBudgetPerFrame { get; init; } = 12;
    public AudioDynamicRange DynamicRange { get; init; } = AudioDynamicRange.Standard;
    public bool ReduceSuddenSounds { get; init; }
    public bool ReduceHighFrequencyDiagnosticSounds { get; init; }
    public bool MonoOutput { get; init; }
    public bool MuteWhenUnfocused { get; init; }
    public bool ContinueAmbienceInMenus { get; init; } = true;

    public AudioSettings Validate()
    {
        static float Volume(float value, string name) => float.IsFinite(value) && value is >= 0f and <= 1f
            ? value : throw new ArgumentOutOfRangeException(name);
        _ = Volume(MasterVolume, nameof(MasterVolume));
        _ = Volume(MusicVolume, nameof(MusicVolume));
        _ = Volume(InterfaceVolume, nameof(InterfaceVolume));
        _ = Volume(WeaponsVolume, nameof(WeaponsVolume));
        _ = Volume(CreaturesVolume, nameof(CreaturesVolume));
        _ = Volume(FoleyVolume, nameof(FoleyVolume));
        _ = Volume(AmbienceVolume, nameof(AmbienceVolume));
        _ = Volume(FocusVolume, nameof(FocusVolume));
        if (MaximumVoices is < 16 or > 512) throw new ArgumentOutOfRangeException(nameof(MaximumVoices));
        if (TargetQueuedBuffers is < 2 or > 8) throw new ArgumentOutOfRangeException(nameof(TargetQueuedBuffers));
        if (RenderBlockFrames is not (256 or 512 or 1024)) throw new ArgumentOutOfRangeException(nameof(RenderBlockFrames));
        if (SubmittedBufferFrames < RenderBlockFrames || SubmittedBufferFrames % RenderBlockFrames != 0) throw new ArgumentOutOfRangeException(nameof(SubmittedBufferFrames));
        if (PcmRingBlocks is < 4 or > 64) throw new ArgumentOutOfRangeException(nameof(PcmRingBlocks));
        if (OcclusionRayBudgetPerFrame is < 0 or > 128) throw new ArgumentOutOfRangeException(nameof(OcclusionRayBudgetPerFrame));
        return this;
    }
}

public static class AudioMixSnapshots
{
    public static IReadOnlyDictionary<string, float> GetBusGains(AudioMixSnapshotKind kind) => kind switch
    {
        AudioMixSnapshotKind.Gameplay => Gains(1f, 1f, 1f, 1f, 1f),
        AudioMixSnapshotKind.PauseMenu => Gains(0.32f, 1f, 0f, 0.5f, 0.45f),
        AudioMixSnapshotKind.Dialogue => Gains(0.3f, 0.9f, 0.15f, 0.35f, 0.35f),
        AudioMixSnapshotKind.FocusPlanning => Gains(0.18f, 0.9f, 1f, 0.25f, 0.2f),
        AudioMixSnapshotKind.FocusExecution => Gains(0.75f, 0.85f, 0.9f, 0.8f, 0.65f),
        AudioMixSnapshotKind.Loading => Gains(0.05f, 0.3f, 0f, 0.1f, 0.1f),
        AudioMixSnapshotKind.Muted => Gains(0f, 0f, 0f, 0f, 0f),
        _ => Gains(1f, 1f, 1f, 1f, 1f)
    };

    private static IReadOnlyDictionary<string, float> Gains(float world, float ui, float focus, float ambience, float music) =>
        new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["world"] = world,
            ["interface"] = ui,
            ["focus"] = focus,
            ["ambience"] = ambience,
            ["music"] = music
        };
}
