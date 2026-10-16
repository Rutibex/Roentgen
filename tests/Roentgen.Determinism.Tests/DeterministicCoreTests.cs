using System.Text;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Generation;
using Roentgen.Persistence;
using Roentgen.Simulation;

namespace Roentgen.Determinism.Tests;

public sealed class DeterministicCoreTests
{
    [Fact]
    public void StableIdsAreReproducibleAndHierarchical()
    {
        StableId first = StableIdFactory.Create("world.landmark", "north-relay");
        StableId repeat = StableIdFactory.Create("world.landmark", "north-relay");
        StableId child = StableIdFactory.CreateChild(first, "room", 3);

        Assert.Equal(first, repeat);
        Assert.NotEqual(first, child);
        Assert.Equal(first, StableId.Parse(first.ToString()));
    }

    [Fact]
    public void SeedDomainsAreNormalizedAndSeparated()
    {
        var campaign = new CampaignSeed(7_351_982_471UL);
        DomainSeed terrain = GenerationDomain.Derive(campaign, " World.Terrain ");
        DomainSeed repeat = GenerationDomain.Derive(campaign, "world.terrain");
        DomainSeed history = GenerationDomain.Derive(campaign, "world.history");

        Assert.Equal(terrain, repeat);
        Assert.NotEqual(terrain.Value, history.Value);
        Assert.Equal("world.terrain", terrain.Domain);
    }

    [Fact]
    public void PcgStreamMatchesGoldenVector()
    {
        DomainSeed domain = GenerationDomain.Derive(new CampaignSeed(7_351_982_471UL), "world.terrain");
        Pcg32Random random = RandomStreamFactory.Create(domain, "height.primary");
        uint[] actual = Enumerable.Range(0, 8).Select(_ => random.NextUInt32()).ToArray();
        uint[] expected = [
            2_099_954_129U,
            2_560_230_803U,
            3_337_791_276U,
            2_075_296_612U,
            766_993_672U,
            1_500_560_158U,
            1_803_241_724U,
            1_759_725_560U
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RandomSnapshotRestoresExactContinuation()
    {
        DomainSeed domain = GenerationDomain.Derive(new CampaignSeed(42UL), "test.snapshot");
        Pcg32Random original = RandomStreamFactory.Create(domain, "primary");
        _ = Enumerable.Range(0, 13).Select(_ => original.NextUInt32()).ToArray();
        RandomStreamSnapshot snapshot = original.Capture();
        uint[] expected = Enumerable.Range(0, 20).Select(_ => original.NextUInt32()).ToArray();
        Pcg32Random restored = Pcg32Random.Restore(snapshot);
        uint[] actual = Enumerable.Range(0, 20).Select(_ => restored.NextUInt32()).ToArray();

        Assert.Equal(expected, actual);
    }


    [Fact]
    public void CanonicalWriterAndReaderRoundTripAllCoreTypes()
    {
        StableId id = StableIdFactory.Create("test", "canonical");
        StateChecksum checksum = StateChecksum.Parse(new string('b', 64));
        var writer = new CanonicalWriter();
        writer.WriteNull();
        writer.WriteBoolean(true);
        writer.WriteInt32(-12);
        writer.WriteUInt32(42);
        writer.WriteInt64(-9_000_000_000L);
        writer.WriteUInt64(18_000_000_000UL);
        writer.WriteSingle(-0.0f);
        writer.WriteDouble(12.5);
        writer.WriteString("Cafe\u0301");
        writer.WriteBytes(new byte[] { 1, 2, 3 });
        writer.WriteStableId(id);
        writer.WriteChecksum(checksum);
        writer.WriteCount(7);

        var reader = new CanonicalReader(writer.ToArray());
        reader.ReadNull();
        Assert.True(reader.ReadBoolean());
        Assert.Equal(-12, reader.ReadInt32());
        Assert.Equal<uint>(42, reader.ReadUInt32());
        Assert.Equal(-9_000_000_000L, reader.ReadInt64());
        Assert.Equal<ulong>(18_000_000_000UL, reader.ReadUInt64());
        Assert.Equal(0.0f, reader.ReadSingle());
        Assert.Equal(12.5, reader.ReadDouble());
        Assert.Equal("Café", reader.ReadRequiredString());
        Assert.Equal(new byte[] { 1, 2, 3 }, reader.ReadBytes());
        Assert.Equal(id, reader.ReadStableId());
        Assert.Equal(checksum, reader.ReadChecksum());
        Assert.Equal(7, reader.ReadCount());
        reader.EnsureComplete();
    }

    [Fact]
    public void StableIdsAndSeedsIgnoreCurrentCulture()
    {
        System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            StableId turkishId = StableIdFactory.Create("WORLD.ITEM", "IDENTIFIER");
            DomainSeed turkishSeed = GenerationDomain.Derive(new CampaignSeed(19), "WORLD.ITEM");
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            StableId frenchId = StableIdFactory.Create("WORLD.ITEM", "IDENTIFIER");
            DomainSeed frenchSeed = GenerationDomain.Derive(new CampaignSeed(19), "WORLD.ITEM");
            Assert.Equal(turkishId, frenchId);
            Assert.Equal(turkishSeed, frenchSeed);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ChecksumContributorRegistrationOrderDoesNotMatter()
    {
        var first = new SimulationChecksumService();
        first.Register(new FixedContributor("z", 2));
        first.Register(new FixedContributor("a", 1));
        var second = new SimulationChecksumService();
        second.Register(new FixedContributor("a", 1));
        second.Register(new FixedContributor("z", 2));
        Assert.Equal(first.Compute(10), second.Compute(10));
    }
    [Fact]
    public void CommandQueueUsesCanonicalOrdering()
    {
        StableId issuer = StableIdFactory.Create("test", "issuer");
        var queue = new DeterministicCommandQueue();
        queue.Enqueue(CommandEnvelopeFactory.Create(issuer, 10, CommandOriginPriority.Player, 2, new AddCommand(2)));
        queue.Enqueue(CommandEnvelopeFactory.Create(issuer, 10, CommandOriginPriority.Engine, 99, new AddCommand(99)));
        queue.Enqueue(CommandEnvelopeFactory.Create(issuer, 10, CommandOriginPriority.Player, 1, new AddCommand(1)));

        IReadOnlyList<CommandEnvelope> drained = queue.DrainTick(10);
        Assert.Equal(3, drained.Count);
        Assert.Equal(CommandOriginPriority.Engine, drained[0].OriginPriority);
        Assert.Equal<ulong>(1, drained[1].Sequence);
        Assert.Equal<ulong>(2, drained[2].Sequence);
    }

    [Fact]
    public void EventHandlersRunByPriorityThenStableId()
    {
        var events = new DeterministicEventBus();
        var calls = new List<string>();
        events.Register(new RecordingEventHandler("z-last", 10, calls));
        events.Register(new RecordingEventHandler("b-second", 0, calls));
        events.Register(new RecordingEventHandler("a-first", 0, calls));
        events.Publish(StableIdFactory.Create("test", "producer"), 1, new ValueEvent(7));

        var context = new SimulationTickContext(1, new CampaignSeed(1), events);
        events.Flush(context);

        Assert.Equal(new[] { "a-first", "b-second", "z-last" }, calls);
    }

    [Fact]
    public void ReplayRoundTripVerifiesHashChain()
    {
        StableId issuer = StableIdFactory.Create("test", "replay-issuer");
        CommandEnvelope command = CommandEnvelopeFactory.Create(issuer, 12, CommandOriginPriority.Player, 1, new AddCommand(5));
        var header = new ReplayHeader(
            ReplayLogWriter.CurrentFormatVersion,
            "Roentgen",
            "0.1.0",
            new CampaignSeed(1234UL),
            60,
            Pcg32Random.CurrentAlgorithmId,
            "test-build");

        using var stream = new MemoryStream();
        using (var writer = new ReplayLogWriter(stream, header, leaveOpen: true))
        {
            writer.AppendCommand(command);
            writer.AppendCheckpoint(new ReplayCheckpoint(12, StateChecksum.Parse(new string('a', 64))));
            writer.Complete();
        }

        stream.Position = 0;
        ReplayLog replay = ReplayLogReader.Read(stream);
        Assert.Equal(header, replay.Header);
        Assert.Single(replay.Commands);
        Assert.Single(replay.Checkpoints);
        Assert.False(replay.FinalChainChecksum.IsZero);
    }


    [Fact]
    public void CrossLanguageReplayFixtureIsAccepted()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "tests", "fixtures", "phase2-golden.replay.jsonl");
        using FileStream stream = File.OpenRead(path);
        ReplayLog replay = ReplayLogReader.Read(stream);
        Assert.Equal<ulong>(7_351_982_471UL, replay.Header.CampaignSeed.Value);
        Assert.Single(replay.Commands);
        Assert.Single(replay.Checkpoints);
        Assert.Equal("7e00b42577cc7cfe21181967554b55c1880bbc758e6cf4f7d2bff8f7a46499cf", replay.FinalChainChecksum.ToString());
    }

    [Fact]
    public void ReplayWriterMatchesCrossLanguageGoldenFixture()
    {
        StableId issuer = StableIdFactory.Create("fixture", "issuer");
        CommandEnvelope command = CommandEnvelopeFactory.Create(issuer, 12, CommandOriginPriority.Player, 1, new AddCommand(5));
        var header = new ReplayHeader(
            ReplayLogWriter.CurrentFormatVersion,
            "Roentgen",
            "0.1.0",
            new CampaignSeed(7_351_982_471UL),
            60,
            Pcg32Random.CurrentAlgorithmId,
            "phase2-golden");

        using var stream = new MemoryStream();
        using (var writer = new ReplayLogWriter(stream, header, leaveOpen: true))
        {
            writer.AppendCommand(command);
            writer.AppendCheckpoint(new ReplayCheckpoint(12, StateChecksum.Parse(new string('a', 64))));
            writer.Complete();
        }

        string expected = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "phase2-golden.replay.jsonl"));
        string actual = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(expected, actual);
    }
    [Fact]
    public void ReplayTamperingIsDetected()
    {
        StableId issuer = StableIdFactory.Create("test", "tamper-issuer");
        CommandEnvelope command = CommandEnvelopeFactory.Create(issuer, 1, CommandOriginPriority.Player, 0, new AddCommand(1));
        var header = new ReplayHeader(1, "Roentgen", "0.1.0", new CampaignSeed(1), 60, Pcg32Random.CurrentAlgorithmId, "test");
        using var stream = new MemoryStream();
        using (var writer = new ReplayLogWriter(stream, header, leaveOpen: true))
        {
            writer.AppendCommand(command);
            writer.Complete();
        }

        string text = Encoding.UTF8.GetString(stream.ToArray()).Replace("AgAAAAE=", "AgAAAAI=", StringComparison.Ordinal);
        using var tampered = new MemoryStream(Encoding.UTF8.GetBytes(text));
        Assert.Throws<InvalidDataException>(() => ReplayLogReader.Read(tampered));
    }

    [Fact]
    public void RepeatedPipelinesProduceSameStateChecksum()
    {
        StateChecksum first = RunCounterScenario();
        StateChecksum second = RunCounterScenario();
        Assert.Equal(first, second);
    }


    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "eng", "projects.json")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
    private static StateChecksum RunCounterScenario()
    {
        StableId issuer = StableIdFactory.Create("test", "pipeline-issuer");
        var queue = new DeterministicCommandQueue();
        var router = new CommandRouter();
        var events = new DeterministicEventBus();
        var state = new CounterState();
        router.Register(new AddCommandHandler(state));
        var checksums = new SimulationChecksumService();
        checksums.Register(state);
        var pipeline = new SimulationPipeline(new CampaignSeed(77), queue, router, events, checksums);
        pipeline.RegisterSystem(new CounterSystem(state));

        queue.Enqueue(CommandEnvelopeFactory.Create(issuer, 1, CommandOriginPriority.Player, 0, new AddCommand(3)));
        queue.Enqueue(CommandEnvelopeFactory.Create(issuer, 3, CommandOriginPriority.Player, 1, new AddCommand(4)));
        StateChecksum result = default;
        for (ulong tick = 0; tick <= 5; tick++)
        {
            result = pipeline.RunTick(tick, computeChecksum: true).StateChecksum!.Value;
        }

        return result;
    }

    private sealed record AddCommand(int Amount) : ISimulationCommand
    {
        public string TypeId => "test.add";
        public int SchemaVersion => 1;
        public void WritePayload(CanonicalWriter writer) => writer.WriteInt32(Amount);
    }

    private sealed record ValueEvent(int Value) : ISimulationEvent
    {
        public string TypeId => "test.value";
        public int SchemaVersion => 1;
        public void WritePayload(CanonicalWriter writer) => writer.WriteInt32(Value);
    }

    private sealed class RecordingEventHandler(string handlerId, int priority, List<string> calls) : IEventHandler
    {
        public string HandlerId { get; } = handlerId;
        public string EventTypeId => "test.value";
        public int Priority { get; } = priority;
        public void Handle(EventEnvelope @event, SimulationTickContext context) => calls.Add(HandlerId);
    }


    private sealed class FixedContributor(string id, int value) : IStateChecksumContributor
    {
        public string ContributorId { get; } = id;
        public void WriteState(CanonicalWriter writer) => writer.WriteInt32(value);
    }
    private sealed class CounterState : IStateChecksumContributor
    {
        public int Value { get; set; }
        public string ContributorId => "test.counter";
        public void WriteState(CanonicalWriter writer) => writer.WriteInt32(Value);
    }

    private sealed class AddCommandHandler(CounterState state) : ICommandHandler
    {
        public string HandlerId => "test.add-handler";
        public string CommandTypeId => "test.add";
        public int MinimumSchemaVersion => 1;
        public int MaximumSchemaVersion => 1;

        public void Handle(CommandEnvelope command, SimulationTickContext context)
        {
            var reader = new CanonicalReader(command.Payload);
            int amount = reader.ReadInt32();
            reader.EnsureComplete();
            state.Value += amount;
        }
    }

    private sealed class CounterSystem(CounterState state) : ISimulationSystem
    {
        public string SystemId => "test.counter-system";
        public int Order => 0;
        public void Update(SimulationTickContext context) => state.Value += 1;
    }
}
