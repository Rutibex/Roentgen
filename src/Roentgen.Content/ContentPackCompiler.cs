using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Roentgen.Content;

public sealed record CompiledContentPackResult(string JsonPath, string BinaryPath, string ManifestPath, string Fingerprint, long JsonBytes, long BinaryBytes);

public sealed class ContentPackCompiler
{
    public const string BinaryMagic = "RCPACK21";
    public const int BinaryVersion = 21;

    public CompiledContentPackResult Compile(string sourcePath, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        LaunchContentPack pack = ContentPackLoader.Load(sourcePath);
        ContentValidationReport report = new ContentPackValidator().Validate(pack);
        report.ThrowIfInvalid();
        Directory.CreateDirectory(outputDirectory);

        byte[] json = ContentPackLoader.SerializeCanonical(pack);
        string jsonPath = Path.Combine(outputDirectory, "roentgen-content-v21.json");
        File.WriteAllBytes(jsonPath, json);
        string binaryPath = Path.Combine(outputDirectory, "roentgen-content-v21.pack");
        WriteBinary(binaryPath, json, report.Fingerprint);
        string manifestPath = Path.Combine(outputDirectory, "roentgen-content-v21.manifest.json");
        var manifest = new
        {
            schemaVersion = BinaryVersion,
            packageId = pack.PackageId,
            contentVersion = pack.ContentVersion,
            fingerprint = report.Fingerprint,
            generatedUtc = DateTimeOffset.UtcNow,
            counts = Phase21ContentRuntime.Create(pack).Counts,
            source = Path.GetFileName(sourcePath),
            binaryMagic = BinaryMagic
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        return new CompiledContentPackResult(jsonPath, binaryPath, manifestPath, report.Fingerprint, new FileInfo(jsonPath).Length, new FileInfo(binaryPath).Length);
    }

    public LaunchContentPack ReadBinary(string binaryPath)
    {
        using var stream = File.OpenRead(binaryPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        string magic = Encoding.ASCII.GetString(reader.ReadBytes(BinaryMagic.Length));
        if (!string.Equals(magic, BinaryMagic, StringComparison.Ordinal)) throw new InvalidDataException("Not a Roentgen Phase 21 content pack.");
        int version = reader.ReadInt32();
        if (version != BinaryVersion) throw new InvalidDataException($"Unsupported content pack version {version}.");
        int fingerprintLength = reader.ReadInt32();
        string fingerprint = Encoding.ASCII.GetString(reader.ReadBytes(fingerprintLength));
        int payloadLength = reader.ReadInt32();
        byte[] payload = reader.ReadBytes(payloadLength);
        byte[] storedHash = reader.ReadBytes(32);
        byte[] actualHash = SHA256.HashData(payload);
        if (!CryptographicOperations.FixedTimeEquals(storedHash, actualHash)) throw new InvalidDataException("Compiled content payload checksum failed.");
        LaunchContentPack pack = JsonSerializer.Deserialize<LaunchContentPack>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Compiled content payload was empty.");
        string actualFingerprint = ContentPackLoader.ComputeFingerprint(pack);
        if (!string.Equals(fingerprint, actualFingerprint, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Compiled content fingerprint failed.");
        return pack;
    }

    private static void WriteBinary(string path, byte[] payload, string fingerprint)
    {
        string temporary = path + ".tmp";
        using (var stream = File.Create(temporary))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
        {
            writer.Write(Encoding.ASCII.GetBytes(BinaryMagic));
            writer.Write(BinaryVersion);
            byte[] fingerprintBytes = Encoding.ASCII.GetBytes(fingerprint);
            writer.Write(fingerprintBytes.Length);
            writer.Write(fingerprintBytes);
            writer.Write(payload.Length);
            writer.Write(payload);
            writer.Write(SHA256.HashData(payload));
            writer.Flush();
        }
        File.Move(temporary, path, overwrite: true);
    }
}
