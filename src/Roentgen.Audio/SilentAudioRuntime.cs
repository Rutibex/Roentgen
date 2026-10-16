using Roentgen.Contracts;

namespace Roentgen.Audio;

public sealed class SilentAudioRuntime : IAudioRuntime
{
    private readonly AudioEmitterRegistry _emitters = new();
    private readonly AudioSettings _settings;

    public SilentAudioRuntime(AudioSettings? settings = null) => _settings = (settings ?? new AudioSettings()).Validate();
    private AudioRuntimeState _state = AudioRuntimeState.SilentFallback;
    private AudioRegion _region;
    private AudioMixSnapshotKind _snapshot = AudioMixSnapshotKind.Gameplay;

    public AudioRuntimeState State => _state;
    public AudioTelemetrySnapshot Telemetry => new(_state, 48_000, 2, _settings.RenderBlockFrames, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, _emitters.Count, _emitters.VirtualizedCount, 0, 0, 0, 0, 0, _snapshot.ToString(), _region.ToString(), string.Empty);
    public void Start() => _state = AudioRuntimeState.SilentFallback;
    public void Stop() => _state = AudioRuntimeState.Stopped;
    public void Pause() { }
    public void Resume() { }
    public void UpdateListener(AudioListenerState listener) { }
    public bool Submit(SemanticAudioEvent audioEvent) => _state != AudioRuntimeState.Disposed;
    public bool RegisterEmitter(AudioEmitterDescriptor descriptor, AudioEmitterState initialState) => _emitters.Register(descriptor, initialState);
    public bool UpdateEmitter(StableId emitterId, AudioEmitterState state, ulong tick) => _emitters.Update(emitterId, state, tick);
    public bool RemoveEmitter(StableId emitterId) => _emitters.Remove(emitterId);
    public void ApplyMixSnapshot(AudioMixSnapshotKind snapshot, float transitionSeconds = 0.15f) => _snapshot = snapshot;
    public void SetRegion(AudioRegion region, ulong regionSeed) => _region = region;
    public void PumpControl() { }
    public void Dispose() => _state = AudioRuntimeState.Disposed;

    public static AudioTelemetrySnapshot CreateTelemetry() => new(
        AudioRuntimeState.SilentFallback,
        48_000,
        2,
        512,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0d,
        0d,
        AudioMixSnapshotKind.Gameplay.ToString(),
        AudioRegion.BrokenMeridian.ToString(),
        string.Empty);
}
