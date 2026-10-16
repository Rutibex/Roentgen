using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Roentgen.Content;

public static class ContentPackLoader
{
    public const string DefaultRelativePath = "content/roentgen/phase21-launch-content.json";
    public const string DefaultChecksumRelativePath = "content/roentgen/phase21-launch-content.sha256";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static LaunchContentPack LoadDefault()
    {
        string path = ResolveDefaultPath();
        string checksum = Path.Combine(Path.GetDirectoryName(path)!, "phase21-launch-content.sha256");
        return Load(path, File.Exists(checksum) ? checksum : null);
    }

    public static LaunchContentPack Load(string path, string? checksumPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] bytes = File.ReadAllBytes(path);
        if (!string.IsNullOrWhiteSpace(checksumPath))
        {
            VerifyChecksum(bytes, checksumPath);
        }

        LaunchContentPack pack = JsonSerializer.Deserialize<LaunchContentPack>(bytes, Options)
            ?? throw new InvalidDataException($"Content pack '{path}' did not contain a valid root object.");
        return pack;
    }

    public static byte[] SerializeCanonical(LaunchContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        LaunchContentPack normalized = ContentPackNormalizer.Normalize(pack);
        return JsonSerializer.SerializeToUtf8Bytes(normalized, Options);
    }

    public static string ComputeFingerprint(LaunchContentPack pack) => Convert.ToHexStringLower(SHA256.HashData(SerializeCanonical(pack)));

    public static string ResolveDefaultPath()
    {
        string[] starts = [AppContext.BaseDirectory, Directory.GetCurrentDirectory()];
        foreach (string start in starts.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            DirectoryInfo? cursor = new(start);
            for (int depth = 0; depth < 8 && cursor is not null; depth++, cursor = cursor.Parent)
            {
                string candidate = Path.Combine(cursor.FullName, DefaultRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate {DefaultRelativePath} from the application base or current directory.");
    }

    private static void VerifyChecksum(byte[] bytes, string checksumPath)
    {
        string expected = File.ReadLines(checksumPath)
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => line.Length > 0)?
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?? throw new InvalidDataException($"Checksum sidecar '{checksumPath}' is empty.");
        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Content pack checksum mismatch. Expected {expected}; actual {actual}.");
        }
    }
}

internal static class ContentPackNormalizer
{
    public static LaunchContentPack Normalize(LaunchContentPack source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        PackageId = source.PackageId,
        ContentVersion = source.ContentVersion,
        Regions = source.Regions.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        SettlementGrammars = source.SettlementGrammars.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        StructureGrammars = source.StructureGrammars.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        RoomMotifs = source.RoomMotifs.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        DamageProfiles = source.DamageProfiles.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        Factions = source.Factions.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        Organizations = source.Organizations.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        HumanoidArchetypes = source.HumanoidArchetypes.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        CreatureFamilies = source.CreatureFamilies.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        RobotChassis = source.RobotChassis.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        CompanionArchetypes = source.CompanionArchetypes.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        WeaponFamilies = source.WeaponFamilies.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        WeaponComponents = source.WeaponComponents.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        ArmorFamilies = source.ArmorFamilies.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        ArmorComponents = source.ArmorComponents.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        Consumables = source.Consumables.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        CraftingProcesses = source.CraftingProcesses.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        Perks = source.Perks.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        Backgrounds = source.Backgrounds.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        Traits = source.Traits.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        QuestKernels = source.QuestKernels.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        CampaignKernels = source.CampaignKernels.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        DialogueActs = source.DialogueActs.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        RumorTemplates = source.RumorTemplates.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        Tutorials = source.Tutorials.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        EpilogueRules = source.EpilogueRules.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        AudioProfiles = source.AudioProfiles.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList(),
        MusicBindings = source.MusicBindings.OrderBy(static value => value.Id, StringComparer.Ordinal).ToList()
    };
}
