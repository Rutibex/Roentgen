using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Roentgen.Campaign;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Persistence;

/// <summary>
/// Phase 17 campaign save envelope. Each authoritative subsystem is stored as an independently
/// checksummed section so corruption can be localized before the complete-file checksum is tested.
/// Base procedural content is still reconstructed from the campaign seed and pinned generation data.
/// </summary>
public sealed record CampaignSnapshotV17(
    CampaignSeed CampaignSeed,
    ulong SimulationTick,
    int GenerationSchemaVersion,
    string ContentFingerprint,
    byte[] ItemSnapshot,
    byte[] SocietySnapshot,
    byte[] NarrativeSnapshot,
    byte[] CampaignSnapshot)
{
    public CampaignWorldState ReadCampaignWorld() => CampaignWorldState.ReadCanonical(CampaignSnapshot);
}

public sealed record CampaignSnapshotSection(string Name, byte[] Payload, StateChecksum Checksum);

public static class CampaignSnapshotV17Serializer
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RGCAMP17");
    private static readonly string[] RequiredSections = ["items", "society", "narrative", "campaign"];
    public const int FormatVersion = 17;
    private const int MaximumSectionBytes = 128 * 1024 * 1024;
    private const int MaximumPayloadBytes = MaximumSectionBytes * 4 + 1024 * 1024;

    public static CampaignSnapshotV17 Create(
        CampaignSeed campaignSeed,
        ulong simulationTick,
        int generationSchemaVersion,
        string contentFingerprint,
        byte[] itemSnapshot,
        byte[] societySnapshot,
        byte[] narrativeSnapshot,
        CampaignWorldState campaignWorld)
    {
        ArgumentNullException.ThrowIfNull(campaignWorld);
        return new CampaignSnapshotV17(
            campaignSeed,
            simulationTick,
            generationSchemaVersion,
            contentFingerprint,
            itemSnapshot,
            societySnapshot,
            narrativeSnapshot,
            campaignWorld.ToCanonicalPayload());
    }

    public static byte[] WriteToBytes(CampaignSnapshotV17 snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.ContentFingerprint);
        CampaignSnapshotSection[] sections = CreateSections(snapshot);
        var writer = new CanonicalWriter();
        writer.WriteUInt64(snapshot.CampaignSeed.Value);
        writer.WriteUInt64(snapshot.SimulationTick);
        writer.WriteInt32(snapshot.GenerationSchemaVersion);
        writer.WriteString(snapshot.ContentFingerprint);
        writer.WriteInt32(sections.Length);
        foreach (CampaignSnapshotSection section in sections.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            writer.WriteString(section.Name);
            byte[] sectionChecksum = new byte[StateChecksum.ByteLength];
            section.Checksum.WriteBytes(sectionChecksum);
            writer.WriteBytes(sectionChecksum);
            writer.WriteBytes(section.Payload);
        }

        byte[] payload = writer.ToArray();
        if (payload.Length > MaximumPayloadBytes) throw new InvalidDataException("Phase 17 campaign snapshot exceeds the supported size limit.");
        byte[] globalChecksum = SHA256.HashData(payload);
        using var stream = new MemoryStream(Magic.Length + 8 + globalChecksum.Length + payload.Length);
        stream.Write(Magic);
        Span<byte> version = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(version, FormatVersion);
        stream.Write(version);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        stream.Write(length);
        stream.Write(globalChecksum);
        stream.Write(payload);
        return stream.ToArray();
    }

    public static CampaignSnapshotV17 ReadFromBytes(ReadOnlyMemory<byte> bytes)
    {
        int headerLength = Magic.Length + sizeof(int) + sizeof(int) + StateChecksum.ByteLength;
        if (bytes.Length < headerLength || !bytes.Span[..Magic.Length].SequenceEqual(Magic))
            throw new InvalidDataException("Phase 17 campaign snapshot magic is invalid.");

        int format = BinaryPrimitives.ReadInt32BigEndian(bytes.Span.Slice(Magic.Length, sizeof(int)));
        if (format != FormatVersion) throw new NotSupportedException($"Phase 17 campaign snapshot format {format} is not supported.");
        int payloadLength = BinaryPrimitives.ReadInt32BigEndian(bytes.Span.Slice(Magic.Length + sizeof(int), sizeof(int)));
        if (payloadLength < 0 || payloadLength > MaximumPayloadBytes || bytes.Length != headerLength + payloadLength)
            throw new InvalidDataException("Phase 17 campaign snapshot length is invalid.");

        ReadOnlyMemory<byte> expectedGlobal = bytes.Slice(Magic.Length + sizeof(int) + sizeof(int), StateChecksum.ByteLength);
        ReadOnlyMemory<byte> payload = bytes.Slice(headerLength, payloadLength);
        byte[] actualGlobal = SHA256.HashData(payload.Span);
        if (!CryptographicOperations.FixedTimeEquals(expectedGlobal.Span, actualGlobal))
            throw new InvalidDataException("Phase 17 campaign snapshot global checksum is invalid.");

        var reader = new CanonicalReader(payload);
        var seed = new CampaignSeed(reader.ReadUInt64());
        ulong tick = reader.ReadUInt64();
        int schema = reader.ReadInt32();
        string fingerprint = reader.ReadRequiredString();
        int sectionCount = reader.ReadCount();
        if (sectionCount != RequiredSections.Length) throw new InvalidDataException($"Phase 17 campaign snapshot requires {RequiredSections.Length} sections.");

        var sections = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        for (int index = 0; index < sectionCount; index++)
        {
            string name = reader.ReadRequiredString();
            if (!RequiredSections.Contains(name, StringComparer.Ordinal)) throw new InvalidDataException($"Unknown Phase 17 campaign section '{name}'.");
            if (sections.ContainsKey(name)) throw new InvalidDataException($"Duplicate Phase 17 campaign section '{name}'.");
            byte[] expectedSection = reader.ReadBytes();
            if (expectedSection.Length != StateChecksum.ByteLength) throw new InvalidDataException($"Campaign section '{name}' has an invalid checksum length.");
            byte[] sectionPayload = ReadBounded(reader, name);
            byte[] actualSection = SHA256.HashData(sectionPayload);
            if (!CryptographicOperations.FixedTimeEquals(expectedSection, actualSection))
                throw new InvalidDataException($"Campaign section '{name}' checksum is invalid.");
            sections.Add(name, sectionPayload);
        }
        reader.EnsureComplete();
        foreach (string required in RequiredSections)
            if (!sections.ContainsKey(required)) throw new InvalidDataException($"Required Phase 17 campaign section '{required}' is missing.");

        CampaignWorldState campaign = CampaignWorldState.ReadCanonical(sections["campaign"]);
        if (campaign.Plan.CampaignSeed != seed)
            throw new InvalidDataException("Campaign state seed does not match the save envelope seed.");
        if (!string.Equals(campaign.Plan.Fingerprint.ToString(), fingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Campaign state fingerprint does not match the save envelope fingerprint.");

        return new CampaignSnapshotV17(seed, tick, schema, fingerprint, sections["items"], sections["society"], sections["narrative"], sections["campaign"]);
    }

    public static void WriteAtomic(string path, CampaignSnapshotV17 snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(full) ?? throw new InvalidOperationException("Campaign snapshot path has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporary = full + ".tmp";
        string backup = full + ".bak";
        File.WriteAllBytes(temporary, WriteToBytes(snapshot));
        _ = ReadFromBytes(File.ReadAllBytes(temporary));
        if (File.Exists(full)) File.Copy(full, backup, overwrite: true);
        File.Move(temporary, full, overwrite: true);
    }

    public static CampaignSnapshotV17 Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ReadFromBytes(File.ReadAllBytes(path));
    }

    private static CampaignSnapshotSection[] CreateSections(CampaignSnapshotV17 snapshot) =>
    [
        Section("items", snapshot.ItemSnapshot),
        Section("society", snapshot.SocietySnapshot),
        Section("narrative", snapshot.NarrativeSnapshot),
        Section("campaign", snapshot.CampaignSnapshot)
    ];

    private static CampaignSnapshotSection Section(string name, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > MaximumSectionBytes) throw new InvalidDataException($"Campaign {name} section exceeds the supported limit.");
        return new CampaignSnapshotSection(name, payload, StateChecksum.FromBytes(SHA256.HashData(payload)));
    }

    private static byte[] ReadBounded(CanonicalReader reader, string name)
    {
        byte[] value = reader.ReadBytes();
        if (value.Length > MaximumSectionBytes) throw new InvalidDataException($"Campaign {name} section exceeds the supported limit.");
        return value;
    }
}
