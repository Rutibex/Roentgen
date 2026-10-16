using System.Diagnostics;
using Roentgen.Audio;

namespace Roentgen.Audio.MonoGame;

public sealed class PlayromaRenderWorker : IDisposable
{
    private readonly IAudioRuntimePcmSource _source;
    private readonly PcmBlockRingBuffer _ring;
    private readonly float[] _floatBuffer;
    private readonly Thread _thread;
    private readonly int _submittedFrames;
    private volatile bool _running;
    private volatile bool _disposed;
    private long _renderedFrames;
    private long _ringOverflows;
    private int _nonFinite;
    private int _clipped;
    private double _lastRenderMs;
    private double _peakRenderMs;

    public PlayromaRenderWorker(IAudioRuntimePcmSource source, PcmBlockRingBuffer ring, int submittedFrames)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _ring = ring ?? throw new ArgumentNullException(nameof(ring));
        if (submittedFrames < source.RenderBlockFrames || submittedFrames % source.RenderBlockFrames != 0) throw new ArgumentOutOfRangeException(nameof(submittedFrames));
        _submittedFrames = submittedFrames;
        _floatBuffer = new float[submittedFrames * source.Channels];
        _thread = new Thread(Run) { IsBackground = true, Name = "Roentgen Playroma Render Worker", Priority = ThreadPriority.AboveNormal };
    }

    public long RenderedFrames => Interlocked.Read(ref _renderedFrames);
    public long RingOverflows => Interlocked.Read(ref _ringOverflows);
    public int NonFiniteSamples => Volatile.Read(ref _nonFinite);
    public int ClippedSamples => Volatile.Read(ref _clipped);
    public double LastRenderMilliseconds => Volatile.Read(ref _lastRenderMs);
    public double PeakRenderMilliseconds => Volatile.Read(ref _peakRenderMs);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        _running = true;
        if ((_thread.ThreadState & System.Threading.ThreadState.Unstarted) != 0) _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        if (_thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void Run()
    {
        while (!_disposed)
        {
            if (!_running) { Thread.Sleep(2); continue; }
            if (!_ring.TryGetWriteSpan(out Span<byte> pcm, out int token))
            {
                Interlocked.Increment(ref _ringOverflows);
                Thread.Sleep(1);
                continue;
            }
            long started = Stopwatch.GetTimestamp();
            int offsetFrames = 0;
            while (offsetFrames < _submittedFrames)
            {
                int frames = Math.Min(_source.RenderBlockFrames, _submittedFrames - offsetFrames);
                Span<float> target = _floatBuffer.AsSpan(offsetFrames * _source.Channels, frames * _source.Channels);
                _source.Render(target, frames);
                offsetFrames += frames;
            }
            Pcm16Converter.Convert(_floatBuffer, pcm, out int nonFinite, out int clipped);
            if (nonFinite != 0) Interlocked.Add(ref _nonFinite, nonFinite);
            if (clipped != 0) Interlocked.Add(ref _clipped, clipped);
            _ring.CommitWrite(token);
            Interlocked.Add(ref _renderedFrames, _submittedFrames);
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Volatile.Write(ref _lastRenderMs, elapsed);
            if (elapsed > Volatile.Read(ref _peakRenderMs)) Volatile.Write(ref _peakRenderMs, elapsed);
        }
    }
}
