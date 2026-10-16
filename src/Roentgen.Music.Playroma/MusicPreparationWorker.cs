namespace Roentgen.Music.Playroma;

internal sealed record MusicPreparationRequest(long Sequence, MusicSituation Situation, MusicContext Context, ulong Seed, bool Stinger, long RestoreTick);
internal sealed record MusicPreparationResult(long Sequence, AdaptiveCueDocument Cue, bool Stinger, long RestoreTick, Exception? Error);

internal sealed class MusicPreparationWorker : IDisposable
{
    private readonly RoentgenCueGenerator _generator;
    private readonly FixedSpscQueue<MusicPreparationRequest> _requests;
    private readonly FixedSpscQueue<MusicPreparationResult> _results;
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;
    private volatile bool _running = true;

    public MusicPreparationWorker(RoentgenCueGenerator generator, int capacity)
    {
        _generator = generator;
        _requests = new FixedSpscQueue<MusicPreparationRequest>(capacity);
        _results = new FixedSpscQueue<MusicPreparationResult>(capacity);
        _thread = new Thread(Run) { IsBackground = true, Name = "Roentgen Music Preparation" };
        _thread.Start();
    }

    public int PendingRequests => _requests.Count;
    public bool TryRequest(MusicPreparationRequest request)
    {
        bool accepted = _requests.TryEnqueue(request);
        if (accepted) _wake.Set();
        return accepted;
    }
    public bool TryTake(out MusicPreparationResult? result) => _results.TryDequeue(out result);

    public void Dispose()
    {
        _running = false;
        _wake.Set();
        _thread.Join(TimeSpan.FromSeconds(3));
        _wake.Dispose();
    }

    private void Run()
    {
        while (_running)
        {
            if (!_requests.TryDequeue(out MusicPreparationRequest? request) || request is null)
            {
                _wake.WaitOne(50);
                continue;
            }
            try
            {
                AdaptiveCueDocument cue = _generator.PrepareDocument(request.Situation, request.Context, request.Seed);
                while (_running && !_results.TryEnqueue(new MusicPreparationResult(request.Sequence, cue, request.Stinger, request.RestoreTick, null))) Thread.Yield();
            }
            catch (Exception exception)
            {
                while (_running && !_results.TryEnqueue(new MusicPreparationResult(request.Sequence, null!, request.Stinger, request.RestoreTick, exception))) Thread.Yield();
            }
        }
    }
}
