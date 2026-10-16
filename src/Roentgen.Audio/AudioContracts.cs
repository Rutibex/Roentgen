using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Audio;

public readonly record struct AudioVector3(float X, float Y, float Z)
{
    public static AudioVector3 Zero => default;
    public static AudioVector3 Forward => new(0f, 0f, 1f);
    public static AudioVector3 Up => new(0f, 1f, 0f);
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}

public readonly record struct AudioListenerState(
    AudioVector3 Position,
    AudioVector3 Forward,
    AudioVector3 Up,
    AudioVector3 Velocity,
    string ZoneId)
{
    public static AudioListenerState Default => new(AudioVector3.Zero, AudioVector3.Forward, AudioVector3.Up, AudioVector3.Zero, string.Empty);
}

public readonly record struct AudioEmitterState(
    AudioVector3 Position,
    AudioVector3 Velocity,
    float MinimumDistance,
    float MaximumDistance,
    float Occlusion,
    float Obstruction,
    string ZoneId)
{
    public static AudioEmitterState At(AudioVector3 position) => new(position, AudioVector3.Zero, 1f, 100f, 0f, 0f, string.Empty);
}

public readonly record struct AudioValue(long Integer, float Number, string Text, StableId Id)
{
    public static AudioValue FromInt64(long value) => new(value, 0f, string.Empty, StableId.Empty);
    public static AudioValue FromSingle(float value) => new(0L, value, string.Empty, StableId.Empty);
    public static AudioValue FromText(string value) => new(0L, 0f, value ?? string.Empty, StableId.Empty);
    public static AudioValue FromId(StableId value) => new(0L, 0f, string.Empty, value);
}

public sealed record SemanticAudioEvent(
    StableId EventId,
    ulong SimulationTick,
    AudioEventKind Kind,
    StableId SourceId,
    StableId TargetId,
    StableId ItemId,
    StableId LocationId,
    AudioEmitterState Emitter,
    IReadOnlyDictionary<string, AudioValue> Parameters)
{
    public static SemanticAudioEvent Create(
        StableId eventId,
        ulong tick,
        AudioEventKind kind,
        AudioEmitterState emitter,
        StableId sourceId = default,
        StableId targetId = default,
        StableId itemId = default,
        StableId locationId = default,
        IReadOnlyDictionary<string, AudioValue>? parameters = null) =>
        new(eventId, tick, kind, sourceId, targetId, itemId, locationId, emitter,
            parameters ?? EmptyParameters.Instance);

    private sealed class EmptyParameters : Dictionary<string, AudioValue>
    {
        public static EmptyParameters Instance { get; } = new();
        private EmptyParameters() : base(StringComparer.Ordinal) { }
    }
}

public readonly record struct AudioVoiceHandle(int Value)
{
    public bool IsValid => Value >= 0;
    public static AudioVoiceHandle Invalid => new(-1);
}

public sealed record AudioVoiceLifecycleEvent(
    AudioVoiceHandle Handle,
    string AssetId,
    AudioLifecycleKind Kind,
    long SampleFrame);

public sealed record AudioEmitterDescriptor(
    StableId EmitterId,
    StableId OwnerId,
    AudioEmitterKind Kind,
    int Priority,
    bool Persistent,
    float MinimumDistance,
    float MaximumDistance);

public sealed record AudioEmitterSnapshot(
    AudioEmitterDescriptor Descriptor,
    AudioEmitterState State,
    AudioVoiceHandle Voice,
    ulong LastUpdateTick,
    bool Virtualized);

public sealed record AudioTelemetrySnapshot(
    AudioRuntimeState RuntimeState,
    int SampleRate,
    int Channels,
    int RenderBlockFrames,
    int PendingPcmBlocks,
    int PcmRingCapacity,
    long RenderedFrames,
    long SubmittedBuffers,
    long Underruns,
    long RingOverflows,
    long CommandRejections,
    long StartedVoices,
    long RejectedVoices,
    long StolenVoices,
    long CompletedVoices,
    long FaultedVoices,
    int ActiveEmitters,
    int VirtualEmitters,
    int OcclusionQueries,
    int NonFiniteSamples,
    int ClippedSamples,
    double LastRenderMilliseconds,
    double PeakRenderMilliseconds,
    string CurrentSnapshot,
    string CurrentRegion,
    string FaultMessage);

public interface IAudioRuntime : IDisposable
{
    AudioRuntimeState State { get; }
    AudioTelemetrySnapshot Telemetry { get; }
    void Start();
    void Stop();
    void Pause();
    void Resume();
    void UpdateListener(AudioListenerState listener);
    bool Submit(SemanticAudioEvent audioEvent);
    bool RegisterEmitter(AudioEmitterDescriptor descriptor, AudioEmitterState initialState);
    bool UpdateEmitter(StableId emitterId, AudioEmitterState state, ulong tick);
    bool RemoveEmitter(StableId emitterId);
    void ApplyMixSnapshot(AudioMixSnapshotKind snapshot, float transitionSeconds = 0.15f);
    void SetRegion(AudioRegion region, ulong regionSeed);
    void PumpControl();
}

public interface IAudioRuntimePcmSource
{
    int SampleRate { get; }
    int Channels { get; }
    int RenderBlockFrames { get; }
    int Render(Span<float> destination, int requestedFrames);
}

public interface IAudioWorldQuery
{
    bool TryGetEmitterState(StableId entityId, out AudioEmitterState state);
    bool TryGetListener(out AudioListenerState listener);
}

public interface IAcousticQueryService
{
    AcousticQueryResult Query(in AudioListenerState listener, in AudioEmitterState emitter);
}

public readonly record struct AcousticQueryResult(float Occlusion, float Obstruction, string SourceZoneId, string ListenerZoneId);
