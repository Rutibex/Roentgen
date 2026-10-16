using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Playroma.Sound.Core.Audio;
using Playroma.Sound.Streaming.Runtime;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Music.Playroma;

public sealed class AdaptiveMusicRuntime : IMusicRuntime, IStreamingAudioSource
{
    private readonly record struct StemMix(float Foundation, float Pulse, float Percussion, float Melody, float Texture, float Danger)
    {
        public bool NearlyEquals(StemMix other) =>
            Math.Abs(Foundation - other.Foundation) < 0.001f &&
            Math.Abs(Pulse - other.Pulse) < 0.001f &&
            Math.Abs(Percussion - other.Percussion) < 0.001f &&
            Math.Abs(Melody - other.Melody) < 0.001f &&
            Math.Abs(Texture - other.Texture) < 0.001f &&
            Math.Abs(Danger - other.Danger) < 0.001f;

        public Dictionary<MusicStemKind, float> ToDictionary() => new()
        {
            [MusicStemKind.Foundation] = Foundation,
            [MusicStemKind.Pulse] = Pulse,
            [MusicStemKind.Percussion] = Percussion,
            [MusicStemKind.Melody] = Melody,
            [MusicStemKind.Texture] = Texture,
            [MusicStemKind.Danger] = Danger
        };
    }

    private abstract record RenderCommand;
    private sealed record SwitchCueCommand(AdaptiveCueDocument Cue, MusicTransitionPlan? Transition, long RestoreTick) : RenderCommand;
    private sealed record StingerCommand(AdaptiveCueDocument Cue) : RenderCommand;
    private sealed record StemCommand(float Foundation, float Pulse, float Percussion, float Melody, float Texture, float Danger) : RenderCommand;

    private readonly MusicSettings _settings;
    private readonly ulong _campaignSeed;
    private readonly RoentgenSoundFontAsset _font;
    private readonly MusicDirector _director;
    private readonly CampaignMotifRegistry _motifs;
    private readonly MusicPreparationWorker _preparation;
    private readonly FixedSpscQueue<RenderCommand> _commands;
    private readonly SoundFontDeck _deckA;
    private readonly SoundFontDeck _deckB;
    private readonly SoundFontDeck _stinger;
    private readonly TransitionBridgeGenerator _bridges = new();
    private readonly float[] _scratchA;
    private readonly float[] _scratchB;
    private readonly float[] _scratchStinger;
    private MusicContext _context = MusicContext.Default;
    private SoundFontDeck _active;
    private SoundFontDeck _inactive;
    private AdaptiveCueDocument? _pendingCue;
    private MusicTransitionPlan? _pendingTransition;
    private long _transitionBoundarySample;
    private long _crossfadeSamples;
    private long _crossfadePosition;
    private long _pendingRestoreTick;
    private bool _crossfading;
    private bool _running;
    private bool _disposed;
    private long _requestSequence;
    private long _latestMainRequest;
    private MusicSituation _requestedSituation = MusicSituation.Silence;
    private ulong _requestedCueSeed;
    private MusicRuntimeState _state = MusicRuntimeState.Stopped;
    private string _fault = string.Empty;
    private long _renderedFrames;
    private long _completedTransitions;
    private long _lateTransitions;
    private long _fallbackTransitions;
    private long _commandRejections;
    private double _lastRenderMs;
    private double _peakRenderMs;
    private readonly Dictionary<string, AdaptiveCueDocument> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ulong> _stingerCooldowns = new(StringComparer.Ordinal);
    private StemMix _lastQueuedStemMix = new(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

    public AdaptiveMusicRuntime(ulong campaignSeed, MusicSettings settings, string baseDirectory)
    {
        _settings = settings.Validate();
        _campaignSeed = campaignSeed;
        Format = new AudioFormat(_settings.SampleRate, 2, _settings.RenderBlockFrames);
        _font = RoentgenSoundFontAsset.Load(_settings, baseDirectory);
        var catalog = RoentgenPresetCatalog.Load(_settings, baseDirectory, _font);
        var generator = new RoentgenCueGenerator(catalog, _font, Format.SampleRate);
        _preparation = new MusicPreparationWorker(generator, Math.Max(8, _settings.CommandCapacity / 4));
        _commands = new FixedSpscQueue<RenderCommand>(_settings.CommandCapacity);
        _deckA = new SoundFontDeck(_font, Format.SampleRate, Format.BlockFrames);
        _deckB = new SoundFontDeck(_font, Format.SampleRate, Format.BlockFrames);
        _stinger = new SoundFontDeck(_font, Format.SampleRate, Format.BlockFrames);
        _active = _deckA;
        _inactive = _deckB;
        _scratchA = new float[Format.SamplesPerBlock];
        _scratchB = new float[Format.SamplesPerBlock];
        _scratchStinger = new float[Format.SamplesPerBlock];
        _director = new MusicDirector(_settings);
        StableId campaignId = StableIdFactory.Create("campaign-music", campaignSeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _motifs = new CampaignMotifRegistry(campaignId);
        _ = _motifs.GetOrCreate(campaignId, "roentgen-central-mystery");
    }

    public AudioFormat Format { get; }
    public long CurrentSampleFrame => Interlocked.Read(ref _renderedFrames);
    public MusicRuntimeState State => _state;
    public MusicTelemetrySnapshot Telemetry => new(
        _state,
        _director.Current,
        _active.Cue?.Plan.Identity.CueId ?? string.Empty,
        _pendingCue?.Plan.Identity.CueId ?? string.Empty,
        ReferenceEquals(_active, _deckA) ? "A" : "B",
        _active.MusicalTick,
        CalculateBar(_active.Cue, _active.MusicalTick),
        CalculateBeat(_active.Cue, _active.MusicalTick),
        _active.ActiveNotes + _inactive.ActiveNotes + _stinger.ActiveNotes,
        Math.Max(_active.PeakPolyphony, Math.Max(_inactive.PeakPolyphony, _stinger.PeakPolyphony)),
        _commands.Count + _preparation.PendingRequests,
        _renderedFrames,
        _completedTransitions,
        _lateTransitions,
        _fallbackTransitions,
        _commandRejections,
        _lastRenderMs,
        _peakRenderMs,
        _font.Name,
        _font.Sha256,
        _fault);

    public void Start()
    {
        ThrowIfDisposed();
        _running = true;
        _state = MusicRuntimeState.Running;
        UpdateContext(_context with { SimulationTick = 0 });
    }

    public void Stop()
    {
        if (_disposed) return;
        _running = false;
        _deckA.Stop();
        _deckB.Stop();
        _stinger.Stop();
        _state = MusicRuntimeState.Stopped;
    }

    public void Pause()
    {
        if (_state != MusicRuntimeState.Running) return;
        if (!_settings.ContinueInPauseMenu || _settings.PauseMusicalTimeInPauseMenu) _running = false;
        _state = MusicRuntimeState.Paused;
    }

    public void Resume()
    {
        if (_state != MusicRuntimeState.Paused) return;
        _running = true;
        _state = MusicRuntimeState.Running;
    }

    public void UpdateContext(MusicContext context)
    {
        ThrowIfDisposed();
        _context = Normalize(context);
        MusicSituation desired = _director.Evaluate(_context);
        QueueStemMix(_context, desired);
        EnsureMotifs(_context);
        ulong seed = DeriveCueSeed(desired, _context);
        if (desired == _requestedSituation && seed == _requestedCueSeed && (_active.Cue is not null || _latestMainRequest != 0)) return;
        _requestedSituation = desired;
        _requestedCueSeed = seed;
        string cacheKey = CacheKey(desired, seed);
        if (_cache.TryGetValue(cacheKey, out AdaptiveCueDocument? cached))
        {
            _latestMainRequest = 0;
            if (!QueueSwitch(cached, 0)) { _requestedSituation = MusicSituation.Silence; _requestedCueSeed = 0; }
            return;
        }
        long sequence = Interlocked.Increment(ref _requestSequence);
        _latestMainRequest = sequence;
        if (!_preparation.TryRequest(new MusicPreparationRequest(sequence, desired, _context, seed, false, 0)))
        {
            _latestMainRequest = 0;
            _requestedSituation = MusicSituation.Silence;
            _requestedCueSeed = 0;
            Interlocked.Increment(ref _commandRejections);
        }
    }

    public void RequestStinger(MusicSituation situation, StableId subjectId, ulong tick)
    {
        string key = situation + ":" + subjectId;
        if (_stingerCooldowns.TryGetValue(key, out ulong until) && tick < until) return;
        _stingerCooldowns[key] = tick + 300UL;
        ulong seed = DeriveCueSeed(situation, _context) ^ subjectId.High ^ subjectId.Low ^ tick;
        long sequence = Interlocked.Increment(ref _requestSequence);
        if (!_preparation.TryRequest(new MusicPreparationRequest(sequence, situation, _context, seed, true, 0))) Interlocked.Increment(ref _commandRejections);
    }

    public void PumpControl()
    {
        while (_preparation.TryTake(out MusicPreparationResult? result) && result is not null)
        {
            if (result.Error is not null)
            {
                _fault = result.Error.Message;
                if (!result.Stinger && result.Sequence == _latestMainRequest)
                {
                    _latestMainRequest = 0;
                    _requestedSituation = MusicSituation.Silence;
                    _requestedCueSeed = 0;
                    _state = _active.Cue is null ? MusicRuntimeState.Faulted : MusicRuntimeState.Running;
                }
                continue;
            }
            string key = CacheKey(result.Cue.Plan.Situation, result.Cue.Plan.Identity.Seed);
            AddCache(key, result.Cue);
            if (result.Stinger)
            {
                if (!_commands.TryEnqueue(new StingerCommand(result.Cue))) Interlocked.Increment(ref _commandRejections);
            }
            else if (result.Sequence == _latestMainRequest)
            {
                _latestMainRequest = 0;
                _fault = string.Empty;
                if (!QueueSwitch(result.Cue, result.RestoreTick)) { _requestedSituation = MusicSituation.Silence; _requestedCueSeed = 0; }
            }
        }
    }

    public MusicSnapshotV19 CaptureSnapshot()
    {
        AdaptiveCueDocument? cue = _active.Cue;
        MusicCueIdentity identity = cue?.Plan.Identity ?? default;
        string loopId = cue?.Plan.Loops.FirstOrDefault(static value => value.IsPrimary)?.Id ?? string.Empty;
        Dictionary<MusicStemKind, float> stems = BuildStemTargets(_context, _director.Current).ToDictionary();
        return new MusicSnapshotV19(19, RoentgenCueGenerator.GeneratorVersion, StyleLibraryFingerprint(), _font.AssetId, _font.Sha256, _motifs.Motifs, _director.Current, identity, loopId, _active.MusicalTick, stems, _context.CombatIntensity, _pendingTransition, new Dictionary<string, ulong>(_stingerCooldowns, StringComparer.Ordinal));
    }

    public void RestoreSnapshot(MusicSnapshotV19 snapshot)
    {
        if (snapshot.SchemaVersion != 19) throw new InvalidDataException("Unsupported music snapshot schema.");
        if (!snapshot.GeneratorVersion.Equals(RoentgenCueGenerator.GeneratorVersion, StringComparison.Ordinal)) throw new InvalidDataException("Music snapshot uses an incompatible cue generator version.");
        if (!snapshot.StyleLibraryFingerprint.Equals(StyleLibraryFingerprint(), StringComparison.Ordinal)) throw new InvalidDataException("Music snapshot uses an incompatible style library.");
        if (!snapshot.SoundFontAssetId.Equals(_font.AssetId, StringComparison.Ordinal)) throw new InvalidDataException("Music snapshot references a different SoundFont asset.");
        if (!snapshot.SoundFontSha256.Equals(_font.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Music snapshot was created with a different SoundFont.");
        _motifs.Restore(snapshot.Motifs);
        _stingerCooldowns.Clear();
        foreach ((string key, ulong value) in snapshot.StingerCooldowns) _stingerCooldowns[key] = value;
        MusicContext restoreContext = _context with { SimulationTick = _context.SimulationTick + 1 };
        long sequence = Interlocked.Increment(ref _requestSequence);
        _latestMainRequest = sequence;
        _requestedSituation = snapshot.Situation;
        _requestedCueSeed = snapshot.CurrentCue.Seed;
        QueueStemMix(StemMixFrom(snapshot.StemGains));
        if (!_preparation.TryRequest(new MusicPreparationRequest(sequence, snapshot.Situation, restoreContext, snapshot.CurrentCue.Seed, false, snapshot.MusicalTick)))
        {
            _latestMainRequest = 0;
            _requestedSituation = MusicSituation.Silence;
            _requestedCueSeed = 0;
            Interlocked.Increment(ref _commandRejections);
        }
    }

    public int Render(Span<float> destination, int requestedFrames)
    {
        ThrowIfDisposed();
        if (requestedFrames < 0 || requestedFrames > Format.BlockFrames) throw new ArgumentOutOfRangeException(nameof(requestedFrames));
        Span<float> output = destination[..(requestedFrames * 2)];
        output.Clear();
        if (!_running || !_settings.Enabled) return requestedFrames;
        long start = Stopwatch.GetTimestamp();
        DrainRenderCommands();
        StartPendingTransitionIfReady();
        Span<float> a = _scratchA.AsSpan(0, output.Length);
        Span<float> b = _scratchB.AsSpan(0, output.Length);
        Span<float> stinger = _scratchStinger.AsSpan(0, output.Length);
        a.Clear(); b.Clear(); stinger.Clear();
        _active.Render(a, requestedFrames);
        if (_crossfading) _inactive.Render(b, requestedFrames);
        _stinger.Render(stinger, requestedFrames);
        float dynamicRangeGain = _settings.DynamicRange switch
        {
            MusicDynamicRange.Wide => 1f,
            MusicDynamicRange.Night => 0.72f,
            _ => 0.9f
        };
        float outputGain = _settings.Volume * dynamicRangeGain;
        for (int frame = 0; frame < requestedFrames; frame++)
        {
            float currentGain = 1f;
            float nextGain = 0f;
            if (_crossfading)
            {
                float t = Math.Clamp((_crossfadePosition + frame) / (float)Math.Max(1L, _crossfadeSamples), 0f, 1f);
                currentGain = MathF.Cos(t * MathF.PI * 0.5f);
                nextGain = MathF.Sin(t * MathF.PI * 0.5f);
            }
            int sample = frame * 2;
            output[sample] = (a[sample] * currentGain + b[sample] * nextGain + stinger[sample] * 0.9f) * outputGain;
            output[sample + 1] = (a[sample + 1] * currentGain + b[sample + 1] * nextGain + stinger[sample + 1] * 0.9f) * outputGain;
        }
        if (_crossfading)
        {
            _crossfadePosition += requestedFrames;
            if (_crossfadePosition >= _crossfadeSamples) CompleteTransition();
        }
        Interlocked.Add(ref _renderedFrames, requestedFrames);
        double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        _lastRenderMs = elapsed;
        _peakRenderMs = Math.Max(_peakRenderMs, elapsed);
        return requestedFrames;
    }

    public void Reset()
    {
        _deckA.Stop(); _deckB.Stop(); _stinger.Stop();
        _pendingCue = null; _pendingTransition = null; _pendingRestoreTick = 0; _crossfading = false;
        _latestMainRequest = 0; _requestedSituation = MusicSituation.Silence; _requestedCueSeed = 0;
    }

    public bool IsRunning => _running;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        _preparation.Dispose();
        _deckA.Dispose(); _deckB.Dispose(); _stinger.Dispose();
        _state = MusicRuntimeState.Disposed;
    }

    private bool QueueSwitch(AdaptiveCueDocument cue, long restoreTick)
    {
        MusicTransitionPlan? transition = null;
        if (_active.Cue is not null)
        {
            MusicTransitionQuantization quantization = _director.SelectQuantization(_active.Cue.Plan.Situation, cue.Plan.Situation);
            transition = _bridges.Plan(_active.Cue, cue, quantization, _context.SimulationTick, _settings.DefaultCrossfadeSeconds);
        }
        if (_commands.TryEnqueue(new SwitchCueCommand(cue, transition, restoreTick))) return true;
        Interlocked.Increment(ref _commandRejections);
        return false;
    }

    private void QueueStemMix(MusicContext context, MusicSituation situation) => QueueStemMix(BuildStemTargets(context, situation));

    private void QueueStemMix(StemMix values)
    {
        if (_lastQueuedStemMix.NearlyEquals(values)) return;
        if (!_commands.TryEnqueue(new StemCommand(values.Foundation, values.Pulse, values.Percussion, values.Melody, values.Texture, values.Danger)))
        {
            Interlocked.Increment(ref _commandRejections);
            return;
        }
        _lastQueuedStemMix = values;
    }

    private static StemMix BuildStemTargets(MusicContext context, MusicSituation situation)
    {
        float intensity = Math.Clamp(context.CombatIntensity, 0f, 1f);
        float percussion = situation == MusicSituation.Dialogue ? 0.12f : situation is MusicSituation.LowCombat or MusicSituation.FullCombat or MusicSituation.EliteCombat ? 0.55f + intensity * 0.45f : 0.35f;
        float melody = situation == MusicSituation.Dialogue ? 0.32f : context.IsInRoentgenFocus ? 0.42f : 0.85f;
        float danger = situation is MusicSituation.LowCombat or MusicSituation.FullCombat or MusicSituation.EliteCombat or MusicSituation.FinalConvergence ? Math.Max(0.4f, intensity) : 0.05f;
        return new StemMix(
            1f,
            context.IsInRoentgenFocus ? 0.2f : 0.45f + intensity * 0.4f,
            percussion,
            melody,
            context.IsInRoentgenFocus ? 1f : 0.7f,
            danger);
    }

    private static StemMix StemMixFrom(IReadOnlyDictionary<MusicStemKind, float> values) => new(
        GetStem(values, MusicStemKind.Foundation, 1f),
        GetStem(values, MusicStemKind.Pulse, 0.45f),
        GetStem(values, MusicStemKind.Percussion, 0.35f),
        GetStem(values, MusicStemKind.Melody, 0.85f),
        GetStem(values, MusicStemKind.Texture, 0.7f),
        GetStem(values, MusicStemKind.Danger, 0.05f));

    private static float GetStem(IReadOnlyDictionary<MusicStemKind, float> values, MusicStemKind stem, float fallback) =>
        values.TryGetValue(stem, out float value) && float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : fallback;

    private void DrainRenderCommands()
    {
        while (_commands.TryDequeue(out RenderCommand? command) && command is not null)
        {
            switch (command)
            {
                case SwitchCueCommand switchCue:
                    if (_active.Cue is null)
                    {
                        _active.Load(switchCue.Cue, switchCue.RestoreTick);
                        _active.Gain = 1f;
                        _state = MusicRuntimeState.Running;
                    }
                    else
                    {
                        _pendingCue = switchCue.Cue;
                        _pendingTransition = switchCue.Transition;
                        _pendingRestoreTick = Math.Max(0, switchCue.RestoreTick);
                        _transitionBoundarySample = ResolveBoundary(_active, switchCue.Transition?.Quantization ?? MusicTransitionQuantization.NextBar);
                        if (switchCue.RestoreTick > 0) _transitionBoundarySample = _active.SamplePosition;
                    }
                    break;
                case StingerCommand stinger:
                    _stinger.Load(stinger.Cue);
                    _stinger.Gain = 1f;
                    break;
                case StemCommand stems:
                    ApplyStems(_deckA, stems);
                    ApplyStems(_deckB, stems);
                    ApplyStems(_stinger, stems);
                    break;
            }
        }
    }

    private void StartPendingTransitionIfReady()
    {
        if (_pendingCue is null || _crossfading || _active.SamplePosition < _transitionBoundarySample) return;
        if (_active.SamplePosition > _transitionBoundarySample) Interlocked.Increment(ref _lateTransitions);
        _inactive.Load(_pendingCue, _pendingRestoreTick);
        _inactive.Gain = 1f;
        _crossfadeSamples = Math.Max(1L, (long)Math.Round((_pendingTransition?.CrossfadeSeconds ?? _settings.DefaultCrossfadeSeconds) * Format.SampleRate));
        _crossfadePosition = 0;
        _crossfading = true;
        _pendingCue = null;
        _pendingRestoreTick = 0;
    }

    private void CompleteTransition()
    {
        _active.Stop();
        (_active, _inactive) = (_inactive, _active);
        _inactive.Stop();
        _crossfading = false;
        _crossfadePosition = 0;
        _pendingTransition = null;
        Interlocked.Increment(ref _completedTransitions);
    }

    private long ResolveBoundary(SoundFontDeck deck, MusicTransitionQuantization quantization)
    {
        AdaptiveCueDocument? cue = deck.Cue;
        if (cue is null) return deck.SamplePosition;
        long current = deck.SamplePosition;
        double samplesPerBeat = Format.SampleRate * 60.0 / Math.Max(20.0, cue.Plan.Harmony.Tempo);
        long beat = Math.Max(1L, (long)Math.Round(samplesPerBeat));
        long bar = beat * Math.Max(1, cue.Plan.Harmony.MeterNumerator);
        return quantization switch
        {
            MusicTransitionQuantization.Immediate => current,
            MusicTransitionQuantization.NextBeat => RoundUp(current, beat),
            MusicTransitionQuantization.NextBar => RoundUp(current, bar),
            MusicTransitionQuantization.NextTwoBars => RoundUp(current, bar * 2),
            MusicTransitionQuantization.NextPhrase => RoundUp(current, bar * 4),
            MusicTransitionQuantization.EndOfLoop => cue.PrimaryLoopEndSample,
            MusicTransitionQuantization.EndOfSection => RoundUp(current, bar * 4),
            MusicTransitionQuantization.ExplicitMarker => cue.PrimaryLoopEndSample,
            _ => current
        };
    }

    private static long RoundUp(long value, long quantum) => ((value + quantum - 1) / quantum) * quantum;

    private static void ApplyStems(SoundFontDeck deck, StemCommand command)
    {
        deck.SetStemGain(MusicStemKind.Foundation, command.Foundation);
        deck.SetStemGain(MusicStemKind.Pulse, command.Pulse);
        deck.SetStemGain(MusicStemKind.Percussion, command.Percussion);
        deck.SetStemGain(MusicStemKind.Melody, command.Melody);
        deck.SetStemGain(MusicStemKind.Texture, command.Texture);
        deck.SetStemGain(MusicStemKind.Danger, command.Danger);
    }

    private void AddCache(string key, AdaptiveCueDocument cue)
    {
        if (_cache.Count >= _settings.PreparedCueCacheSize)
        {
            string first = _cache.Keys.OrderBy(static value => value, StringComparer.Ordinal).First();
            _cache.Remove(first);
        }
        _cache[key] = cue;
    }

    private void EnsureMotifs(MusicContext context)
    {
        if (!context.RegionId.IsEmpty) _ = _motifs.GetOrCreate(context.RegionId, "region");
        if (!context.ControllingFactionId.IsEmpty) _ = _motifs.GetOrCreate(context.ControllingFactionId, "faction");
        if (!context.ActiveCompanionId.IsEmpty) _ = _motifs.GetOrCreate(context.ActiveCompanionId, "companion");
    }

    private ulong DeriveCueSeed(MusicSituation situation, MusicContext context)
    {
        string text = $"{_campaignSeed}:{situation}:{context.RegionId}:{context.LocationId}:{context.LocationKind}:{context.Weather}:{context.ControllingFactionId}:{context.ActiveCompanionId}:{context.CampaignMoment}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return BitConverter.ToUInt64(hash, 0);
    }

    private static string CacheKey(MusicSituation situation, ulong seed) => situation + ":" + seed.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    private static MusicContext Normalize(MusicContext value) => value with { CombatIntensity = Math.Clamp(float.IsFinite(value.CombatIntensity) ? value.CombatIntensity : 0f, 0f, 1f), PlayerHealthRatio = Math.Clamp(float.IsFinite(value.PlayerHealthRatio) ? value.PlayerHealthRatio : 1f, 0f, 1f), Contamination = Math.Clamp(float.IsFinite(value.Contamination) ? value.Contamination : 0f, 0f, 1f), HourOfDay = ((value.HourOfDay % 24) + 24) % 24 };
    private static int CalculateBar(AdaptiveCueDocument? cue, long tick) => cue is null ? 0 : (int)(tick / Math.Max(1L, cue.Composition.Song.TicksPerQuarterNote * cue.Plan.Harmony.MeterNumerator)) + 1;
    private static int CalculateBeat(AdaptiveCueDocument? cue, long tick) => cue is null ? 0 : (int)((tick / Math.Max(1, cue.Composition.Song.TicksPerQuarterNote)) % Math.Max(1, cue.Plan.Harmony.MeterNumerator)) + 1;
    private static string StyleLibraryFingerprint() => "playroma-style-library-integrated-v19";
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
