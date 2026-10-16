using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Diagnostics;
using Roentgen.Generation;
using Roentgen.Persistence;
using Roentgen.Simulation;

namespace Roentgen.Headless;

public sealed record ReplayExecutionResult(
    bool IsMatch,
    ulong FinalTick,
    StateChecksum FinalChecksum,
    SimulationSnapshot FinalSnapshot,
    IReadOnlyList<DeterminismMismatch> Mismatches);

public static class HeadlessExecution
{
    public static SimulationRunResult Run(
        SimulationHost host,
        ulong tickCount,
        ReplayLogWriter? replayWriter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (tickCount > (ulong)host.Options.MaximumTicksPerRun)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), $"A single run is limited to {host.Options.MaximumTicksPerRun} ticks.");
        }

        ulong startTick = host.Clock.Tick;
        long commands = 0;
        long events = 0;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        for (ulong index = 0; index < tickCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool finalTick = index + 1UL == tickCount;
            SimulationTickResult result = host.Step(forceChecksum: finalTick);
            commands = checked(commands + result.CommandsProcessed);
            events = checked(events + result.EventsDispatched);
            if (result.StateChecksum is StateChecksum checksum)
            {
                replayWriter?.AppendCheckpoint(new ReplayCheckpoint(result.Tick, checksum));
            }
        }

        StateChecksum finalChecksum = host.ChecksumService.Compute(host.Clock.Tick);
        return new SimulationRunResult(
            startTick,
            host.Clock.Tick,
            host.Clock.Tick - startTick,
            commands,
            events,
            finalChecksum,
            System.Diagnostics.Stopwatch.GetElapsedTime(started));
    }

    public static ReplayExecutionResult Replay(
        ReplayLog replay,
        ulong? requestedFinalTick = null,
        HeadlessTelemetryCollector? telemetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replay);
        if (!string.Equals(replay.Header.Product, ConstitutionVersion.Product, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Replay product '{replay.Header.Product}' is not '{ConstitutionVersion.Product}'.");
        }

        if (!string.Equals(replay.Header.RandomAlgorithmId, Pcg32Random.CurrentAlgorithmId, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Replay RNG '{replay.Header.RandomAlgorithmId}' is not supported by this build.");
        }

        using SimulationHost host = HeadlessSimulationFactory.Create(
            replay.Header.CampaignSeed,
            replay.Header.TicksPerSecond,
            checksumInterval: 0,
            telemetry);
        foreach (CommandEnvelope command in replay.Commands)
        {
            if (command.TargetTick == 0UL)
            {
                throw new InvalidDataException($"Headless replay command '{command.CommandId}' targets reserved tick zero.");
            }

            host.Enqueue(command);
        }

        if (replay.Commands.Count > 0)
        {
            ulong nextSequence = checked(replay.Commands.Max(item => item.Sequence) + 1UL);
            host.CommandSequences.Restore(nextSequence);
        }

        ulong maximumReferencedTick = replay.Commands.Select(item => item.TargetTick)
            .Concat(replay.Checkpoints.Select(item => item.Tick))
            .DefaultIfEmpty(0UL)
            .Max();
        ulong finalTick = requestedFinalTick ?? maximumReferencedTick;
        if (finalTick < maximumReferencedTick)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedFinalTick), "Requested final tick precedes replay content.");
        }

        Dictionary<ulong, StateChecksum> expected = replay.Checkpoints.ToDictionary(item => item.Tick, item => item.StateChecksum);
        var actual = new List<(ulong Tick, StateChecksum Checksum)>();
        while (host.Clock.Tick < finalTick)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ulong nextTick = host.Clock.Tick + 1UL;
            bool checkpoint = expected.ContainsKey(nextTick);
            SimulationTickResult result = host.Step(forceChecksum: checkpoint || nextTick == finalTick);
            if (checkpoint)
            {
                actual.Add((nextTick, result.StateChecksum!.Value));
            }
        }

        DeterminismComparisonResult comparison = DeterminismDiagnostics.Compare(
            replay.Checkpoints.Select(item => (item.Tick, item.StateChecksum)),
            actual);
        return new ReplayExecutionResult(
            comparison.IsMatch,
            host.Clock.Tick,
            host.ChecksumService.Compute(host.Clock.Tick),
            host.CaptureSnapshot(),
            comparison.Mismatches);
    }
}
