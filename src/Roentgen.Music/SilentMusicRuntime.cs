using Roentgen.Contracts;

namespace Roentgen.Music;

public sealed class SilentMusicRuntime : IMusicRuntime
{
    private MusicRuntimeState _state = MusicRuntimeState.SilentFallback;
    public MusicRuntimeState State => _state;
    public MusicTelemetrySnapshot Telemetry => CreateTelemetry(_state);
    public static MusicTelemetrySnapshot CreateTelemetry(MusicRuntimeState state = MusicRuntimeState.SilentFallback) => new(state, MusicSituation.Silence, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, string.Empty, string.Empty, string.Empty);
    public void Start() => _state = MusicRuntimeState.SilentFallback;
    public void Stop() => _state = MusicRuntimeState.Stopped;
    public void Pause() { }
    public void Resume() { }
    public void UpdateContext(MusicContext context) { }
    public void RequestStinger(MusicSituation situation, StableId subjectId, ulong tick) { }
    public MusicSnapshotV19 CaptureSnapshot() => new(19, string.Empty, string.Empty, string.Empty, string.Empty, [], MusicSituation.Silence, default, string.Empty, 0, new Dictionary<MusicStemKind, float>(), 0f, null, new Dictionary<string, ulong>());
    public void RestoreSnapshot(MusicSnapshotV19 snapshot) { }
    public void PumpControl() { }
    public void Dispose() => _state = MusicRuntimeState.Disposed;
}
