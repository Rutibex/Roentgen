using System.Security.Cryptography;
using Roentgen.Core;

namespace Roentgen.Simulation;

public sealed class SimulationStateValue : ICanonicalWritable
{
    private readonly byte[] _payload;

    private SimulationStateValue(string key, int schemaVersion, byte[] payload, StateChecksum payloadChecksum)
    {
        Key = key;
        SchemaVersion = schemaVersion;
        _payload = payload;
        PayloadChecksum = payloadChecksum;
    }

    public string Key { get; }
    public int SchemaVersion { get; }
    public ReadOnlyMemory<byte> Payload => _payload;
    public StateChecksum PayloadChecksum { get; }

    public static SimulationStateValue Create(string key, int schemaVersion, Action<CanonicalWriter> writePayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(writePayload);
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        var writer = new CanonicalWriter();
        writePayload(writer);
        return FromPayload(key, schemaVersion, writer.ToArray());
    }

    public static SimulationStateValue FromPayload(string key, int schemaVersion, ReadOnlySpan<byte> payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        byte[] copy = payload.ToArray();
        return new SimulationStateValue(key, schemaVersion, copy, StateChecksum.FromBytes(SHA256.HashData(copy)));
    }

    public static SimulationStateValue Restore(
        string key,
        int schemaVersion,
        ReadOnlySpan<byte> payload,
        StateChecksum expectedChecksum)
    {
        SimulationStateValue value = FromPayload(key, schemaVersion, payload);
        if (value.PayloadChecksum != expectedChecksum)
        {
            throw new InvalidDataException($"State value '{key}' payload checksum is invalid.");
        }

        return value;
    }

    public CanonicalReader OpenReader() => new(_payload);

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Key);
        writer.WriteInt32(SchemaVersion);
        writer.WriteBytes(_payload);
        writer.WriteChecksum(PayloadChecksum);
    }
}

public sealed record SimulationStateValueSnapshot(
    string Key,
    int SchemaVersion,
    byte[] Payload,
    StateChecksum PayloadChecksum);

public sealed record SimulationStateStoreSnapshot(
    ulong MutationVersion,
    IReadOnlyList<SimulationStateValueSnapshot> Values);

public sealed class SimulationStateStore : ICanonicalWritable, IStateChecksumContributor
{
    private readonly SortedDictionary<string, SimulationStateValue> _values = new(StringComparer.Ordinal);

    public string ContributorId => "simulation.state-store";
    public int Count => _values.Count;
    public ulong MutationVersion { get; private set; }
    public IEnumerable<SimulationStateValue> Values => _values.Values;

    public void Set(SimulationStateValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _values[value.Key] = value;
        MutationVersion = checked(MutationVersion + 1UL);
    }

    public void SetInt64(string key, long value) => Set(SimulationStateValue.Create(key, 1, writer => writer.WriteInt64(value)));

    public long GetInt64(string key, long defaultValue = 0)
    {
        if (!_values.TryGetValue(key, out SimulationStateValue? value))
        {
            return defaultValue;
        }

        CanonicalReader reader = value.OpenReader();
        long result = reader.ReadInt64();
        reader.EnsureComplete();
        return result;
    }

    public bool TryGet(string key, out SimulationStateValue? value) => _values.TryGetValue(key, out value);

    public bool Remove(string key)
    {
        bool removed = _values.Remove(key);
        if (removed)
        {
            MutationVersion = checked(MutationVersion + 1UL);
        }

        return removed;
    }

    public SimulationStateStoreSnapshot CaptureSnapshot() => new(
        MutationVersion,
        _values.Values.Select(value => new SimulationStateValueSnapshot(
            value.Key,
            value.SchemaVersion,
            value.Payload.ToArray(),
            value.PayloadChecksum)).ToArray());

    public void Restore(SimulationStateStoreSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _values.Clear();
        foreach (SimulationStateValueSnapshot value in snapshot.Values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (_values.ContainsKey(value.Key))
            {
                throw new InvalidDataException($"State snapshot contains duplicate key '{value.Key}'.");
            }

            _values.Add(value.Key, SimulationStateValue.Restore(
                value.Key,
                value.SchemaVersion,
                value.Payload,
                value.PayloadChecksum));
        }

        MutationVersion = snapshot.MutationVersion;
    }

    public void WriteState(CanonicalWriter writer) => WriteCanonical(writer);

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString("roentgen.simulation-state-store");
        writer.WriteInt32(1);
        writer.WriteUInt64(MutationVersion);
        writer.WriteCount(_values.Count);
        foreach (SimulationStateValue value in _values.Values)
        {
            value.WriteCanonical(writer);
        }
    }
}
