using System.Numerics;
using Playroma.Sound.Graph.Definitions;
using Playroma.Sound.Streaming.Compilation;
using Playroma.Sound.Streaming.Runtime;
using Playroma.Sound.Streaming.Spatial;
using Roentgen.Audio;
using Roentgen.Contracts;

namespace Roentgen.Audio.Playroma;

public sealed class PlayromaAudioRuntime : IAudioRuntime, IAudioRuntimePcmSource
{
    private readonly AudioSettings _settings;
    private readonly IReadOnlyDictionary<string, SoundGraphDefinition> _assets;
    private readonly StreamingSceneHost _global;
    private readonly StreamingSceneHost _region;
    private readonly StreamingSceneHost _resident;
    private readonly StreamingSceneHost _focus;
    private readonly RoentgenMasterAudioSource _master;
    private readonly AudioEmitterRegistry _emitters = new();
    private readonly StableId[] _recentEvents = new StableId[4096];
    private readonly Dictionary<AudioVoiceHandle, VoiceBinding> _voiceMap = [];
    private readonly Dictionary<StableId, AudioVoiceHandle> _persistentVoices = [];
    private int _recentEventWrite;
    private int _nextVoiceHandle;
    private AudioRuntimeState _state = AudioRuntimeState.Stopped;
    private AudioRegion _currentRegion;
    private AudioMixSnapshotKind _snapshot = AudioMixSnapshotKind.Gameplay;
    private bool _snapshotInitialized;
    private long _commandRejections;
    private long _startedVoices;
    private long _rejectedVoices;
    private long _stolenVoices;
    private long _completedVoices;
    private long _faultedVoices;
    private string _fault = string.Empty;

    public PlayromaAudioRuntime(ulong campaignSeed, AudioSettings settings)
    {
        _settings = settings.Validate();
        _assets = IntegratedSoundCatalog.Build(campaignSeed, _settings.Quality);
        _global = new StreamingSceneHost(PlayromaSceneFactory.CreateGlobal(_assets, _settings, campaignSeed), 4096, 32);
        _region = new StreamingSceneHost(PlayromaSceneFactory.CreateRegion(AudioRegion.GlassBasin, campaignSeed ^ 0xB451UL, _settings), 256, 16);
        _resident = new StreamingSceneHost(PlayromaSceneFactory.CreateResident(_assets, _settings, campaignSeed), 1024, 16);
        _focus = new StreamingSceneHost(PlayromaSceneFactory.CreateFocus(_assets, _settings, campaignSeed), 512, 16);
        _master = new RoentgenMasterAudioSource(_global, _region, _resident, _focus, _settings);
        ApplyMixSnapshot(AudioMixSnapshotKind.Gameplay, 0f);
    }


    public void AttachMusicSource(IStreamingAudioSource musicSource)
    {
        ArgumentNullException.ThrowIfNull(musicSource);
        if (_state is AudioRuntimeState.Running or AudioRuntimeState.Paused) throw new InvalidOperationException("Attach the music source before starting the audio runtime.");
        _master.SetMusicSource(musicSource);
    }

    public AudioRuntimeState State => _state;
    public int SampleRate => _master.SampleRate;
    public int Channels => _master.Channels;
    public int RenderBlockFrames => _master.RenderBlockFrames;
    public AudioTelemetrySnapshot Telemetry
    {
        get
        {
            var global = _global.Telemetry;
            var region = _region.Telemetry;
            var resident = _resident.Telemetry;
            var focus = _focus.Telemetry;
            return new AudioTelemetrySnapshot(
                _state, SampleRate, Channels, RenderBlockFrames, 0, _settings.PcmRingBlocks, _master.CurrentSampleFrame,
                0, 0, 0, _commandRejections,
                _startedVoices,
                _rejectedVoices,
                _stolenVoices,
                _completedVoices,
                _faultedVoices,
                _emitters.Count, _emitters.VirtualizedCount, 0, _master.NonFiniteSamples, _master.ClippedSamples,
                Math.Max(Math.Max(global.LastRenderMilliseconds, region.LastRenderMilliseconds), Math.Max(resident.LastRenderMilliseconds, focus.LastRenderMilliseconds)),
                Math.Max(Math.Max(global.PeakRenderMilliseconds, region.PeakRenderMilliseconds), Math.Max(resident.PeakRenderMilliseconds, focus.PeakRenderMilliseconds)),
                _snapshot.ToString(), _currentRegion.ToString(), string.IsNullOrWhiteSpace(_master.LastFault) ? _fault : _master.LastFault);
        }
    }

    public void Start()
    {
        if (_state == AudioRuntimeState.Disposed) throw new ObjectDisposedException(nameof(PlayromaAudioRuntime));
        try { _master.Start(); _state = AudioRuntimeState.Running; }
        catch (Exception ex) { _fault = ex.Message; _state = AudioRuntimeState.Faulted; }
    }

    public void Stop() { if (_state == AudioRuntimeState.Disposed) return; _master.Stop(); _state = AudioRuntimeState.Stopped; }
    public void Pause() { if (_state == AudioRuntimeState.Running) { ApplyMixSnapshot(AudioMixSnapshotKind.PauseMenu); _state = AudioRuntimeState.Paused; } }
    public void Resume() { if (_state == AudioRuntimeState.Paused) { ApplyMixSnapshot(AudioMixSnapshotKind.Gameplay); _state = AudioRuntimeState.Running; } }

    public void UpdateListener(AudioListenerState listener)
    {
        SoundListenerState converted = Convert(listener);
        _global.TrySetListener(converted); _region.TrySetListener(converted); _resident.TrySetListener(converted); _focus.TrySetListener(converted);
        _global.TrySetListenerZone(listener.ZoneId); _resident.TrySetListenerZone(listener.ZoneId);
    }

    public bool Submit(SemanticAudioEvent audioEvent)
    {
        if (_state is AudioRuntimeState.Disposed or AudioRuntimeState.Faulted or AudioRuntimeState.Stopped) return false;
        if (IsDuplicate(audioEvent.EventId)) return true;
        AudioIdentity identity = AudioIdentityFactory.Resolve(audioEvent);
        StreamingSceneHost host = Host(identity.Domain);
        if (!_assets.ContainsKey(identity.AssetId)) { Interlocked.Increment(ref _commandRejections); return false; }
        SoundEmitterState emitter = Convert(audioEvent.Emitter);
        float gain = ResolveGain(audioEvent.Kind);
        var options = new VoiceStartOptions(identity.BusId, gain, 0f, Priority(audioEvent.Kind), false, Protected(audioEvent.Kind), emitter, audioEvent.Emitter.ZoneId);
        if (!host.TryPlaySound(identity.AssetId, options, out HostedVoiceHandle inner)) { Interlocked.Increment(ref _commandRejections); return false; }
        var outer = new AudioVoiceHandle(Interlocked.Increment(ref _nextVoiceHandle));
        _voiceMap[outer] = new VoiceBinding(identity.Domain, inner);
        if (!audioEvent.SourceId.IsEmpty && _emitters.TryGet(audioEvent.SourceId, out AudioEmitterSnapshot snapshot) && snapshot.Descriptor.Persistent)
        {
            _persistentVoices[audioEvent.SourceId] = outer;
            _emitters.SetVoice(audioEvent.SourceId, outer);
        }
        return true;
    }

    public bool RegisterEmitter(AudioEmitterDescriptor descriptor, AudioEmitterState initialState) => _emitters.Register(descriptor, initialState);

    public bool UpdateEmitter(StableId emitterId, AudioEmitterState state, ulong tick)
    {
        if (!_emitters.Update(emitterId, state, tick)) return false;
        if (_persistentVoices.TryGetValue(emitterId, out AudioVoiceHandle outer) && _voiceMap.TryGetValue(outer, out VoiceBinding binding))
        {
            SoundEmitterState converted = Convert(state);
            Host(binding.Domain).TrySetVoiceAcousticState(binding.Handle, converted, state.ZoneId);
        }
        return true;
    }

    public bool RemoveEmitter(StableId emitterId)
    {
        if (_persistentVoices.Remove(emitterId, out AudioVoiceHandle outer) && _voiceMap.Remove(outer, out VoiceBinding binding))
        {
            Host(binding.Domain).TryStopVoice(binding.Handle);
        }
        return _emitters.Remove(emitterId);
    }

    public void ApplyMixSnapshot(AudioMixSnapshotKind snapshot, float transitionSeconds = 0.15f)
    {
        if (!float.IsFinite(transitionSeconds) || transitionSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
        if (_snapshotInitialized && _snapshot == snapshot) return;
        _snapshot = snapshot;
        _snapshotInitialized = true;
        IReadOnlyDictionary<string, float> gains = AudioMixSnapshots.GetBusGains(snapshot);
        _global.TrySetParameter("mix.world", gains["world"]);
        _global.TrySetParameter("mix.interface", gains["interface"]);
        _resident.TrySetParameter("mix.world", gains["world"]);
        _resident.TrySetParameter("mix.interface", gains["interface"]);
        _focus.TrySetParameter("mix.focus", gains["focus"]);
        _master.SetDomainGain(AudioSceneDomain.Global, _settings.MasterVolume);
        float ambienceGain = !_settings.ContinueAmbienceInMenus && snapshot is (AudioMixSnapshotKind.PauseMenu or AudioMixSnapshotKind.Dialogue)
            ? 0f
            : gains["ambience"];
        _master.SetDomainGain(AudioSceneDomain.Region, ambienceGain * _settings.AmbienceVolume * _settings.MasterVolume);
        _master.SetDomainGain(AudioSceneDomain.Resident, _settings.MasterVolume);
        _master.SetDomainGain(AudioSceneDomain.Focus, _settings.FocusVolume * _settings.MasterVolume);
        _master.SetDomainGain(AudioSceneDomain.Music, gains["music"] * _settings.MusicVolume * _settings.MasterVolume);
    }

    public void SetRegion(AudioRegion region, ulong regionSeed)
    {
        try
        {
            PreparedSoundScene replacement = PlayromaSceneFactory.CreateRegion(region, regionSeed, _settings);
            if (_region.TryReplaceScene(replacement, SampleRate / 2)) _currentRegion = region;
            else Interlocked.Increment(ref _commandRejections);
        }
        catch (Exception exception)
        {
            // Region ambience is optional.  Keep global SFX/UI audio alive if a generated
            // ambience scene cannot compile or hot-swap on a particular machine.
            _fault = $"Region ambience '{region}' failed: {exception.Message}";
            Interlocked.Increment(ref _commandRejections);
        }
    }

    public void PumpControl()
    {
        _global.DrainRetiredInstances(); _region.DrainRetiredInstances(); _resident.DrainRetiredInstances(); _focus.DrainRetiredInstances();
        DrainLifecycle(_global, AudioSceneDomain.Global);
        DrainLifecycle(_region, AudioSceneDomain.Region);
        DrainLifecycle(_resident, AudioSceneDomain.Resident);
        DrainLifecycle(_focus, AudioSceneDomain.Focus);
    }

    public int Render(Span<float> destination, int requestedFrames) => _master.Render(destination, requestedFrames);

    public void Dispose()
    {
        if (_state == AudioRuntimeState.Disposed) return;
        _state = AudioRuntimeState.Disposed;
        _master.Dispose();
    }

    private void DrainLifecycle(StreamingSceneHost host, AudioSceneDomain domain)
    {
        while (host.TryDequeueVoiceLifecycle(out HostedVoiceLifecycleEvent item))
        {
            switch (item.Kind)
            {
                case VoiceLifecycleKind.Started: _startedVoices++; break;
                case VoiceLifecycleKind.Rejected: _rejectedVoices++; break;
                case VoiceLifecycleKind.Stolen: _stolenVoices++; break;
                case VoiceLifecycleKind.Completed: _completedVoices++; break;
                case VoiceLifecycleKind.Faulted: _faultedVoices++; break;
            }

            if (item.Kind is not (VoiceLifecycleKind.Rejected or VoiceLifecycleKind.Stolen or VoiceLifecycleKind.Completed or VoiceLifecycleKind.Stopped or VoiceLifecycleKind.Faulted)) continue;
            AudioVoiceHandle outer = AudioVoiceHandle.Invalid;
            foreach ((AudioVoiceHandle candidate, VoiceBinding binding) in _voiceMap)
            {
                if (binding.Domain != domain || binding.Handle != item.Handle) continue;
                outer = candidate;
                break;
            }
            if (!outer.IsValid) continue;
            _voiceMap.Remove(outer);
            StableId persistentEmitter = StableId.Empty;
            foreach ((StableId emitterId, AudioVoiceHandle candidate) in _persistentVoices)
            {
                if (candidate != outer) continue;
                persistentEmitter = emitterId;
                break;
            }
            if (!persistentEmitter.IsEmpty)
            {
                _persistentVoices.Remove(persistentEmitter);
                _emitters.SetVoice(persistentEmitter, AudioVoiceHandle.Invalid);
            }
        }
    }

    private bool IsDuplicate(StableId eventId)
    {
        if (eventId.IsEmpty) return false;
        for (int i = 0; i < _recentEvents.Length; i++) if (_recentEvents[i] == eventId) return true;
        _recentEvents[_recentEventWrite] = eventId;
        _recentEventWrite = (_recentEventWrite + 1) % _recentEvents.Length;
        return false;
    }

    private StreamingSceneHost Host(AudioSceneDomain domain) => domain switch
    {
        AudioSceneDomain.Focus => _focus,
        AudioSceneDomain.Resident => _resident,
        AudioSceneDomain.Region => _region,
        _ => _global
    };

    private float ResolveGain(AudioEventKind kind)
    {
        float gain = kind switch
        {
            AudioEventKind.WeaponFire or AudioEventKind.Explosion => _settings.WeaponsVolume,
            AudioEventKind.FootContact or AudioEventKind.Landing or AudioEventKind.MeleeSwing
                or AudioEventKind.DoorOpen or AudioEventKind.DoorClose or AudioEventKind.DoorLock or AudioEventKind.DoorUnlock or AudioEventKind.DoorBreach
                or AudioEventKind.ContainerOpen or AudioEventKind.ContainerClose or AudioEventKind.Pickup or AudioEventKind.Drop or AudioEventKind.Equip or AudioEventKind.Unequip
                or AudioEventKind.Craft or AudioEventKind.Repair or AudioEventKind.MedicalAction => _settings.FoleyVolume,
            AudioEventKind.TerminalUse => MathF.Sqrt(_settings.InterfaceVolume * _settings.FoleyVolume),
            AudioEventKind.Injury or AudioEventKind.Death => _settings.CreaturesVolume,
            AudioEventKind.FocusEnter or AudioEventKind.FocusTarget or AudioEventKind.FocusPartSelected or AudioEventKind.FocusActionQueued or AudioEventKind.FocusInsufficient or AudioEventKind.FocusCommit or AudioEventKind.FocusExit => _settings.FocusVolume,
            AudioEventKind.InterfaceConfirm or AudioEventKind.InterfaceCancel or AudioEventKind.InterfaceError or AudioEventKind.InterfaceWarning
                or AudioEventKind.InterfaceNavigate or AudioEventKind.InterfaceTab or AudioEventKind.InterfaceTransaction or AudioEventKind.InterfaceCraft
                or AudioEventKind.JournalUpdate or AudioEventKind.QuestAccepted or AudioEventKind.ObjectiveAdvanced or AudioEventKind.Revelation or AudioEventKind.IrreversibleChoice => _settings.InterfaceVolume,
            _ => 1f
        };
        if ((_settings.ReduceSuddenSounds || _settings.DynamicRange == AudioDynamicRange.ReducedSuddenSounds)
            && kind is (AudioEventKind.WeaponFire or AudioEventKind.Explosion or AudioEventKind.MeleeContact))
            gain *= 0.62f;
        return gain;
    }

    private static int Priority(AudioEventKind kind) => kind switch
    {
        AudioEventKind.FocusEnter or AudioEventKind.FocusTarget or AudioEventKind.FocusCommit or AudioEventKind.InterfaceError => 100,
        AudioEventKind.WeaponFire => 90,
        AudioEventKind.Explosion => 85,
        AudioEventKind.Revelation or AudioEventKind.IrreversibleChoice => 80,
        AudioEventKind.Impact or AudioEventKind.ArmorImpact or AudioEventKind.BiologicalImpact => 75,
        AudioEventKind.Injury or AudioEventKind.Death => 65,
        AudioEventKind.FootContact => 60,
        AudioEventKind.MachineStart or AudioEventKind.MachineStop => 30,
        _ => 50
    };

    private static bool Protected(AudioEventKind kind) => kind is AudioEventKind.FocusEnter or AudioEventKind.FocusTarget or AudioEventKind.FocusCommit or AudioEventKind.InterfaceError;

    private readonly record struct VoiceBinding(AudioSceneDomain Domain, HostedVoiceHandle Handle);

    private static SoundListenerState Convert(AudioListenerState state) => new(ToVector(state.Position), ToVector(state.Forward), ToVector(state.Up), ToVector(state.Velocity));
    private static SoundEmitterState Convert(AudioEmitterState state) => new(true, ToVector(state.Position), ToVector(state.Velocity), Math.Max(0.01f, state.MinimumDistance), Math.Max(state.MinimumDistance + 0.01f, state.MaximumDistance), Math.Clamp(state.Occlusion, 0f, 1f), Math.Clamp(state.Obstruction, 0f, 1f));
    private static Vector3 ToVector(AudioVector3 value) => new(value.X, value.Y, value.Z);
}
