using Roentgen.Contracts;

namespace Roentgen.Audio;

public sealed class AudioEmitterRegistry
{
    private readonly SortedDictionary<StableId, MutableEmitter> _emitters = new();

    public int Count => _emitters.Count;
    public int VirtualizedCount => _emitters.Values.Count(static value => value.Virtualized);

    public bool Register(AudioEmitterDescriptor descriptor, AudioEmitterState initialState)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.EmitterId.IsEmpty) throw new ArgumentException("Emitter ID cannot be empty.", nameof(descriptor));
        Validate(initialState);
        return _emitters.TryAdd(descriptor.EmitterId, new MutableEmitter(descriptor, initialState));
    }

    public bool Update(StableId id, AudioEmitterState state, ulong tick)
    {
        Validate(state);
        if (!_emitters.TryGetValue(id, out MutableEmitter? emitter)) return false;
        emitter.State = state;
        emitter.LastUpdateTick = tick;
        return true;
    }

    public bool SetVoice(StableId id, AudioVoiceHandle voice)
    {
        if (!_emitters.TryGetValue(id, out MutableEmitter? emitter)) return false;
        emitter.Voice = voice;
        return true;
    }

    public bool SetVirtualized(StableId id, bool virtualized)
    {
        if (!_emitters.TryGetValue(id, out MutableEmitter? emitter)) return false;
        emitter.Virtualized = virtualized;
        return true;
    }

    public bool Remove(StableId id) => _emitters.Remove(id);

    public bool TryGet(StableId id, out AudioEmitterSnapshot snapshot)
    {
        if (_emitters.TryGetValue(id, out MutableEmitter? emitter))
        {
            snapshot = emitter.Snapshot();
            return true;
        }
        snapshot = default!;
        return false;
    }

    public IReadOnlyList<AudioEmitterSnapshot> Snapshot() => _emitters.Values.Select(static value => value.Snapshot()).ToArray();

    public IReadOnlyList<AudioEmitterSnapshot> SelectAcousticUpdateCandidates(AudioVector3 listener, int budget)
    {
        if (budget <= 0) return [];
        return _emitters.Values
            .Where(static value => value.Descriptor.Kind != AudioEmitterKind.Interface && value.Descriptor.Kind != AudioEmitterKind.Focus)
            .OrderByDescending(static value => value.Descriptor.Priority)
            .ThenBy(value => DistanceSquared(value.State.Position, listener))
            .ThenBy(static value => value.Descriptor.EmitterId)
            .Take(budget)
            .Select(static value => value.Snapshot())
            .ToArray();
    }

    private static double DistanceSquared(AudioVector3 a, AudioVector3 b)
    {
        double x = a.X - b.X;
        double y = a.Y - b.Y;
        double z = a.Z - b.Z;
        return (x * x) + (y * y) + (z * z);
    }

    private static void Validate(AudioEmitterState state)
    {
        if (!state.Position.IsFinite || !state.Velocity.IsFinite) throw new ArgumentOutOfRangeException(nameof(state));
        if (!float.IsFinite(state.MinimumDistance) || !float.IsFinite(state.MaximumDistance) || state.MinimumDistance < 0f || state.MaximumDistance <= state.MinimumDistance) throw new ArgumentOutOfRangeException(nameof(state));
        if (!float.IsFinite(state.Occlusion) || !float.IsFinite(state.Obstruction)) throw new ArgumentOutOfRangeException(nameof(state));
    }

    private sealed class MutableEmitter
    {
        public MutableEmitter(AudioEmitterDescriptor descriptor, AudioEmitterState state)
        {
            Descriptor = descriptor;
            State = state;
            Voice = AudioVoiceHandle.Invalid;
        }
        public AudioEmitterDescriptor Descriptor { get; }
        public AudioEmitterState State { get; set; }
        public AudioVoiceHandle Voice { get; set; }
        public ulong LastUpdateTick { get; set; }
        public bool Virtualized { get; set; }
        public AudioEmitterSnapshot Snapshot() => new(Descriptor, State, Voice, LastUpdateTick, Virtualized);
    }
}
