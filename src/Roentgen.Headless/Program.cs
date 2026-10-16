using System.Globalization;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Diagnostics;
using Roentgen.Generation;
using Roentgen.Persistence;
using Roentgen.Simulation;

namespace Roentgen.Headless;

public static class Program
{
    public static int Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            HeadlessArguments arguments = HeadlessArguments.Parse(args);
            return arguments.Command switch
            {
                "run" => Run(arguments, cancellation.Token),
                "replay" => Replay(arguments, cancellation.Token),
                "soak" => Soak(arguments, cancellation.Token),
                "console" => ConsoleMode(arguments),
                "inspect-snapshot" => InspectSnapshot(arguments),
                "help" or "--help" or "-h" => Help(),
                _ => throw new ArgumentException($"Unknown headless command '{arguments.Command}'.")
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int Run(HeadlessArguments arguments, CancellationToken cancellationToken)
    {
        ulong ticks = arguments.GetUInt64("ticks", 10_000UL);
        if (ticks == 0UL)
        {
            throw new ArgumentOutOfRangeException("ticks", "Run mode requires at least one tick.");
        }

        string? snapshotInput = arguments.GetOptionalString("snapshot-in");
        string? replayOutput = arguments.GetOptionalString("replay-out");
        if (snapshotInput is not null && replayOutput is not null)
        {
            throw new ArgumentException("--replay-out cannot be combined with --snapshot-in because the replay would not contain the snapshot's prior history.");
        }

        SimulationSnapshot? initialSnapshot = snapshotInput is null ? null : ReadSnapshot(snapshotInput);
        ulong seedValue = initialSnapshot?.CampaignSeed.Value ?? arguments.GetUInt64("seed", 7_351_982_471UL);
        int ticksPerSecond = initialSnapshot?.TicksPerSecond ?? arguments.GetInt32("ticks-per-second", 60);
        int checksumInterval = arguments.GetInt32("checksum-interval", 600);
        int entityCount = arguments.GetInt32("entities", 32);
        string scenario = arguments.GetString("scenario", "smoke").ToLowerInvariant();

        var telemetry = new HeadlessTelemetryCollector();
        using SimulationHost host = HeadlessSimulationFactory.Create(
            new CampaignSeed(seedValue),
            ticksPerSecond,
            checksumInterval,
            telemetry);
        if (initialSnapshot is not null)
        {
            host.Restore(initialSnapshot);
        }

        IReadOnlyList<CommandEnvelope> scheduledCommands = scenario switch
        {
            "none" => [],
            "smoke" when initialSnapshot is null => HeadlessSimulationFactory.ScheduleSmokeScenario(
                host,
                entityCount,
                checked(host.Clock.Tick + ticks)),
            "smoke" => [],
            _ => throw new ArgumentException($"Unknown scenario '{scenario}'.")
        };

        using FileStream? replayStream = replayOutput is null ? null : CreateOutput(replayOutput);
        using ReplayLogWriter? replayWriter = replayStream is null ? null : new ReplayLogWriter(
            replayStream,
            new ReplayHeader(
                ReplayLogWriter.CurrentFormatVersion,
                ConstitutionVersion.Product,
                ConstitutionVersion.Current,
                host.CampaignSeed,
                host.Clock.TicksPerSecond,
                Pcg32Random.CurrentAlgorithmId,
                $"{BuildIdentity.DevelopmentPhase}|{BuildIdentity.AssemblyVersion}"));
        if (replayWriter is not null)
        {
            foreach (CommandEnvelope command in scheduledCommands)
            {
                replayWriter.AppendCommand(command);
            }
        }

        SimulationRunResult result = HeadlessExecution.Run(host, ticks, replayWriter, cancellationToken);
        StateChecksum? replayChain = replayWriter?.Complete();

        string? snapshotOutput = arguments.GetOptionalString("snapshot-out");
        if (snapshotOutput is not null)
        {
            using FileStream stream = CreateOutput(snapshotOutput);
            SimulationSnapshotSerializer.Write(stream, host.CaptureSnapshot());
        }

        HeadlessTelemetryReport report = telemetry.CreateReport();
        WriteTelemetryIfRequested(arguments, report);
        Console.WriteLine($"Seed: {host.CampaignSeed.Value}");
        Console.WriteLine($"Ticks: {result.StartTick + 1UL}..{result.EndTick} ({result.TicksExecuted:N0})");
        Console.WriteLine($"Entities: {host.Entities.Count:N0}");
        Console.WriteLine($"Final checksum: {result.FinalChecksum}");
        if (replayChain is StateChecksum chain)
        {
            Console.WriteLine($"Replay chain: {chain}");
        }

        Console.WriteLine(HeadlessTelemetryText.Format(report));
        return report.Faults.Count == 0 ? 0 : 2;
    }

    private static int Replay(HeadlessArguments arguments, CancellationToken cancellationToken)
    {
        string input = arguments.GetOptionalString("input")
            ?? throw new ArgumentException("Replay mode requires --input <path>.");
        using FileStream replayStream = File.OpenRead(input);
        ReplayLog replay = ReplayLogReader.Read(replayStream);
        string? requestedTicksText = arguments.GetOptionalString("final-tick");
        ulong? requestedFinalTick = requestedTicksText is null
            ? null
            : ulong.Parse(requestedTicksText, NumberStyles.Integer, CultureInfo.InvariantCulture);
        var telemetry = new HeadlessTelemetryCollector();
        ReplayExecutionResult result = HeadlessExecution.Replay(replay, requestedFinalTick, telemetry, cancellationToken);

        string? snapshotOutput = arguments.GetOptionalString("snapshot-out");
        if (snapshotOutput is not null)
        {
            using FileStream stream = CreateOutput(snapshotOutput);
            SimulationSnapshotSerializer.Write(stream, result.FinalSnapshot);
        }

        HeadlessTelemetryReport report = telemetry.CreateReport();
        WriteTelemetryIfRequested(arguments, report);
        Console.WriteLine($"Replay final tick: {result.FinalTick:N0}");
        Console.WriteLine($"Replay final checksum: {result.FinalChecksum}");
        Console.WriteLine($"Checkpoint match: {result.IsMatch}");
        foreach (DeterminismMismatch mismatch in result.Mismatches)
        {
            Console.WriteLine($"Mismatch tick {mismatch.Tick}: expected={mismatch.Expected} actual={mismatch.Actual} {mismatch.Context}");
        }

        Console.WriteLine(HeadlessTelemetryText.Format(report));
        return result.IsMatch ? 0 : 3;
    }

    private static int Soak(HeadlessArguments arguments, CancellationToken cancellationToken)
    {
        var options = new HeadlessSoakOptions(
            arguments.GetUInt64("start-seed", 1UL),
            arguments.GetInt32("seeds", 10),
            arguments.GetUInt64("ticks", 10_000UL),
            arguments.GetInt32("entities", 64),
            arguments.GetInt32("repetitions", 2),
            arguments.GetInt32("checksum-interval", 600));
        HeadlessSoakReport report = HeadlessSoakRunner.Run(options, cancellationToken);
        string? reportPath = arguments.GetOptionalString("report");
        if (reportPath is not null)
        {
            using FileStream stream = CreateOutput(reportPath);
            HeadlessSoakRunner.WriteJson(stream, report);
        }

        Console.WriteLine($"Soak runs: {report.Runs.Count:N0}");
        Console.WriteLine($"Determinism mismatches: {report.MismatchCount:N0}");
        Console.WriteLine($"Faults: {report.FaultCount:N0}");
        foreach (HeadlessSoakSeedResult result in report.Runs)
        {
            Console.WriteLine($"seed={result.Seed} repetition={result.Repetition} tick={result.FinalTick} entities={result.FinalEntityCount} checksum={result.FinalChecksum} match={result.MatchesFirstRepetition}");
        }

        return report.MismatchCount == 0 && report.FaultCount == 0 ? 0 : 4;
    }

    private static int ConsoleMode(HeadlessArguments arguments)
    {
        ulong seed = arguments.GetUInt64("seed", 7_351_982_471UL);
        int ticksPerSecond = arguments.GetInt32("ticks-per-second", 60);
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(seed), ticksPerSecond, checksumInterval: 60);
        string? snapshotInput = arguments.GetOptionalString("snapshot-in");
        if (snapshotInput is not null)
        {
            host.Restore(ReadSnapshot(snapshotInput));
        }

        return new HeadlessCommandConsole(host, Console.In, Console.Out).Run();
    }

    private static int InspectSnapshot(HeadlessArguments arguments)
    {
        string input = arguments.GetOptionalString("input")
            ?? throw new ArgumentException("inspect-snapshot requires --input <path>.");
        SimulationSnapshot snapshot = ReadSnapshot(input);
        Console.WriteLine($"Format: {snapshot.FormatVersion}");
        Console.WriteLine($"Seed: {snapshot.CampaignSeed.Value}");
        Console.WriteLine($"Tick rate: {snapshot.TicksPerSecond}");
        Console.WriteLine($"Completed tick: {snapshot.CompletedTick}");
        Console.WriteLine($"Entities: {snapshot.EntityRegistry.Entities.Count}");
        Console.WriteLine($"State values: {snapshot.StateStore.Values.Count}");
        Console.WriteLine($"Pending commands: {snapshot.PendingCommands.Count}");
        Console.WriteLine($"State checksum: {snapshot.StateChecksum}");
        return 0;
    }

    private static int Help()
    {
        Console.WriteLine("Roentgen.Headless — authoritative simulation shell");
        Console.WriteLine();
        Console.WriteLine("run --seed N --ticks N [--scenario smoke|none] [--entities N]");
        Console.WriteLine("    [--checksum-interval N] [--replay-out path] [--snapshot-in path]");
        Console.WriteLine("    [--snapshot-out path] [--telemetry-out path]");
        Console.WriteLine("replay --input path [--final-tick N] [--snapshot-out path] [--telemetry-out path]");
        Console.WriteLine("soak --start-seed N --seeds N --ticks N --entities N --repetitions N [--report path]");
        Console.WriteLine("console [--seed N] [--snapshot-in path]");
        Console.WriteLine("inspect-snapshot --input path");
        return 0;
    }

    private static SimulationSnapshot ReadSnapshot(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SimulationSnapshotSerializer.Read(stream);
    }

    private static void WriteTelemetryIfRequested(HeadlessArguments arguments, HeadlessTelemetryReport report)
    {
        string? path = arguments.GetOptionalString("telemetry-out");
        if (path is null)
        {
            return;
        }

        using FileStream stream = CreateOutput(path);
        System.Text.Json.JsonSerializer.Serialize(stream, report, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    private static FileStream CreateOutput(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return File.Create(fullPath);
    }
}
