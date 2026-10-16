using Roentgen.Contracts;

namespace Roentgen.Simulation;

public sealed class DeterministicCommandQueue
{
    private readonly SortedSet<CommandEnvelope> _commands = new(CommandEnvelopeCanonicalComparer.Instance);
    private readonly HashSet<StableId> _commandIds = [];

    public int Count => _commands.Count;

    public IReadOnlyList<CommandEnvelope> CaptureSnapshot() => _commands.ToArray();

    public void Restore(IEnumerable<CommandEnvelope> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands.Clear();
        _commandIds.Clear();
        foreach (CommandEnvelope command in commands)
        {
            Enqueue(command);
        }
    }

    public void Enqueue(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!CommandEnvelopeFactory.IsValid(command))
        {
            throw new InvalidDataException($"Command '{command.CommandId}' failed deterministic envelope validation.");
        }

        if (!_commandIds.Add(command.CommandId))
        {
            throw new InvalidOperationException($"Command '{command.CommandId}' has already been queued.");
        }

        if (!_commands.Add(command))
        {
            _commandIds.Remove(command.CommandId);
            throw new InvalidOperationException("A command with the same deterministic ordering key is already queued.");
        }
    }

    public IReadOnlyList<CommandEnvelope> DrainTick(ulong tick)
    {
        if (_commands.Count > 0 && _commands.Min!.TargetTick < tick)
        {
            throw new InvalidOperationException($"A command targeting tick {_commands.Min.TargetTick} was not processed before tick {tick}.");
        }

        var result = new List<CommandEnvelope>();
        while (_commands.Count > 0)
        {
            CommandEnvelope next = _commands.Min!;
            if (next.TargetTick != tick)
            {
                break;
            }

            _commands.Remove(next);
            _commandIds.Remove(next.CommandId);
            result.Add(next);
        }

        return result;
    }


}
