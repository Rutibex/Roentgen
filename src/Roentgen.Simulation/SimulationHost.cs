using System.Diagnostics;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Simulation;

public sealed record SimulationHostOptions(
    int TicksPerSecond = 60,
    int ChecksumInterval = 0,
    int MaximumTicksPerRun = 10_000_000)
{
    public void Validate()
    {
        if (TicksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(TicksPerSecond));
        if (ChecksumInterval < 0) throw new ArgumentOutOfRangeException(nameof(ChecksumInterval));
        if (MaximumTicksPerRun <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumTicksPerRun));
    }
}

public sealed record SimulationRunResult(
    ulong StartTick,
    ulong EndTick,
    ulong TicksExecuted,
    long CommandsProcessed,
    long EventsDispatched,
    StateChecksum FinalChecksum,
    TimeSpan Elapsed);

public sealed class SimulationHost : IDisposable
{
    private readonly ISimulationTelemetrySink _telemetry;
    private bool _disposed;
    private bool _initialized;
    private Exception? _faultException;

    public SimulationHost(
        CampaignSeed campaignSeed,
        SimulationHostOptions options,
        EntityRegistry entities,
        SimulationStateStore stateStore,
        DeterministicCommandQueue commands,
        CommandSequenceAllocator commandSequences,
        CommandRouter commandRouter,
        DeterministicEventBus events,
        SimulationChecksumService checksumService,
        SimulationServiceRegistry services,
        ISimulationTelemetrySink? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        CampaignSeed = campaignSeed;
        Options = options;
        Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        StateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        CommandSequences = commandSequences ?? throw new ArgumentNullException(nameof(commandSequences));
        CommandRouter = commandRouter ?? throw new ArgumentNullException(nameof(commandRouter));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        ChecksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        _telemetry = telemetry ?? NullSimulationTelemetrySink.Instance;
        Clock = new SimulationClock(options.TicksPerSecond);
        Pipeline = new SimulationPipeline(campaignSeed, commands, commandRouter, events, checksumService, services);
    }

    public CampaignSeed CampaignSeed { get; }
    public SimulationHostOptions Options { get; }
    public EntityRegistry Entities { get; }
    public SimulationStateStore StateStore { get; }
    public DeterministicCommandQueue Commands { get; }
    public CommandSequenceAllocator CommandSequences { get; }
    public CommandRouter CommandRouter { get; }
    public DeterministicEventBus Events { get; }
    public SimulationChecksumService ChecksumService { get; }
    public SimulationServiceRegistry Services { get; }
    public SimulationClock Clock { get; }
    public SimulationPipeline Pipeline { get; }
    public bool IsInitialized => _initialized;
    public bool IsFaulted => _faultException is not null;
    public Exception? FaultException => _faultException;

    public void Initialize()
    {
        ThrowIfUnavailable();
        if (_initialized)
        {
            return;
        }

        Services.Freeze();
        Pipeline.Initialize();
        _initialized = true;
    }

    public void RegisterSystem(ISimulationSystem system)
    {
        ThrowIfUnavailable();
        if (_initialized)
        {
            throw new InvalidOperationException("Systems must be registered before the simulation host is initialized.");
        }

        Pipeline.RegisterSystem(system);
    }

    public void Enqueue(CommandEnvelope command)
    {
        ThrowIfUnavailable();
        ArgumentNullException.ThrowIfNull(command);
        ValidateTargetTick(command.TargetTick);
        Commands.Enqueue(command);
    }

    public CommandEnvelope Enqueue(
        StableId issuerId,
        ulong targetTick,
        CommandOriginPriority priority,
        ISimulationCommand command)
    {
        ThrowIfUnavailable();
        ValidateTargetTick(targetTick);
        ulong sequence = CommandSequences.Allocate();
        try
        {
            CommandEnvelope envelope = CommandEnvelopeFactory.Create(issuerId, targetTick, priority, sequence, command);
            Commands.Enqueue(envelope);
            return envelope;
        }
        catch
        {
            CommandSequences.Restore(sequence);
            throw;
        }
    }

    public SimulationTickResult Step(bool forceChecksum = false)
    {
        ThrowIfUnavailable();
        long started = Stopwatch.GetTimestamp();
        try
        {
            Initialize();
            ulong tick = Clock.Step();
            bool computeChecksum = forceChecksum ||
                (Options.ChecksumInterval > 0 && tick % (ulong)Options.ChecksumInterval == 0UL);
            SimulationTickResult result = Pipeline.RunTick(tick, computeChecksum);
            TimeSpan duration = Stopwatch.GetElapsedTime(started);
            _telemetry.OnTickCompleted(new SimulationTickTelemetry(
                tick,
                duration,
                result.CommandsProcessed,
                result.EventsDispatched,
                Entities.Count,
                GC.GetTotalMemory(forceFullCollection: false),
                result.StateChecksum));
            return result;
        }
        catch (Exception exception)
        {
            _faultException = exception;
            _telemetry.OnHostFault(Clock.Tick, exception);
            throw;
        }
    }

    public SimulationRunResult RunTicks(ulong tickCount, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();
        if (tickCount > (ulong)Options.MaximumTicksPerRun)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), $"A single run is limited to {Options.MaximumTicksPerRun} ticks.");
        }

        ulong startTick = Clock.Tick;
        long commands = 0;
        long events = 0;
        long started = Stopwatch.GetTimestamp();
        for (ulong index = 0; index < tickCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SimulationTickResult result = Step(forceChecksum: false);
            commands = checked(commands + result.CommandsProcessed);
            events = checked(events + result.EventsDispatched);
        }

        StateChecksum finalChecksum = ChecksumService.Compute(Clock.Tick);
        return new SimulationRunResult(
            startTick,
            Clock.Tick,
            Clock.Tick - startTick,
            commands,
            events,
            finalChecksum,
            Stopwatch.GetElapsedTime(started));
    }

    public SimulationSnapshot CaptureSnapshot()
    {
        ThrowIfUnavailable();
        if (Events.PendingCount != 0)
        {
            throw new InvalidOperationException("Snapshots may only be captured at a tick boundary with no pending events.");
        }

        return new SimulationSnapshot(
            SimulationSnapshot.CurrentFormatVersion,
            CampaignSeed,
            Clock.TicksPerSecond,
            Clock.Tick,
            Clock.TimeScale,
            CommandSequences.NextSequence,
            Events.NextSequence,
            Entities.CaptureSnapshot(),
            StateStore.CaptureSnapshot(),
            Commands.CaptureSnapshot(),
            ChecksumService.Compute(Clock.Tick));
    }

    public void Restore(SimulationSnapshot snapshot)
    {
        ThrowIfUnavailable();
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != SimulationSnapshot.CurrentFormatVersion)
        {
            throw new NotSupportedException($"Simulation snapshot format {snapshot.FormatVersion} is not supported.");
        }

        if (snapshot.CampaignSeed != CampaignSeed || snapshot.TicksPerSecond != Clock.TicksPerSecond)
        {
            throw new InvalidDataException("Snapshot campaign seed or tick rate does not match this simulation host.");
        }

        if (Events.PendingCount != 0)
        {
            throw new InvalidOperationException("Cannot restore while events are pending.");
        }

        Entities.Restore(snapshot.EntityRegistry);
        StateStore.Restore(snapshot.StateStore);
        Commands.Restore(snapshot.PendingCommands);
        CommandSequences.Restore(snapshot.NextCommandSequence);
        Events.RestoreSequence(snapshot.NextEventSequence);
        Clock.Restore(snapshot.CompletedTick, snapshot.TimeScale);
        Pipeline.NotifyRestored();
        StateChecksum actual = ChecksumService.Compute(Clock.Tick);
        if (actual != snapshot.StateChecksum)
        {
            var exception = new InvalidDataException($"Restored simulation checksum {actual} does not match snapshot {snapshot.StateChecksum}.");
            _faultException = exception;
            _telemetry.OnHostFault(Clock.Tick, exception);
            throw exception;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_initialized)
        {
            Pipeline.Shutdown();
        }

        _disposed = true;
    }

    private void ValidateTargetTick(ulong targetTick)
    {
        if (targetTick <= Clock.Tick)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTick), $"Commands must target a future tick. Completed tick is {Clock.Tick}.");
        }
    }

    private void ThrowIfUnavailable()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SimulationHost));
        }

        if (_faultException is not null)
        {
            throw new InvalidOperationException("The simulation host is faulted and cannot continue. Create a new host and restore a known-good snapshot.", _faultException);
        }
    }
}
