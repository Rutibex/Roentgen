using System.Text.Json;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Diagnostics;
using Roentgen.Simulation;

namespace Roentgen.Headless;

public sealed record HeadlessSoakOptions(
    ulong StartSeed,
    int SeedCount,
    ulong TicksPerSeed,
    int EntitiesPerSeed,
    int Repetitions,
    int ChecksumInterval = 600);

public sealed record HeadlessSoakSeedResult(
    ulong Seed,
    int Repetition,
    ulong FinalTick,
    int FinalEntityCount,
    StateChecksum FinalChecksum,
    double ElapsedMilliseconds,
    double AverageTickMilliseconds,
    bool MatchesFirstRepetition);

public sealed record HeadlessSoakFault(
    ulong Seed,
    int Repetition,
    string ExceptionType,
    string Message);

public sealed record HeadlessSoakReport(
    int SchemaVersion,
    HeadlessSoakOptions Options,
    IReadOnlyList<HeadlessSoakSeedResult> Runs,
    IReadOnlyList<HeadlessSoakFault> Faults,
    int MismatchCount,
    int FaultCount);

public static class HeadlessSoakRunner
{
    public static HeadlessSoakReport Run(HeadlessSoakOptions options, CancellationToken cancellationToken = default)
    {
        Validate(options);
        var results = new List<HeadlessSoakSeedResult>(checked(options.SeedCount * options.Repetitions));
        var faults = new List<HeadlessSoakFault>();
        for (int seedIndex = 0; seedIndex < options.SeedCount; seedIndex++)
        {
            ulong seed = checked(options.StartSeed + (ulong)seedIndex);
            StateChecksum? baseline = null;
            for (int repetition = 1; repetition <= options.Repetitions; repetition++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var telemetry = new HeadlessTelemetryCollector();
                try
                {
                    using SimulationHost host = HeadlessSimulationFactory.Create(
                        new CampaignSeed(seed),
                        checksumInterval: options.ChecksumInterval,
                        telemetry: telemetry);
                    HeadlessSimulationFactory.ScheduleSmokeScenario(host, options.EntitiesPerSeed, options.TicksPerSeed);
                    SimulationRunResult run = HeadlessExecution.Run(host, options.TicksPerSeed, cancellationToken: cancellationToken);
                    HeadlessTelemetryReport report = telemetry.CreateReport();
                    bool matches = baseline is null || baseline.Value == run.FinalChecksum;
                    baseline ??= run.FinalChecksum;
                    results.Add(new HeadlessSoakSeedResult(
                        seed,
                        repetition,
                        run.EndTick,
                        host.Entities.Count,
                        run.FinalChecksum,
                        run.Elapsed.TotalMilliseconds,
                        report.TickDurations.AverageMilliseconds,
                        matches));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    faults.Add(new HeadlessSoakFault(
                        seed,
                        repetition,
                        exception.GetType().FullName ?? exception.GetType().Name,
                        exception.Message));
                }
            }
        }

        return new HeadlessSoakReport(
            1,
            options,
            results,
            faults,
            results.Count(item => !item.MatchesFirstRepetition),
            faults.Count);
    }

    public static void WriteJson(Stream stream, HeadlessSoakReport report)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(report);
        JsonSerializer.Serialize(stream, report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static void Validate(HeadlessSoakOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.SeedCount <= 0) throw new ArgumentOutOfRangeException(nameof(options.SeedCount));
        if (options.TicksPerSeed == 0UL) throw new ArgumentOutOfRangeException(nameof(options.TicksPerSeed));
        if (options.EntitiesPerSeed < 0) throw new ArgumentOutOfRangeException(nameof(options.EntitiesPerSeed));
        if (options.Repetitions <= 0) throw new ArgumentOutOfRangeException(nameof(options.Repetitions));
        if (options.ChecksumInterval < 0) throw new ArgumentOutOfRangeException(nameof(options.ChecksumInterval));
    }
}
