namespace Roentgen.Simulation;

public sealed class SimulationClock
{
    private readonly double _secondsPerTick;
    private double _accumulator;
    private double _timeScale = 1.0;

    public SimulationClock(int ticksPerSecond)
    {
        if (ticksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        }

        TicksPerSecond = ticksPerSecond;
        _secondsPerTick = 1.0 / ticksPerSecond;
    }

    public int TicksPerSecond { get; }
    public ulong Tick { get; private set; }
    public double TimeScale
    {
        get => _timeScale;
        set
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Time scale must be finite and non-negative.");
            }

            _timeScale = value;
        }
    }

    public bool IsPaused => TimeScale == 0.0;

    public int Advance(TimeSpan elapsed, int maximumCatchUpTicks = 5)
    {
        if (maximumCatchUpTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCatchUpTicks));
        }

        if (TimeScale <= 0.0)
        {
            return 0;
        }

        _accumulator += elapsed.TotalSeconds * TimeScale;
        int advanced = 0;
        while (_accumulator >= _secondsPerTick && advanced < maximumCatchUpTicks)
        {
            _accumulator -= _secondsPerTick;
            Tick = checked(Tick + 1UL);
            advanced++;
        }

        return advanced;
    }

    public ulong Step()
    {
        Tick = checked(Tick + 1UL);
        return Tick;
    }

    public void Pause() => TimeScale = 0.0;

    public void Resume() => TimeScale = 1.0;

    public void Reset(ulong tick = 0) => Restore(tick, 1.0);

    public void Restore(ulong completedTick, double timeScale)
    {
        if (!double.IsFinite(timeScale) || timeScale < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeScale));
        }

        Tick = completedTick;
        TimeScale = timeScale;
        _accumulator = 0.0;
    }
}
