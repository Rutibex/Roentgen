using System.Text.Json;
using Roentgen.Core;
using Roentgen.Simulation;

namespace Roentgen.Diagnostics;

public sealed record TickDurationSummary(
    double MinimumMilliseconds,
    double AverageMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaximumMilliseconds);

public sealed record HeadlessFaultRecord(ulong CompletedTick, string ExceptionType, string Message);

public sealed record TelemetryCheckpoint(ulong Tick, StateChecksum Checksum);

public sealed record HeadlessTelemetryReport(
    int SchemaVersion,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    ulong FirstTick,
    ulong LastTick,
    ulong TickCount,
    long CommandsProcessed,
    long EventsDispatched,
    int PeakEntityCount,
    long PeakManagedBytes,
    TickDurationSummary TickDurations,
    IReadOnlyList<TelemetryCheckpoint> Checkpoints,
    IReadOnlyList<HeadlessFaultRecord> Faults);

public sealed class HeadlessTelemetryCollector : ISimulationTelemetrySink
{
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private readonly List<double> _durationsMilliseconds = [];
    private readonly List<TelemetryCheckpoint> _checkpoints = [];
    private readonly List<HeadlessFaultRecord> _faults = [];
    private ulong _firstTick;
    private ulong _lastTick;
    private long _commandsProcessed;
    private long _eventsDispatched;
    private int _peakEntityCount;
    private long _peakManagedBytes;

    public void OnTickCompleted(SimulationTickTelemetry telemetry)
    {
        if (_durationsMilliseconds.Count == 0)
        {
            _firstTick = telemetry.Tick;
        }

        _lastTick = telemetry.Tick;
        _durationsMilliseconds.Add(telemetry.Duration.TotalMilliseconds);
        _commandsProcessed = checked(_commandsProcessed + telemetry.CommandsProcessed);
        _eventsDispatched = checked(_eventsDispatched + telemetry.EventsDispatched);
        _peakEntityCount = Math.Max(_peakEntityCount, telemetry.EntityCount);
        _peakManagedBytes = Math.Max(_peakManagedBytes, telemetry.ManagedBytes);
        if (telemetry.StateChecksum is StateChecksum checksum)
        {
            _checkpoints.Add(new TelemetryCheckpoint(telemetry.Tick, checksum));
        }
    }

    public void OnHostFault(ulong completedTick, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _faults.Add(new HeadlessFaultRecord(completedTick, exception.GetType().FullName ?? exception.GetType().Name, exception.Message));
    }

    public HeadlessTelemetryReport CreateReport()
    {
        double[] ordered = _durationsMilliseconds.Order().ToArray();
        var summary = new TickDurationSummary(
            ordered.Length == 0 ? 0.0 : ordered[0],
            ordered.Length == 0 ? 0.0 : ordered.Average(),
            Percentile(ordered, 0.50),
            Percentile(ordered, 0.95),
            Percentile(ordered, 0.99),
            ordered.Length == 0 ? 0.0 : ordered[^1]);

        return new HeadlessTelemetryReport(
            1,
            _startedUtc,
            DateTimeOffset.UtcNow,
            _firstTick,
            _lastTick,
            checked((ulong)_durationsMilliseconds.Count),
            _commandsProcessed,
            _eventsDispatched,
            _peakEntityCount,
            _peakManagedBytes,
            summary,
            _checkpoints.ToArray(),
            _faults.ToArray());
    }

    public void WriteJson(Stream stream, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        JsonSerializer.Serialize(stream, CreateReport(), new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        if (ordered.Count == 0)
        {
            return 0.0;
        }

        double index = percentile * (ordered.Count - 1);
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);
        if (lower == upper)
        {
            return ordered[lower];
        }

        double fraction = index - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
    }
}

public static class HeadlessTelemetryText
{
    public static string Format(HeadlessTelemetryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return string.Join(Environment.NewLine,
            $"Ticks: {report.TickCount:N0} ({report.FirstTick:N0}..{report.LastTick:N0})",
            $"Commands: {report.CommandsProcessed:N0}",
            $"Events: {report.EventsDispatched:N0}",
            $"Peak entities: {report.PeakEntityCount:N0}",
            $"Peak managed memory: {report.PeakManagedBytes / (1024.0 * 1024.0):N2} MiB",
            $"Tick ms avg/p95/p99/max: {report.TickDurations.AverageMilliseconds:N4} / {report.TickDurations.P95Milliseconds:N4} / {report.TickDurations.P99Milliseconds:N4} / {report.TickDurations.MaximumMilliseconds:N4}",
            $"Checkpoints: {report.Checkpoints.Count:N0}",
            $"Faults: {report.Faults.Count:N0}");
    }
}
