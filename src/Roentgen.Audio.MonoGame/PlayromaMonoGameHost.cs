using Microsoft.Xna.Framework.Audio;
using Roentgen.Audio;
using Roentgen.Audio.Playroma;

namespace Roentgen.Audio.MonoGame;

public sealed class PlayromaMonoGameHost : IDisposable
{
    private readonly PlayromaAudioRuntime _runtime;
    private readonly AudioSettings _settings;
    private readonly DynamicSoundEffectInstance _device;
    private readonly PcmBlockRingBuffer _ring;
    private readonly PlayromaRenderWorker _worker;
    private int _bufferDemand;
    private long _submittedBuffers;
    private long _underruns;
    private bool _started;
    private bool _disposed;

    public PlayromaMonoGameHost(PlayromaAudioRuntime runtime, AudioSettings settings)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _settings = settings.Validate();
        if (runtime.SampleRate > 48_000) throw new ArgumentOutOfRangeException(nameof(runtime), "MonoGame DynamicSoundEffectInstance supports at most 48 kHz.");
        AudioChannels channels = runtime.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
        _device = new DynamicSoundEffectInstance(runtime.SampleRate, channels);
        _device.BufferNeeded += OnBufferNeeded;
        int bytesPerBlock = settings.SubmittedBufferFrames * runtime.Channels * sizeof(short);
        _ring = new PcmBlockRingBuffer(settings.PcmRingBlocks, bytesPerBlock);
        _worker = new PlayromaRenderWorker(runtime, _ring, settings.SubmittedBufferFrames);
    }

    public bool IsStarted => _started;
    public int PendingBufferCount => _device.PendingBufferCount;
    public int RingCount => _ring.Count;
    public long SubmittedBuffers => Interlocked.Read(ref _submittedBuffers);
    public long Underruns => Interlocked.Read(ref _underruns);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        _runtime.Start();
        if (_runtime.State != AudioRuntimeState.Running)
            throw new InvalidOperationException($"Playroma runtime did not enter the running state: {_runtime.State}. {_runtime.Telemetry.FaultMessage}");
        _worker.Start();
        for (int i = 0; i < 50 && _ring.Count < _settings.TargetQueuedBuffers; i++) Thread.Sleep(2);
        Pump();
        _device.Play();
        _started = true;
    }

    public void Pump()
    {
        if (_disposed) return;
        _runtime.PumpControl();
        int pending = _device.PendingBufferCount;
        int needed = Math.Max(0, _settings.TargetQueuedBuffers - pending);
        int requested = Interlocked.Exchange(ref _bufferDemand, 0);
        needed = Math.Max(needed, requested);
        for (int i = 0; i < needed; i++)
        {
            if (!_ring.TryRead(out byte[] block))
            {
                if (_started) Interlocked.Increment(ref _underruns);
                break;
            }
            _device.SubmitBuffer(block);
            Interlocked.Increment(ref _submittedBuffers);
        }
    }

    public void Pause()
    {
        if (!_started) return;
        _runtime.Pause();
        _device.Pause();
    }

    public void Resume()
    {
        if (!_started) return;
        _runtime.Resume();
        _device.Resume();
    }

    public void Stop()
    {
        if (!_started) return;
        _device.Stop();
        _worker.Stop();
        _runtime.Stop();
        _started = false;
    }

    public AudioTelemetrySnapshot CaptureTelemetry()
    {
        AudioTelemetrySnapshot baseTelemetry = _runtime.Telemetry;
        return baseTelemetry with
        {
            PendingPcmBlocks = _ring.Count,
            PcmRingCapacity = _ring.Capacity,
            RenderedFrames = _worker.RenderedFrames,
            SubmittedBuffers = SubmittedBuffers,
            Underruns = Underruns,
            RingOverflows = _worker.RingOverflows,
            NonFiniteSamples = baseTelemetry.NonFiniteSamples + _worker.NonFiniteSamples,
            ClippedSamples = baseTelemetry.ClippedSamples + _worker.ClippedSamples,
            LastRenderMilliseconds = Math.Max(baseTelemetry.LastRenderMilliseconds, _worker.LastRenderMilliseconds),
            PeakRenderMilliseconds = Math.Max(baseTelemetry.PeakRenderMilliseconds, _worker.PeakRenderMilliseconds)
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _device.BufferNeeded -= OnBufferNeeded;
        _worker.Dispose();
        _device.Dispose();
        _runtime.Dispose();
    }

    private void OnBufferNeeded(object? sender, EventArgs e) => Interlocked.Increment(ref _bufferDemand);
}
