using System.Text.Json;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Diagnostics;
using Roentgen.Entities;
using Roentgen.Generation;
using Roentgen.Headless;
using Roentgen.Persistence;
using Roentgen.Simulation;

namespace Roentgen.Headless.Tests;

public sealed class HeadlessSimulationTests
{
    [Fact]
    public void EntityRegistryUsesStableOrderingAndRoundTrips()
    {
        var registry = new EntityRegistry();
        StableId second = StableIdFactory.Create("test.entity", "second");
        StableId first = StableIdFactory.Create("test.entity", "first");
        registry.Create(second, "machine", 2);
        registry.Create(first, "human", 1);
        registry.AddTag(first, "living");
        registry.UpsertComponent(first, EntityComponentState.Create("health", 1, writer => writer.WriteInt64(100)));

        EntityRegistrySnapshot snapshot = registry.CaptureSnapshot();
        Assert.Equal(snapshot.Entities.OrderBy(item => item.Id).Select(item => item.Id).ToArray(), snapshot.Entities.Select(item => item.Id).ToArray());

        var restored = new EntityRegistry();
        restored.Restore(snapshot);
        var firstWriter = new CanonicalWriter();
        var secondWriter = new CanonicalWriter();
        registry.WriteCanonical(firstWriter);
        restored.WriteCanonical(secondWriter);
        Assert.Equal(firstWriter.ToArray(), secondWriter.ToArray());
    }

    [Fact]
    public void HeadlessHostRunsTenThousandTicksWithoutPresentationDependencies()
    {
        var telemetry = new HeadlessTelemetryCollector();
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(41), checksumInterval: 600, telemetry: telemetry);
        HeadlessSimulationFactory.ScheduleSmokeScenario(host, 16, 10_000);
        SimulationRunResult result = HeadlessExecution.Run(host, 10_000);

        Assert.Equal<ulong>(10_000, result.EndTick);
        Assert.Equal(15, host.Entities.Count);
        Assert.False(result.FinalChecksum.IsZero);
        Assert.Empty(telemetry.CreateReport().Faults);
    }

    [Fact]
    public void SnapshotRoundTripPreservesPendingCommandsAndContinuation()
    {
        using SimulationHost original = HeadlessSimulationFactory.Create(new CampaignSeed(99), checksumInterval: 60);
        HeadlessSimulationFactory.ScheduleSmokeScenario(original, 5, 200);
        HeadlessExecution.Run(original, 100);

        using var stream = new MemoryStream();
        SimulationSnapshotSerializer.Write(stream, original.CaptureSnapshot());
        stream.Position = 0;
        SimulationSnapshot snapshot = SimulationSnapshotSerializer.Read(stream);

        SimulationRunResult expected = HeadlessExecution.Run(original, 100);
        using SimulationHost restored = HeadlessSimulationFactory.Create(new CampaignSeed(99), checksumInterval: 60);
        restored.Restore(snapshot);
        SimulationRunResult actual = HeadlessExecution.Run(restored, 100);

        Assert.Equal(expected.FinalChecksum, actual.FinalChecksum);
        Assert.Equal(original.Entities.Count, restored.Entities.Count);
        Assert.Equal(original.Commands.Count, restored.Commands.Count);
    }

    [Fact]
    public void RecordedReplayReconstructsCheckpointHistory()
    {
        var telemetry = new HeadlessTelemetryCollector();
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(7351982471), checksumInterval: 30, telemetry: telemetry);
        IReadOnlyList<CommandEnvelope> commands = HeadlessSimulationFactory.ScheduleSmokeScenario(host, 10, 300);
        using var stream = new MemoryStream();
        using (var writer = new ReplayLogWriter(stream, new ReplayHeader(
            ReplayLogWriter.CurrentFormatVersion,
            ConstitutionVersion.Product,
            ConstitutionVersion.Current,
            host.CampaignSeed,
            60,
            Pcg32Random.CurrentAlgorithmId,
            "phase3-test"), leaveOpen: true))
        {
            foreach (CommandEnvelope command in commands)
            {
                writer.AppendCommand(command);
            }

            HeadlessExecution.Run(host, 300, writer);
            writer.Complete();
        }

        stream.Position = 0;
        ReplayLog replay = ReplayLogReader.Read(stream);
        ReplayExecutionResult result = HeadlessExecution.Replay(replay);
        Assert.True(result.IsMatch);
        Assert.Empty(result.Mismatches);
    }

    [Fact]
    public void SystemsAreScheduledByPhaseThenOrderThenId()
    {
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(1));
        IReadOnlyList<SimulationSystemDescriptor> schedule = host.Pipeline.GetSchedule();
        Assert.Equal(schedule.OrderBy(item => item.Phase).ThenBy(item => item.Order).ThenBy(item => item.SystemId, StringComparer.Ordinal).ToArray(), schedule.ToArray());
        Assert.Equal(SimulationSystemPhase.Gameplay, schedule[0].Phase);
        Assert.Equal(SimulationSystemPhase.Finalization, schedule[^1].Phase);
    }

    [Fact]
    public void ConsoleTokenizerSupportsQuotedArguments()
    {
        string[] tokens = HeadlessCommandConsole.Tokenize("set-global \"quest stage\" 12");
        Assert.Equal(new[] { "set-global", "quest stage", "12" }, tokens);
    }

    [Fact]
    public void SoakRunnerRepeatsSeedsWithIdenticalChecksums()
    {
        HeadlessSoakReport report = HeadlessSoakRunner.Run(new HeadlessSoakOptions(
            StartSeed: 10,
            SeedCount: 2,
            TicksPerSeed: 1_000,
            EntitiesPerSeed: 8,
            Repetitions: 2,
            ChecksumInterval: 100));

        Assert.Equal(4, report.Runs.Count);
        Assert.Equal(0, report.MismatchCount);
        Assert.Equal(0, report.FaultCount);
    }

    [Fact]
    public void CorruptedSnapshotIsRejected()
    {
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(1));
        HeadlessExecution.Run(host, 1);
        using var stream = new MemoryStream();
        SimulationSnapshotSerializer.Write(stream, host.CaptureSnapshot());
        byte[] bytes = stream.ToArray();
        bytes[20] ^= 0x7f;
        using var corrupted = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => SimulationSnapshotSerializer.Read(corrupted));
    }

    [Fact]
    public void CrossLanguageEmptySnapshotFixtureIsAccepted()
    {
        string path = Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "phase3-empty.rgsnap");
        using FileStream stream = File.OpenRead(path);
        SimulationSnapshot snapshot = SimulationSnapshotSerializer.Read(stream);
        Assert.Equal<ulong>(7_351_982_471UL, snapshot.CampaignSeed.Value);
        Assert.Equal<ulong>(0UL, snapshot.CompletedTick);
        Assert.Empty(snapshot.EntityRegistry.Entities);
        Assert.Empty(snapshot.StateStore.Values);
        Assert.Equal("4904bf054418a148ba9ef93055cd0f75011fae7e4bc7fc999762b84840ca2a69", snapshot.StateChecksum.ToString());

        using SimulationHost host = HeadlessSimulationFactory.Create(snapshot.CampaignSeed, snapshot.TicksPerSecond);
        host.Restore(snapshot);
        Assert.Equal(snapshot.StateChecksum, host.ChecksumService.Compute(0));
    }


    [Fact]
    public void OneTickHeadlessChecksumMatchesCrossLanguageReference()
    {
        string path = Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "phase3-headless-golden.json");
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = fixture.RootElement;
        ulong seed = root.GetProperty("seed").GetUInt64();
        ulong ticks = root.GetProperty("ticks").GetUInt64();
        string expected = root.GetProperty("finalChecksum").GetString()!;

        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(seed));
        SimulationRunResult result = HeadlessExecution.Run(host, ticks);
        Assert.Equal(expected, result.FinalChecksum.ToString());
    }


    [Fact]
    public void FaultedHostCannotContinueAfterAuthoritativeFailure()
    {
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(17));
        StableId issuer = StableIdFactory.CreateCampaignRoot(host.CampaignSeed);
        StableId missing = StableIdFactory.CreateChild(issuer, "missing", 0);
        host.Enqueue(issuer, 1, CommandOriginPriority.Engine, new DestroyEntityCommand(missing));

        Assert.Throws<InvalidOperationException>(() => host.Step());
        Assert.True(host.IsFaulted);
        Assert.NotNull(host.FaultException);
        Assert.Throws<InvalidOperationException>(() => host.Step());
    }


    [Fact]
    public void HostRejectsCommandsForCompletedTicksWithoutConsumingSequence()
    {
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(19));
        StableId issuer = StableIdFactory.CreateCampaignRoot(host.CampaignSeed);
        ulong before = host.CommandSequences.NextSequence;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            host.Enqueue(issuer, host.Clock.Tick, CommandOriginPriority.Engine, new SetGlobalInt64Command("invalid", 1)));
        Assert.Equal(before, host.CommandSequences.NextSequence);
        Assert.Equal(0, host.Commands.Count);
    }

    [Fact]
    public void FailedEnvelopeCreationRestoresCommandSequence()
    {
        using SimulationHost host = HeadlessSimulationFactory.Create(new CampaignSeed(18));
        StableId issuer = StableIdFactory.CreateCampaignRoot(host.CampaignSeed);
        ulong before = host.CommandSequences.NextSequence;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            host.Enqueue(issuer, 1, CommandOriginPriority.Engine, new InvalidSchemaCommand()));
        Assert.Equal(before, host.CommandSequences.NextSequence);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Roentgen.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Roentgen repository root.");
    }

    private sealed class InvalidSchemaCommand : ISimulationCommand
    {
        public string TypeId => "test.invalid-schema";
        public int SchemaVersion => 0;
        public void WritePayload(CanonicalWriter writer) { }
    }
}
