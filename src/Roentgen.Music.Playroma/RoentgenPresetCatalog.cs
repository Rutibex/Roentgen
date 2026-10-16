using System.Text.Json;
using PlayromaMusic.Core.Orchestration;

namespace Roentgen.Music.Playroma;

public sealed class RoentgenPresetCatalog
{
    private sealed record CatalogDocument(int SchemaVersion, string SoundFontId, string SoundFontName, string Sha256, int PresetCount, List<CatalogPreset> Presets);
    private sealed record CatalogPreset(string Key, int Bank, int Program, string Name, string Family, bool IsDrumKit, bool ExcludedByDefault, int SuggestedLowNote, int SuggestedHighNote, List<string> Tags);

    public static InstrumentCatalogDocument Load(MusicSettings settings, string baseDirectory, RoentgenSoundFontAsset asset)
    {
        string path = Path.Combine(baseDirectory, settings.CatalogRelativePath.Replace('/', Path.DirectorySeparatorChar));
        CatalogDocument document = JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The Roentgen SoundFont catalog was empty.");
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported SoundFont catalog schema.");
        if (!document.Sha256.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SoundFont catalog fingerprint does not match the bundled bank.");
        var result = new InstrumentCatalogDocument
        {
            SoundFontId = document.SoundFontId,
            SoundFontName = document.SoundFontName,
            SourcePath = asset.FullPath,
            SourceFingerprint = asset.Sha256,
            ScannedAtUtc = DateTimeOffset.UnixEpoch,
            IsComplete = document.Presets.Count == document.PresetCount
        };
        foreach (CatalogPreset preset in document.Presets.OrderBy(static value => value.Bank).ThenBy(static value => value.Program).ThenBy(static value => value.Name, StringComparer.Ordinal))
        {
            _ = Enum.TryParse(preset.Family, true, out InstrumentFamily family);
            result.Presets.Add(new InstrumentPresetDescriptor
            {
                Key = preset.Key,
                SoundFontId = document.SoundFontId,
                SoundFontName = document.SoundFontName,
                Bank = preset.Bank,
                Program = preset.Program,
                Name = preset.Name,
                Family = family,
                Tags = preset.Tags,
                SuggestedLowNote = preset.SuggestedLowNote,
                SuggestedHighNote = preset.SuggestedHighNote,
                IsDrumKit = preset.IsDrumKit,
                IsExcluded = preset.ExcludedByDefault,
                IsEnabled = !preset.ExcludedByDefault,
                Timbre = EstimateTimbre(family, preset.Name)
            });
        }
        result.Diagnostics.Add($"Loaded {result.Presets.Count} reviewed GeneralUser GS presets; {result.EnabledPresetCount} are enabled for automatic scoring.");
        return result;
    }

    private static TimbreProfile EstimateTimbre(InstrumentFamily family, string name)
    {
        bool slow = name.Contains("Slow", StringComparison.OrdinalIgnoreCase) || family is InstrumentFamily.Strings or InstrumentFamily.Ensemble or InstrumentFamily.SynthPad;
        bool bright = name.Contains("Bright", StringComparison.OrdinalIgnoreCase) || family is InstrumentFamily.Brass or InstrumentFamily.SynthLead or InstrumentFamily.ChromaticPercussion;
        bool percussive = family is InstrumentFamily.Piano or InstrumentFamily.ChromaticPercussion or InstrumentFamily.Percussive or InstrumentFamily.Drums;
        return new TimbreProfile
        {
            Attack = slow ? 0.18 : percussive ? 0.82 : 0.52,
            Sustain = slow ? 0.88 : percussive ? 0.28 : 0.62,
            Brightness = bright ? 0.82 : 0.48,
            Density = family is InstrumentFamily.Ensemble or InstrumentFamily.SynthPad ? 0.82 : 0.48,
            Percussiveness = percussive ? 0.88 : 0.22,
            PitchStability = family == InstrumentFamily.SynthEffects ? 0.52 : 0.92,
            Confidence = 0.7
        };
    }
}
