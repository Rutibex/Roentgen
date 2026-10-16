using System.Text;
using System.Text.Json;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Simulation;

namespace Roentgen.Persistence;

public sealed record ReplayHeader(
    int FormatVersion,
    string Product,
    string ConstitutionVersion,
    CampaignSeed CampaignSeed,
    int TicksPerSecond,
    string RandomAlgorithmId,
    string BuildFingerprint);

public sealed record ReplayCheckpoint(ulong Tick, StateChecksum StateChecksum);

public sealed record ReplayLog(
    ReplayHeader Header,
    IReadOnlyList<CommandEnvelope> Commands,
    IReadOnlyList<ReplayCheckpoint> Checkpoints,
    StateChecksum FinalChainChecksum);

public sealed class ReplayLogWriter : IDisposable
{
    public const int CurrentFormatVersion = 1;
    private readonly StreamWriter _writer;
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private StateChecksum _chain;
    private long _recordCount;
    private bool _completed;

    public ReplayLogWriter(Stream stream, ReplayHeader header, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);
        if (!stream.CanWrite)
        {
            throw new ArgumentException("Replay stream must be writable.", nameof(stream));
        }

        if (header.FormatVersion != CurrentFormatVersion)
        {
            throw new NotSupportedException($"Replay format {header.FormatVersion} is not supported.");
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
        _writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { NewLine = "\n" };
        _chain = ReplayHash.ComputeHeader(header);
        WriteLine(new HeaderLine(
            "header",
            header.FormatVersion,
            header.Product,
            header.ConstitutionVersion,
            header.CampaignSeed.Value,
            header.TicksPerSecond,
            header.RandomAlgorithmId,
            header.BuildFingerprint,
            _chain.ToString()));
    }

    public void AppendCommand(CommandEnvelope command)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(command);
        if (!CommandEnvelopeFactory.IsValid(command))
        {
            throw new InvalidDataException($"Command '{command.CommandId}' failed deterministic envelope validation.");
        }

        StateChecksum next = ReplayHash.ComputeCommand(_chain, command);
        WriteLine(new CommandLine(
            "command",
            command.CommandId.ToString(),
            command.IssuerId.ToString(),
            command.TargetTick,
            (int)command.OriginPriority,
            command.Sequence,
            command.TypeId,
            command.SchemaVersion,
            Convert.ToBase64String(command.Payload),
            command.PayloadChecksum.ToString(),
            _chain.ToString(),
            next.ToString()));
        _chain = next;
        _recordCount++;
    }

    public void AppendCheckpoint(ReplayCheckpoint checkpoint)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(checkpoint);
        StateChecksum next = ReplayHash.ComputeCheckpoint(_chain, checkpoint);
        WriteLine(new CheckpointLine(
            "checkpoint",
            checkpoint.Tick,
            checkpoint.StateChecksum.ToString(),
            _chain.ToString(),
            next.ToString()));
        _chain = next;
        _recordCount++;
    }

    public StateChecksum Complete()
    {
        EnsureOpen();
        WriteLine(new FooterLine("footer", _recordCount, _chain.ToString()));
        _writer.Flush();
        _completed = true;
        return _chain;
    }

    public void Dispose()
    {
        _writer.Dispose();
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private void EnsureOpen()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The replay log has already been completed.");
        }
    }

    private void WriteLine<T>(T value)
    {
        _writer.WriteLine(JsonSerializer.Serialize(value, ReplayJson.Options));
    }

    private sealed record HeaderLine(
        string RecordType,
        int FormatVersion,
        string Product,
        string ConstitutionVersion,
        ulong CampaignSeed,
        int TicksPerSecond,
        string RandomAlgorithmId,
        string BuildFingerprint,
        string HeaderChecksum);

    private sealed record CommandLine(
        string RecordType,
        string CommandId,
        string IssuerId,
        ulong TargetTick,
        int OriginPriority,
        ulong Sequence,
        string TypeId,
        int SchemaVersion,
        string Payload,
        string PayloadChecksum,
        string PreviousChecksum,
        string RecordChecksum);

    private sealed record CheckpointLine(
        string RecordType,
        ulong Tick,
        string StateChecksum,
        string PreviousChecksum,
        string RecordChecksum);

    private sealed record FooterLine(string RecordType, long RecordCount, string FinalChecksum);
}

public static class ReplayLogReader
{
    public static ReplayLog Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Replay stream must be readable.", nameof(stream));
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
        string? headerText = reader.ReadLine();
        if (headerText is null)
        {
            throw new InvalidDataException("Replay log is empty.");
        }

        using JsonDocument headerDocument = JsonDocument.Parse(headerText);
        JsonElement headerRoot = headerDocument.RootElement;
        RequireRecordType(headerRoot, "header");
        var header = new ReplayHeader(
            headerRoot.GetProperty("FormatVersion").GetInt32(),
            headerRoot.GetProperty("Product").GetString()!,
            headerRoot.GetProperty("ConstitutionVersion").GetString()!,
            new CampaignSeed(headerRoot.GetProperty("CampaignSeed").GetUInt64()),
            headerRoot.GetProperty("TicksPerSecond").GetInt32(),
            headerRoot.GetProperty("RandomAlgorithmId").GetString()!,
            headerRoot.GetProperty("BuildFingerprint").GetString()!);
        if (header.FormatVersion != ReplayLogWriter.CurrentFormatVersion)
        {
            throw new NotSupportedException($"Replay format {header.FormatVersion} is not supported.");
        }

        StateChecksum chain = ReplayHash.ComputeHeader(header);
        StateChecksum declaredHeader = StateChecksum.Parse(headerRoot.GetProperty("HeaderChecksum").GetString()!);
        if (chain != declaredHeader)
        {
            throw new InvalidDataException("Replay header checksum is invalid.");
        }

        var commands = new List<CommandEnvelope>();
        var checkpoints = new List<ReplayCheckpoint>();
        long recordCount = 0;
        bool footerFound = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            string recordType = root.GetProperty("RecordType").GetString()!;
            if (recordType == "command")
            {
                VerifyPrevious(root, chain);
                byte[] payload = Convert.FromBase64String(root.GetProperty("Payload").GetString()!);
                var command = new CommandEnvelope(
                    StableId.Parse(root.GetProperty("CommandId").GetString()!),
                    StableId.Parse(root.GetProperty("IssuerId").GetString()!),
                    root.GetProperty("TargetTick").GetUInt64(),
                    (CommandOriginPriority)root.GetProperty("OriginPriority").GetInt32(),
                    root.GetProperty("Sequence").GetUInt64(),
                    root.GetProperty("TypeId").GetString()!,
                    root.GetProperty("SchemaVersion").GetInt32(),
                    payload,
                    StateChecksum.Parse(root.GetProperty("PayloadChecksum").GetString()!));
                if (!CommandEnvelopeFactory.IsValid(command))
                {
                    throw new InvalidDataException($"Deterministic envelope validation failed for command '{command.CommandId}'.");
                }

                StateChecksum next = ReplayHash.ComputeCommand(chain, command);
                VerifyRecord(root, next);
                chain = next;
                commands.Add(command);
                recordCount++;
            }
            else if (recordType == "checkpoint")
            {
                VerifyPrevious(root, chain);
                var checkpoint = new ReplayCheckpoint(
                    root.GetProperty("Tick").GetUInt64(),
                    StateChecksum.Parse(root.GetProperty("StateChecksum").GetString()!));
                StateChecksum next = ReplayHash.ComputeCheckpoint(chain, checkpoint);
                VerifyRecord(root, next);
                chain = next;
                checkpoints.Add(checkpoint);
                recordCount++;
            }
            else if (recordType == "footer")
            {
                long declaredCount = root.GetProperty("RecordCount").GetInt64();
                StateChecksum final = StateChecksum.Parse(root.GetProperty("FinalChecksum").GetString()!);
                if (declaredCount != recordCount || final != chain)
                {
                    throw new InvalidDataException("Replay footer does not match the verified record chain.");
                }

                footerFound = true;
                if (reader.ReadLine() is not null)
                {
                    throw new InvalidDataException("Replay log contains data after its footer.");
                }

                break;
            }
            else
            {
                throw new InvalidDataException($"Unknown replay record type '{recordType}'.");
            }
        }

        if (!footerFound)
        {
            throw new InvalidDataException("Replay footer is missing.");
        }

        return new ReplayLog(header, commands, checkpoints, chain);
    }

    private static void RequireRecordType(JsonElement root, string expected)
    {
        string actual = root.GetProperty("RecordType").GetString()!;
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected replay record '{expected}', found '{actual}'.");
        }
    }

    private static void VerifyPrevious(JsonElement root, StateChecksum expected)
    {
        StateChecksum actual = StateChecksum.Parse(root.GetProperty("PreviousChecksum").GetString()!);
        if (actual != expected)
        {
            throw new InvalidDataException("Replay checksum chain is broken.");
        }
    }

    private static void VerifyRecord(JsonElement root, StateChecksum expected)
    {
        StateChecksum actual = StateChecksum.Parse(root.GetProperty("RecordChecksum").GetString()!);
        if (actual != expected)
        {
            throw new InvalidDataException("Replay record checksum is invalid.");
        }
    }

}

internal static class ReplayHash
{
    public static StateChecksum ComputeHeader(ReplayHeader header)
    {
        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.replay.header");
        writer.WriteInt32(header.FormatVersion);
        writer.WriteString(header.Product);
        writer.WriteString(header.ConstitutionVersion);
        writer.WriteUInt64(header.CampaignSeed.Value);
        writer.WriteInt32(header.TicksPerSecond);
        writer.WriteString(header.RandomAlgorithmId);
        writer.WriteString(header.BuildFingerprint);
        return writer.ComputeChecksum();
    }

    public static StateChecksum ComputeCommand(StateChecksum previous, CommandEnvelope command)
    {
        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.replay.command");
        writer.WriteChecksum(previous);
        writer.WriteStableId(command.CommandId);
        writer.WriteStableId(command.IssuerId);
        writer.WriteUInt64(command.TargetTick);
        writer.WriteInt32((int)command.OriginPriority);
        writer.WriteUInt64(command.Sequence);
        writer.WriteString(command.TypeId);
        writer.WriteInt32(command.SchemaVersion);
        writer.WriteBytes(command.Payload);
        writer.WriteChecksum(command.PayloadChecksum);
        return writer.ComputeChecksum();
    }

    public static StateChecksum ComputeCheckpoint(StateChecksum previous, ReplayCheckpoint checkpoint)
    {
        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.replay.checkpoint");
        writer.WriteChecksum(previous);
        writer.WriteUInt64(checkpoint.Tick);
        writer.WriteChecksum(checkpoint.StateChecksum);
        return writer.ComputeChecksum();
    }
}

internal static class ReplayJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };
}
