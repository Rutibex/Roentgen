using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Simulation;

public interface ISimulationEvent
{
    string TypeId { get; }
    int SchemaVersion { get; }
    void WritePayload(CanonicalWriter writer);
}

public sealed record EventEnvelope(
    StableId EventId,
    StableId ProducerId,
    ulong Tick,
    ulong Sequence,
    string TypeId,
    int SchemaVersion,
    byte[] Payload,
    StateChecksum PayloadChecksum);

public interface IEventHandler
{
    string HandlerId { get; }
    string EventTypeId { get; }
    int Priority { get; }
    void Handle(EventEnvelope @event, SimulationTickContext context);
}

public sealed class DeterministicEventBus
{
    private readonly Dictionary<string, List<IEventHandler>> _handlers = new(StringComparer.Ordinal);
    private readonly Queue<EventEnvelope> _events = new();
    private ulong _nextSequence;

    public int PendingCount => _events.Count;
    public ulong NextSequence => _nextSequence;

    public void RestoreSequence(ulong nextSequence)
    {
        if (_events.Count != 0)
        {
            throw new InvalidOperationException("Cannot restore event sequence while events are pending.");
        }

        _nextSequence = nextSequence;
    }

    public void Register(IEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler.HandlerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler.EventTypeId);

        if (!_handlers.TryGetValue(handler.EventTypeId, out List<IEventHandler>? handlers))
        {
            handlers = [];
            _handlers.Add(handler.EventTypeId, handlers);
        }

        if (handlers.Any(existing => string.Equals(existing.HandlerId, handler.HandlerId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Event handler '{handler.HandlerId}' is already registered for '{handler.EventTypeId}'.");
        }

        handlers.Add(handler);
        handlers.Sort(EventHandlerComparer.Instance);
    }

    public EventEnvelope Publish(StableId producerId, ulong tick, ISimulationEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(@event.TypeId);
        if (@event.SchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(@event), "Event schema version must be positive.");
        }

        var payloadWriter = new CanonicalWriter();
        @event.WritePayload(payloadWriter);
        byte[] payload = payloadWriter.ToArray();
        StateChecksum checksum = payloadWriter.ComputeChecksum();
        ulong sequence = _nextSequence;
        _nextSequence = checked(_nextSequence + 1UL);

        var idWriter = new CanonicalWriter();
        idWriter.WriteString("roentgen.event");
        idWriter.WriteStableId(producerId);
        idWriter.WriteUInt64(tick);
        idWriter.WriteUInt64(sequence);
        idWriter.WriteString(@event.TypeId);
        idWriter.WriteInt32(@event.SchemaVersion);
        idWriter.WriteChecksum(checksum);
        StateChecksum idHash = idWriter.ComputeChecksum();

        var envelope = new EventEnvelope(
            new StableId(idHash.A, idHash.B),
            producerId,
            tick,
            sequence,
            @event.TypeId,
            @event.SchemaVersion,
            payload,
            checksum);
        _events.Enqueue(envelope);
        return envelope;
    }

    public int Flush(SimulationTickContext context, int maximumEvents = 100_000)
    {
        if (maximumEvents <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEvents));
        }

        int dispatched = 0;
        while (_events.TryDequeue(out EventEnvelope envelope))
        {
            if (++dispatched > maximumEvents)
            {
                throw new InvalidOperationException("Event dispatch limit exceeded; a recursive event loop is likely.");
            }

            if (!_handlers.TryGetValue(envelope.TypeId, out List<IEventHandler>? handlers))
            {
                continue;
            }

            foreach (IEventHandler handler in handlers)
            {
                handler.Handle(envelope, context);
            }
        }

        return dispatched;
    }

    private sealed class EventHandlerComparer : IComparer<IEventHandler>
    {
        public static EventHandlerComparer Instance { get; } = new();

        public int Compare(IEventHandler? left, IEventHandler? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            int result = left.Priority.CompareTo(right.Priority);
            return result != 0 ? result : string.CompareOrdinal(left.HandlerId, right.HandlerId);
        }
    }
}
