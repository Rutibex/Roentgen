using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Simulation;

public enum SimulationSystemPhase
{
    Input = 0,
    PreSimulation = 100,
    World = 200,
    Gameplay = 300,
    ArtificialIntelligence = 400,
    PostSimulation = 500,
    Finalization = 600
}

public interface ISimulationSystem
{
    string SystemId { get; }
    int Order { get; }
    void Update(SimulationTickContext context);
}

public interface IPhasedSimulationSystem
{
    SimulationSystemPhase Phase { get; }
}

public interface ISimulationLifecycleSystem
{
    void Initialize(SimulationInitializationContext context);
    void Shutdown();
}

public interface ISimulationRestoreAware
{
    void OnSimulationRestored(SimulationInitializationContext context);
}

public sealed record SimulationSystemDescriptor(
    string SystemId,
    SimulationSystemPhase Phase,
    int Order,
    bool HasLifecycle);

public sealed class SimulationInitializationContext
{
    public SimulationInitializationContext(CampaignSeed campaignSeed, SimulationServiceRegistry services)
    {
        CampaignSeed = campaignSeed;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public CampaignSeed CampaignSeed { get; }
    public SimulationServiceRegistry Services { get; }
}

public sealed class SimulationTickContext
{
    public SimulationTickContext(ulong tick, CampaignSeed campaignSeed, DeterministicEventBus events)
        : this(tick, campaignSeed, events, new SimulationServiceRegistry())
    {
    }

    public SimulationTickContext(
        ulong tick,
        CampaignSeed campaignSeed,
        DeterministicEventBus events,
        SimulationServiceRegistry services)
    {
        Tick = tick;
        CampaignSeed = campaignSeed;
        Events = events ?? throw new ArgumentNullException(nameof(events));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public ulong Tick { get; }
    public CampaignSeed CampaignSeed { get; }
    public DeterministicEventBus Events { get; }
    public SimulationServiceRegistry Services { get; }
}

public sealed record SimulationTickResult(
    ulong Tick,
    int CommandsProcessed,
    int EventsDispatched,
    StateChecksum? StateChecksum);

public sealed class SimulationPipeline
{
    private readonly CampaignSeed _campaignSeed;
    private readonly DeterministicCommandQueue _commands;
    private readonly CommandRouter _commandRouter;
    private readonly DeterministicEventBus _events;
    private readonly SimulationChecksumService? _checksumService;
    private readonly SimulationServiceRegistry _services;
    private readonly List<ISimulationSystem> _systems = [];
    private bool _initialized;

    public SimulationPipeline(
        CampaignSeed campaignSeed,
        DeterministicCommandQueue commands,
        CommandRouter commandRouter,
        DeterministicEventBus events,
        SimulationChecksumService? checksumService = null,
        SimulationServiceRegistry? services = null)
    {
        _campaignSeed = campaignSeed;
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _commandRouter = commandRouter ?? throw new ArgumentNullException(nameof(commandRouter));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _checksumService = checksumService;
        _services = services ?? new SimulationServiceRegistry();
    }

    public int SystemCount => _systems.Count;
    public bool IsInitialized => _initialized;

    public IReadOnlyList<SimulationSystemDescriptor> GetSchedule() => _systems.Select(system => new SimulationSystemDescriptor(
        system.SystemId,
        GetPhase(system),
        system.Order,
        system is ISimulationLifecycleSystem)).ToArray();

    public void RegisterSystem(ISimulationSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(system.SystemId);
        if (_initialized)
        {
            throw new InvalidOperationException("Simulation systems cannot be registered after initialization.");
        }

        if (_systems.Any(existing => string.Equals(existing.SystemId, system.SystemId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Simulation system '{system.SystemId}' is already registered.");
        }

        _systems.Add(system);
        _systems.Sort(SimulationSystemComparer.Instance);
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        var context = new SimulationInitializationContext(_campaignSeed, _services);
        foreach (ISimulationLifecycleSystem system in _systems.OfType<ISimulationLifecycleSystem>())
        {
            system.Initialize(context);
        }

        _initialized = true;
    }

    public void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        foreach (ISimulationLifecycleSystem system in _systems.OfType<ISimulationLifecycleSystem>().Reverse())
        {
            system.Shutdown();
        }

        _initialized = false;
    }

    public void NotifyRestored()
    {
        if (!_initialized)
        {
            return;
        }

        var context = new SimulationInitializationContext(_campaignSeed, _services);
        foreach (ISimulationRestoreAware system in _systems.OfType<ISimulationRestoreAware>())
        {
            system.OnSimulationRestored(context);
        }
    }

    public SimulationTickResult RunTick(ulong tick, bool computeChecksum = false)
    {
        Initialize();
        var context = new SimulationTickContext(tick, _campaignSeed, _events, _services);
        IReadOnlyList<CommandEnvelope> commands = _commands.DrainTick(tick);
        foreach (CommandEnvelope command in commands)
        {
            _commandRouter.Dispatch(command, context);
        }

        int eventsDispatched = _events.Flush(context);
        foreach (ISimulationSystem system in _systems)
        {
            system.Update(context);
            eventsDispatched += _events.Flush(context);
        }

        if (computeChecksum && _checksumService is null)
        {
            throw new InvalidOperationException("A checksum was requested but no checksum service is registered.");
        }

        StateChecksum? checksum = computeChecksum
            ? _checksumService!.Compute(tick)
            : null;

        return new SimulationTickResult(tick, commands.Count, eventsDispatched, checksum);
    }

    private static SimulationSystemPhase GetPhase(ISimulationSystem system) =>
        system is IPhasedSimulationSystem phased ? phased.Phase : SimulationSystemPhase.Gameplay;

    private sealed class SimulationSystemComparer : IComparer<ISimulationSystem>
    {
        public static SimulationSystemComparer Instance { get; } = new();

        public int Compare(ISimulationSystem? left, ISimulationSystem? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            int result = GetPhase(left).CompareTo(GetPhase(right));
            if (result != 0) return result;
            result = left.Order.CompareTo(right.Order);
            return result != 0 ? result : string.CompareOrdinal(left.SystemId, right.SystemId);
        }
    }
}
