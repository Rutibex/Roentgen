using Playroma.Sound.Core.Audio;
using Playroma.Sound.Streaming.Runtime;
using Roentgen.Audio;

namespace Roentgen.Audio.Playroma;

public sealed class RoentgenMasterAudioSource : IStreamingAudioSource, IAudioRuntimePcmSource, IDisposable
{
    private readonly StreamingSceneHost _global;
    private readonly StreamingSceneHost _region;
    private readonly StreamingSceneHost _resident;
    private readonly StreamingSceneHost _focus;
    private IStreamingAudioSource? _music;
    private readonly float[] _scratch;
    private readonly float[] _domainGains = [1f, 0.75f, 0.8f, 1f, 0f];
    private long _sampleFrame;
    private bool _running;
    private bool _disposed;
    private int _nonFinite;
    private int _clipped;
    private readonly float _compressorThreshold;
    private readonly float _compressorRatio;
    private readonly float _limiterDrive;
    private readonly bool _monoOutput;
    private string _lastFault = string.Empty;

    public RoentgenMasterAudioSource(StreamingSceneHost global, StreamingSceneHost region, StreamingSceneHost resident, StreamingSceneHost focus, AudioSettings settings)
    {
        _global = global;
        _region = region;
        _resident = resident;
        _focus = focus;
        Format = global.Format;
        if (region.Format != Format || resident.Format != Format || focus.Format != Format) throw new ArgumentException("All audio domains must use one format.");
        _scratch = new float[Format.SamplesPerBlock];
        (_compressorThreshold, _compressorRatio, _limiterDrive) = settings.DynamicRange switch
        {
            AudioDynamicRange.Wide => (0.92f, 1.5f, 0.78f),
            AudioDynamicRange.Night => (0.48f, 5f, 1.18f),
            AudioDynamicRange.ReducedSuddenSounds => (0.38f, 7f, 1.26f),
            _ => (0.68f, 3f, 0.98f)
        };
        _monoOutput = settings.MonoOutput;
    }

    public AudioFormat Format { get; }
    public int SampleRate => Format.SampleRate;
    public int Channels => Format.Channels;
    public int RenderBlockFrames => Format.BlockFrames;
    public long CurrentSampleFrame => Volatile.Read(ref _sampleFrame);
    public bool IsRunning => Volatile.Read(ref _running);
    public int NonFiniteSamples => Volatile.Read(ref _nonFinite);
    public int ClippedSamples => Volatile.Read(ref _clipped);
    public string LastFault => _lastFault;

    public void SetMusicSource(IStreamingAudioSource? music)
    {
        ThrowIfDisposed();
        if (music is not null && music.Format != Format) throw new ArgumentException("Music source must use the master audio format.", nameof(music));
        _music = music;
    }

    public void SetDomainGain(AudioSceneDomain domain, float gain)
    {
        if (!float.IsFinite(gain)) throw new ArgumentOutOfRangeException(nameof(gain));
        _domainGains[(int)domain] = Math.Clamp(gain, 0f, 2f);
    }

    public void Start()
    {
        ThrowIfDisposed();
        // Keep the gameplay sound device alive even if an optional domain (especially
        // adaptive music) faults during startup.  Global SFX is the one required domain.
        _global.Start();
        TryStart(_region, "region ambience");
        TryStart(_resident, "resident ambience");
        TryStart(_focus, "focus audio");
        if (_music is not null && !TryStart(_music, "adaptive music")) _music = null;
        Volatile.Write(ref _running, true);
    }

    public void Stop()
    {
        if (_disposed) return;
        _global.Stop(); _region.Stop(); _resident.Stop(); _focus.Stop(); _music?.Stop();
        Volatile.Write(ref _running, false);
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _global.Reset(); _region.Reset(); _resident.Reset(); _focus.Reset(); _music?.Reset();
        Volatile.Write(ref _sampleFrame, 0L);
    }

    public int Render(Span<float> destination, int requestedFrames)
    {
        ThrowIfDisposed();
        if (requestedFrames < 0 || requestedFrames > Format.BlockFrames) throw new ArgumentOutOfRangeException(nameof(requestedFrames));
        int samples = requestedFrames * Format.Channels;
        if (destination.Length < samples) throw new ArgumentException("Destination is too small.", nameof(destination));
        destination = destination[..samples];
        destination.Clear();
        if (!IsRunning) return requestedFrames;
        TryMix(_global, destination, requestedFrames, _domainGains[(int)AudioSceneDomain.Global], "global SFX");
        TryMix(_region, destination, requestedFrames, _domainGains[(int)AudioSceneDomain.Region], "region ambience");
        TryMix(_resident, destination, requestedFrames, _domainGains[(int)AudioSceneDomain.Resident], "resident ambience");
        TryMix(_focus, destination, requestedFrames, _domainGains[(int)AudioSceneDomain.Focus], "focus audio");
        if (_music is not null && !TryMix(_music, destination, requestedFrames, _domainGains[(int)AudioSceneDomain.Music], "adaptive music")) _music = null;
        if (_monoOutput && Format.Channels == 2) DownmixMono(destination);
        Limit(destination);
        Interlocked.Add(ref _sampleFrame, requestedFrames);
        return requestedFrames;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _global.Dispose(); _region.Dispose(); _resident.Dispose(); _focus.Dispose();
        _music = null;
    }


    private bool TryStart(IStreamingAudioSource source, string label)
    {
        try
        {
            source.Start();
            return true;
        }
        catch (Exception exception)
        {
            _lastFault = $"{label} startup failed: {exception.Message}";
            return false;
        }
    }

    private bool TryMix(IStreamingAudioSource source, Span<float> destination, int frames, float gain, string label)
    {
        if (gain <= 0f) return true;
        try
        {
            Mix(source, destination, frames, gain);
            return true;
        }
        catch (Exception exception)
        {
            // A single failed ambience/music render must never kill the worker thread and
            // silence every weapon/UI/footstep voice for the rest of the session.
            _lastFault = $"{label} render failed: {exception.Message}";
            return false;
        }
    }

    private void Mix(IStreamingAudioSource source, Span<float> destination, int frames, float gain)
    {
        if (gain <= 0f) return;
        Span<float> scratch = _scratch.AsSpan(0, frames * Format.Channels);
        scratch.Clear();
        source.Render(scratch, frames);
        for (int i = 0; i < scratch.Length; i++) destination[i] += scratch[i] * gain;
    }

    private void Limit(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float value = samples[i];
            if (!float.IsFinite(value)) { value = 0f; Interlocked.Increment(ref _nonFinite); }
            float magnitude = Math.Abs(value);
            if (magnitude > _compressorThreshold)
            {
                float compressed = _compressorThreshold + ((magnitude - _compressorThreshold) / _compressorRatio);
                value = MathF.CopySign(compressed, value);
            }
            if (Math.Abs(value) > 1f) Interlocked.Increment(ref _clipped);
            samples[i] = MathF.Tanh(value * _limiterDrive);
        }
    }

    private static void DownmixMono(Span<float> samples)
    {
        for (int i = 0; i + 1 < samples.Length; i += 2)
        {
            float mono = (samples[i] + samples[i + 1]) * 0.5f;
            samples[i] = mono;
            samples[i + 1] = mono;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
