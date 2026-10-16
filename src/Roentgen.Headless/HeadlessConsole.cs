using System.Globalization;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Persistence;
using Roentgen.Simulation;

namespace Roentgen.Headless;

public sealed class HeadlessCommandConsole
{
    private readonly SimulationHost _host;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly StableId _issuer;
    private long _spawnOrdinal;

    public HeadlessCommandConsole(SimulationHost host, TextReader input, TextWriter output)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _issuer = StableIdFactory.CreateChild(StableIdFactory.CreateCampaignRoot(host.CampaignSeed), "headless-console", "issuer");
    }

    public int Run()
    {
        _output.WriteLine("Roentgen headless command console. Type 'help' for commands.");
        while (true)
        {
            _output.Write($"roentgen[{_host.Clock.Tick}]> ");
            string? line = _input.ReadLine();
            if (line is null)
            {
                return 0;
            }

            try
            {
                string[] tokens = Tokenize(line);
                if (tokens.Length == 0)
                {
                    continue;
                }

                if (!Execute(tokens))
                {
                    return 0;
                }
            }
            catch (Exception exception)
            {
                _output.WriteLine($"ERROR: {exception.Message}");
            }
        }
    }

    public bool Execute(IReadOnlyList<string> tokens)
    {
        string command = tokens[0].ToLowerInvariant();
        switch (command)
        {
            case "help":
                WriteHelp();
                break;
            case "status":
                WriteStatus();
                break;
            case "step":
                RunTicks(ParseUInt64(tokens, 1, 1UL));
                break;
            case "run":
                RunTicks(ParseUInt64(tokens, 1, 60UL));
                break;
            case "entities":
                WriteEntities();
                break;
            case "systems":
                foreach (SimulationSystemDescriptor system in _host.Pipeline.GetSchedule())
                {
                    _output.WriteLine($"{system.Phase,-22} {system.Order,6} {system.SystemId}");
                }
                break;
            case "checksum":
                _output.WriteLine(_host.ChecksumService.Compute(_host.Clock.Tick));
                break;
            case "spawn":
                Spawn(tokens);
                break;
            case "destroy":
                RequireCount(tokens, 2);
                ExecuteCommand(new DestroyEntityCommand(StableId.Parse(tokens[1])));
                break;
            case "set-global":
                RequireCount(tokens, 3);
                ExecuteCommand(new SetGlobalInt64Command(tokens[1], ParseInt64(tokens[2])));
                break;
            case "add-global":
                RequireCount(tokens, 3);
                ExecuteCommand(new AddGlobalInt64Command(tokens[1], ParseInt64(tokens[2])));
                break;
            case "set-component":
                RequireCount(tokens, 4);
                ExecuteCommand(new SetEntityInt64ComponentCommand(
                    StableId.Parse(tokens[1]),
                    tokens[2],
                    ParseInt64(tokens[3])));
                break;
            case "snapshot":
                RequireCount(tokens, 2);
                using (FileStream stream = File.Create(tokens[1]))
                {
                    SimulationSnapshotSerializer.Write(stream, _host.CaptureSnapshot());
                }
                _output.WriteLine($"Snapshot written: {Path.GetFullPath(tokens[1])}");
                break;
            case "load":
                RequireCount(tokens, 2);
                using (FileStream stream = File.OpenRead(tokens[1]))
                {
                    _host.Restore(SimulationSnapshotSerializer.Read(stream));
                }
                _output.WriteLine($"Snapshot restored at tick {_host.Clock.Tick}.");
                break;
            case "quit":
            case "exit":
                return false;
            default:
                throw new ArgumentException($"Unknown console command '{tokens[0]}'.");
        }

        return true;
    }

    public static string[] Tokenize(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(character);
            }
        }

        if (quoted)
        {
            throw new FormatException("Console command contains an unterminated quote.");
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens.ToArray();
    }

    private void Spawn(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 2)
        {
            throw new ArgumentException("Usage: spawn <archetype> [tag ...]");
        }

        long ordinal = _spawnOrdinal;
        _spawnOrdinal = checked(_spawnOrdinal + 1L);
        StableId id = StableIdFactory.CreateChild(_issuer, "spawn", ordinal);
        ExecuteCommand(new SpawnEntityCommand(id, tokens[1], tokens.Skip(2).ToArray()));
        _output.WriteLine($"Spawned {id}.");
    }

    private void ExecuteCommand(ISimulationCommand command)
    {
        _host.Enqueue(_issuer, _host.Clock.Tick + 1UL, CommandOriginPriority.Development, command);
        SimulationTickResult result = _host.Step(forceChecksum: true);
        _output.WriteLine($"Tick {result.Tick}; checksum {result.StateChecksum}.");
    }

    private void RunTicks(ulong count)
    {
        SimulationRunResult result = HeadlessExecution.Run(_host, count);
        _output.WriteLine($"Ran {result.TicksExecuted:N0} ticks to {result.EndTick:N0}; checksum {result.FinalChecksum}.");
    }

    private void WriteStatus()
    {
        _output.WriteLine($"Seed: {_host.CampaignSeed.Value}");
        _output.WriteLine($"Tick: {_host.Clock.Tick}");
        _output.WriteLine($"Entities: {_host.Entities.Count}");
        _output.WriteLine($"Pending commands: {_host.Commands.Count}");
        _output.WriteLine($"State values: {_host.StateStore.Count}");
        _output.WriteLine($"Checksum: {_host.ChecksumService.Compute(_host.Clock.Tick)}");
    }

    private void WriteEntities()
    {
        foreach (EntityState entity in _host.Entities.Entities)
        {
            _output.WriteLine($"{entity.Id}  {entity.ArchetypeId}  tags=[{string.Join(",", entity.Tags)}]  components={entity.Components.Count}");
        }
    }

    private void WriteHelp()
    {
        _output.WriteLine("status | step [ticks] | run [ticks] | entities | systems | checksum");
        _output.WriteLine("spawn <archetype> [tag ...] | destroy <stable-id>");
        _output.WriteLine("set-global <key> <int64> | add-global <key> <delta>");
        _output.WriteLine("set-component <stable-id> <type-id> <int64>");
        _output.WriteLine("snapshot <path> | load <path> | quit");
    }

    private static ulong ParseUInt64(IReadOnlyList<string> tokens, int index, ulong defaultValue)
    {
        if (tokens.Count <= index) return defaultValue;
        return ulong.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : throw new FormatException($"'{tokens[index]}' is not an unsigned integer.");
    }

    private static long ParseInt64(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : throw new FormatException($"'{value}' is not a signed 64-bit integer.");

    private static void RequireCount(IReadOnlyList<string> tokens, int count)
    {
        if (tokens.Count != count)
        {
            throw new ArgumentException($"Command '{tokens[0]}' requires {count - 1} argument(s).");
        }
    }
}
