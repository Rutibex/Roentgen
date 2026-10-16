using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Persistence;

public enum SaveSlotKind
{
    Manual,
    Quick,
    Auto,
    Transition,
    MajorChoice
}

public sealed record SaveGameMetadata(
    string SlotId,
    SaveSlotKind Kind,
    string DisplayName,
    DateTimeOffset SavedAtUtc,
    ulong SimulationTick,
    ulong CampaignSeed,
    long PlayTimeSeconds,
    string LocationName,
    string CharacterName,
    int CharacterLevel,
    string BuildVersion,
    int FormatVersion,
    bool HasThumbnail,
    bool RecoveredFromBackup = false);

public sealed record PlayerSaveState(
    StableId Id,
    FixedVector3 FeetPosition,
    FixedVector3 Velocity,
    int BodyYawUnits,
    int ViewPitchUnits,
    PlayerStance Stance,
    bool Grounded,
    StableId GroundEntityId,
    ulong Revision)
{
    public static PlayerSaveState Capture(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new PlayerSaveState(
            player.Id,
            player.FeetPosition,
            player.Velocity,
            player.BodyYawUnits,
            player.ViewPitchUnits,
            player.Stance,
            player.Grounded,
            player.GroundEntityId,
            player.Revision);
    }

    public void Apply(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Id != Id) throw new InvalidDataException("The saved player identity does not match the active campaign.");
        player.FeetPosition = FeetPosition;
        player.Velocity = Velocity;
        player.BodyYawUnits = BodyYawUnits;
        player.ViewPitchUnits = ViewPitchUnits;
        player.Stance = Stance;
        player.Grounded = Grounded;
        player.GroundEntityId = GroundEntityId;
        player.FocusedTargetId = StableId.Empty;
        player.OpenContainerId = StableId.Empty;
        player.ControlMode = PlayerControlMode.Exploration;
        player.Traversal = TraversalState.None;
        player.TraversalProgressPermille = 0;
        player.Revision = Revision;
    }
}

public sealed record Phase20SaveGame(
    SaveGameMetadata Metadata,
    PlayerSaveState Player,
    byte[] ProgressionPayload,
    byte[] PlayerWorldPayload,
    byte[] CombatPayload,
    byte[] CampaignPayload,
    byte[] MusicPayload,
    byte[] ThumbnailPng,
    IReadOnlyDictionary<string, string> GeneratorVersions);

public sealed record SaveSlotSummary(
    SaveGameMetadata Metadata,
    string Path,
    long FileSizeBytes,
    bool IsValid,
    string Status);

public sealed class SaveSlotManager
{
    private const string Extension = ".rgsave";
    private readonly string rootDirectory;

    public SaveSlotManager(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    public string RootDirectory => rootDirectory;

    public string GetPath(string slotId)
    {
        ValidateSlotId(slotId);
        return Path.Combine(rootDirectory, slotId + Extension);
    }

    public void Write(Phase20SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        string path = GetPath(save.Metadata.SlotId);
        string temporary = path + ".tmp";
        string backup = path + ".bak";
        byte[] bytes = Phase20SaveGameSerializer.Write(save);
        File.WriteAllBytes(temporary, bytes);
        _ = Phase20SaveGameSerializer.Read(File.ReadAllBytes(temporary));
        if (File.Exists(path)) File.Copy(path, backup, overwrite: true);
        File.Move(temporary, path, overwrite: true);
    }

    public Phase20SaveGame Read(string slotId, bool recoverFromBackup = true)
    {
        string path = GetPath(slotId);
        try
        {
            return Phase20SaveGameSerializer.Read(File.ReadAllBytes(path));
        }
        catch when (recoverFromBackup && File.Exists(path + ".bak"))
        {
            Phase20SaveGame recovered = Phase20SaveGameSerializer.Read(File.ReadAllBytes(path + ".bak"));
            return recovered with { Metadata = recovered.Metadata with { RecoveredFromBackup = true } };
        }
    }

    public bool TryRead(string slotId, out Phase20SaveGame? save, out string status)
    {
        try
        {
            save = Read(slotId);
            status = save.Metadata.RecoveredFromBackup ? "Recovered from backup." : "Ready.";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            save = null;
            status = exception.Message;
            return false;
        }
    }

    public IReadOnlyList<SaveSlotSummary> List()
    {
        var result = new List<SaveSlotSummary>();
        foreach (string path in Directory.EnumerateFiles(rootDirectory, "*" + Extension, SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            string slotId = Path.GetFileNameWithoutExtension(path);
            if (TryRead(slotId, out Phase20SaveGame? save, out string status) && save is not null)
            {
                result.Add(new SaveSlotSummary(save.Metadata, path, new FileInfo(path).Length, true, status));
            }
            else
            {
                DateTimeOffset timestamp = new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
                var metadata = new SaveGameMetadata(slotId, SaveSlotKind.Manual, slotId, timestamp, 0, 0, 0, "Unknown", "Unknown", 0, "Unknown", Phase20SaveGameSerializer.FormatVersion, false);
                result.Add(new SaveSlotSummary(metadata, path, new FileInfo(path).Length, false, status));
            }
        }
        return result;
    }

    public void Delete(string slotId)
    {
        string path = GetPath(slotId);
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }

    public string NextManualSlotId()
    {
        HashSet<string> existing = List().Select(static value => value.Metadata.SlotId).ToHashSet(StringComparer.Ordinal);
        for (int index = 1; index <= 999; index++)
        {
            string candidate = $"manual-{index:000}";
            if (!existing.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("The manual save-slot limit has been reached.");
    }

    public void RotateAutosaves(int maximumSlots)
    {
        maximumSlots = Math.Clamp(maximumSlots, 1, 20);
        IReadOnlyList<SaveSlotSummary> autosaves = List()
            .Where(static value => value.Metadata.Kind == SaveSlotKind.Auto)
            .OrderByDescending(static value => value.Metadata.SavedAtUtc)
            .ToArray();
        foreach (SaveSlotSummary obsolete in autosaves.Skip(maximumSlots)) Delete(obsolete.Metadata.SlotId);
    }

    private static void ValidateSlotId(string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        if (slotId.Length > 64 || slotId.Any(static value => !(char.IsAsciiLetterOrDigit(value) || value is '-' or '_')))
            throw new ArgumentException("Save slot IDs may contain only ASCII letters, numbers, hyphens, and underscores.", nameof(slotId));
    }
}

public static class Phase20SaveGameSerializer
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RGSAVE20");
    public const int FormatVersion = 20;
    private const int MaximumPayloadBytes = 64 * 1024 * 1024;
    private const int MaximumSectionBytes = 48 * 1024 * 1024;

    public static byte[] Write(Phase20SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        Validate(save);
        var writer = new CanonicalWriter();
        WriteMetadata(writer, save.Metadata);
        WritePlayer(writer, save.Player);
        WriteSection(writer, "progression", save.ProgressionPayload);
        WriteSection(writer, "player-world", save.PlayerWorldPayload);
        WriteSection(writer, "combat", save.CombatPayload);
        WriteSection(writer, "campaign", save.CampaignPayload);
        WriteSection(writer, "music", save.MusicPayload);
        WriteSection(writer, "thumbnail", save.ThumbnailPng);
        writer.WriteCount(save.GeneratorVersions.Count);
        foreach ((string name, string version) in save.GeneratorVersions.OrderBy(static value => value.Key, StringComparer.Ordinal))
        {
            writer.WriteString(name);
            writer.WriteString(version);
        }

        byte[] payload = writer.ToArray();
        if (payload.Length > MaximumPayloadBytes) throw new InvalidDataException("The Phase 20 save exceeds the supported size limit.");
        byte[] checksum = SHA256.HashData(payload);
        using var stream = new MemoryStream(Magic.Length + 8 + checksum.Length + payload.Length);
        stream.Write(Magic);
        Span<byte> integer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(integer, FormatVersion);
        stream.Write(integer);
        BinaryPrimitives.WriteInt32BigEndian(integer, payload.Length);
        stream.Write(integer);
        stream.Write(checksum);
        stream.Write(payload);
        return stream.ToArray();
    }

    public static Phase20SaveGame Read(ReadOnlyMemory<byte> bytes)
    {
        int headerLength = Magic.Length + sizeof(int) + sizeof(int) + StateChecksum.ByteLength;
        if (bytes.Length < headerLength || !bytes.Span[..Magic.Length].SequenceEqual(Magic)) throw new InvalidDataException("Phase 20 save magic is invalid.");
        int version = BinaryPrimitives.ReadInt32BigEndian(bytes.Span.Slice(Magic.Length, sizeof(int)));
        if (version != FormatVersion) throw new NotSupportedException($"Phase 20 save format {version} is not supported.");
        int payloadLength = BinaryPrimitives.ReadInt32BigEndian(bytes.Span.Slice(Magic.Length + sizeof(int), sizeof(int)));
        if (payloadLength < 0 || payloadLength > MaximumPayloadBytes || bytes.Length != headerLength + payloadLength) throw new InvalidDataException("Phase 20 save length is invalid.");
        ReadOnlySpan<byte> expected = bytes.Span.Slice(Magic.Length + sizeof(int) + sizeof(int), StateChecksum.ByteLength);
        ReadOnlyMemory<byte> payload = bytes.Slice(headerLength, payloadLength);
        byte[] actual = SHA256.HashData(payload.Span);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual)) throw new InvalidDataException("Phase 20 save checksum is invalid.");

        var reader = new CanonicalReader(payload);
        SaveGameMetadata metadata = ReadMetadata(reader);
        PlayerSaveState player = ReadPlayer(reader);
        byte[] progression = ReadSection(reader, "progression");
        byte[] playerWorld = ReadSection(reader, "player-world");
        byte[] combat = ReadSection(reader, "combat");
        byte[] campaign = ReadSection(reader, "campaign");
        byte[] music = ReadSection(reader, "music");
        byte[] thumbnail = ReadSection(reader, "thumbnail");
        int versionCount = reader.ReadCount();
        var versions = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < versionCount; index++) versions.Add(reader.ReadRequiredString(), reader.ReadRequiredString());
        reader.EnsureComplete();
        var result = new Phase20SaveGame(metadata, player, progression, playerWorld, combat, campaign, music, thumbnail, versions);
        Validate(result);
        return result;
    }

    private static void WriteMetadata(CanonicalWriter writer, SaveGameMetadata metadata)
    {
        writer.WriteString(metadata.SlotId);
        writer.WriteInt32((int)metadata.Kind);
        writer.WriteString(metadata.DisplayName);
        writer.WriteInt64(metadata.SavedAtUtc.UtcTicks);
        writer.WriteUInt64(metadata.SimulationTick);
        writer.WriteUInt64(metadata.CampaignSeed);
        writer.WriteInt64(metadata.PlayTimeSeconds);
        writer.WriteString(metadata.LocationName);
        writer.WriteString(metadata.CharacterName);
        writer.WriteInt32(metadata.CharacterLevel);
        writer.WriteString(metadata.BuildVersion);
        writer.WriteInt32(metadata.FormatVersion);
        writer.WriteBoolean(metadata.HasThumbnail);
        writer.WriteBoolean(metadata.RecoveredFromBackup);
    }

    private static SaveGameMetadata ReadMetadata(CanonicalReader reader) => new(
        reader.ReadRequiredString(),
        (SaveSlotKind)reader.ReadInt32(),
        reader.ReadRequiredString(),
        new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        reader.ReadUInt64(),
        reader.ReadUInt64(),
        reader.ReadInt64(),
        reader.ReadRequiredString(),
        reader.ReadRequiredString(),
        reader.ReadInt32(),
        reader.ReadRequiredString(),
        reader.ReadInt32(),
        reader.ReadBoolean(),
        reader.ReadBoolean());

    private static void WritePlayer(CanonicalWriter writer, PlayerSaveState player)
    {
        writer.WriteStableId(player.Id);
        WriteVector(writer, player.FeetPosition);
        WriteVector(writer, player.Velocity);
        writer.WriteInt32(player.BodyYawUnits);
        writer.WriteInt32(player.ViewPitchUnits);
        writer.WriteInt32((int)player.Stance);
        writer.WriteBoolean(player.Grounded);
        writer.WriteStableId(player.GroundEntityId);
        writer.WriteUInt64(player.Revision);
    }

    private static PlayerSaveState ReadPlayer(CanonicalReader reader) => new(
        reader.ReadStableId(),
        ReadVector(reader),
        ReadVector(reader),
        reader.ReadInt32(),
        reader.ReadInt32(),
        (PlayerStance)reader.ReadInt32(),
        reader.ReadBoolean(),
        reader.ReadStableId(),
        reader.ReadUInt64());

    private static void WriteSection(CanonicalWriter writer, string name, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > MaximumSectionBytes) throw new InvalidDataException($"Save section '{name}' exceeds the supported limit.");
        writer.WriteString(name);
        writer.WriteBytes(SHA256.HashData(payload));
        writer.WriteBytes(payload);
    }

    private static byte[] ReadSection(CanonicalReader reader, string expectedName)
    {
        string name = reader.ReadRequiredString();
        if (!string.Equals(name, expectedName, StringComparison.Ordinal)) throw new InvalidDataException($"Expected save section '{expectedName}' but found '{name}'.");
        byte[] expected = reader.ReadBytes();
        if (expected.Length != StateChecksum.ByteLength) throw new InvalidDataException($"Save section '{name}' has an invalid checksum.");
        byte[] payload = reader.ReadBytes();
        if (payload.Length > MaximumSectionBytes) throw new InvalidDataException($"Save section '{name}' exceeds the supported limit.");
        byte[] actual = SHA256.HashData(payload);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual)) throw new InvalidDataException($"Save section '{name}' is corrupt.");
        return payload;
    }

    private static void WriteVector(CanonicalWriter writer, FixedVector3 value)
    {
        writer.WriteInt64(value.XMillimeters);
        writer.WriteInt64(value.YMillimeters);
        writer.WriteInt64(value.ZMillimeters);
    }

    private static FixedVector3 ReadVector(CanonicalReader reader) => new(reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64());

    private static void Validate(Phase20SaveGame save)
    {
        if (save.Metadata.FormatVersion != FormatVersion) throw new InvalidDataException("Save metadata format version is invalid.");
        if (save.Metadata.CampaignSeed == 0) throw new InvalidDataException("Save campaign seed is invalid.");
        if (save.Player.Id.IsEmpty) throw new InvalidDataException("Save player identity is missing.");
        if (save.CampaignPayload.Length == 0) throw new InvalidDataException("Save campaign payload is missing.");
        if (save.PlayerWorldPayload.Length == 0) throw new InvalidDataException("Save player-world payload is missing.");
        if (save.CombatPayload.Length == 0) throw new InvalidDataException("Save combat payload is missing.");
        if (save.ProgressionPayload.Length == 0) throw new InvalidDataException("Save progression payload is missing.");
        if (save.Metadata.HasThumbnail != (save.ThumbnailPng.Length > 0)) throw new InvalidDataException("Save thumbnail metadata does not match its payload.");
    }
}
