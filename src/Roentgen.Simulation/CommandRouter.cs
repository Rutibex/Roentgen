namespace Roentgen.Simulation;

public sealed class CommandRouter
{
    private readonly Dictionary<string, ICommandHandler> _handlers = new(StringComparer.Ordinal);

    public void Register(ICommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler.HandlerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler.CommandTypeId);

        if (handler.MinimumSchemaVersion <= 0 || handler.MaximumSchemaVersion < handler.MinimumSchemaVersion)
        {
            throw new ArgumentException("Command handler schema range is invalid.", nameof(handler));
        }

        if (!_handlers.TryAdd(handler.CommandTypeId, handler))
        {
            throw new InvalidOperationException($"A handler for command type '{handler.CommandTypeId}' is already registered.");
        }
    }

    public void Dispatch(CommandEnvelope command, SimulationTickContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        if (!_handlers.TryGetValue(command.TypeId, out ICommandHandler? handler))
        {
            throw new InvalidOperationException($"No command handler is registered for '{command.TypeId}'.");
        }

        if (command.SchemaVersion < handler.MinimumSchemaVersion || command.SchemaVersion > handler.MaximumSchemaVersion)
        {
            throw new NotSupportedException(
                $"Command '{command.TypeId}' schema {command.SchemaVersion} is outside handler '{handler.HandlerId}' range " +
                $"{handler.MinimumSchemaVersion}..{handler.MaximumSchemaVersion}.");
        }

        handler.Handle(command, context);
    }
}
