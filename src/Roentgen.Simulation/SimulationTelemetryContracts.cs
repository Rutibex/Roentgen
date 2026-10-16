using Roentgen.Core;

namespace Roentgen.Simulation;

public sealed record SimulationTickTelemetry(
    ulong Tick,
    TimeSpan Duration,
    int CommandsProcessed,
    int EventsDispatched,
    int EntityCount,
    long ManagedBytes,
    StateChecksum? StateChecksum);

public interface ISimulationTelemetrySink
{
    void OnTickCompleted(SimulationTickTelemetry telemetry);
    void OnHostFault(ulong completedTick, Exception exception);
}

public sealed class NullSimulationTelemetrySink : ISimulationTelemetrySink
{
    public static NullSimulationTelemetrySink Instance { get; } = new();
    private NullSimulationTelemetrySink() { }
    public void OnTickCompleted(SimulationTickTelemetry telemetry) { }
    public void OnHostFault(ulong completedTick, Exception exception) { }
}
