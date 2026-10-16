using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roentgen.Music;

public sealed record MusicSettings
{
    public bool Enabled { get; init; } = true;
    public float Volume { get; init; } = 0.8f;
    public string SoundFontRelativePath { get; init; } = "content/music/soundfonts/default.sf2";
    public string ExpectedSoundFontSha256 { get; init; } = "9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe";
    public string CatalogRelativePath { get; init; } = "content/music/catalogs/generaluser-gs-roentgen.catalog.json";
    public int SampleRate { get; init; } = 48_000;
    public int RenderBlockFrames { get; init; } = 512;
    public int CommandCapacity { get; init; } = 128;
    public int PreparedCueCacheSize { get; init; } = 24;
    public float DefaultCrossfadeSeconds { get; init; } = 1.5f;
    public float CombatEntryThreshold { get; init; } = 0.22f;
    public float FullCombatThreshold { get; init; } = 0.58f;
    public float EliteCombatThreshold { get; init; } = 0.86f;
    public float CombatExitThreshold { get; init; } = 0.08f;
    public int MinimumSituationDwellTicks { get; init; } = 180;
    public int TransitionCooldownTicks { get; init; } = 120;
    public bool ContinueInPauseMenu { get; init; } = true;
    public bool PauseMusicalTimeInPauseMenu { get; init; }
    public bool ReducePercussionInDialogue { get; init; } = true;
    public bool DiagnosticFocusTreatment { get; init; } = true;
    public MusicDynamicRange DynamicRange { get; init; } = MusicDynamicRange.Standard;
    public string DeveloperSoundFontOverride { get; init; } = string.Empty;

    public MusicSettings Validate()
    {
        if (!float.IsFinite(Volume) || Volume is < 0f or > 2f) throw new InvalidDataException("Music volume must be between zero and two.");
        if (SampleRate != 48_000) throw new InvalidDataException("Phase 19 music must render at 48 kHz.");
        if (RenderBlockFrames is < 64 or > 4096) throw new InvalidDataException("Music render block size is invalid.");
        if (CommandCapacity is < 16 or > 4096) throw new InvalidDataException("Music command capacity is invalid.");
        if (PreparedCueCacheSize is < 1 or > 512) throw new InvalidDataException("Prepared cue cache size is invalid.");
        if (!float.IsFinite(DefaultCrossfadeSeconds) || DefaultCrossfadeSeconds is < 0f or > 20f) throw new InvalidDataException("Default music crossfade must be between zero and twenty seconds.");
        if (!float.IsFinite(CombatExitThreshold) || !float.IsFinite(CombatEntryThreshold) || !float.IsFinite(FullCombatThreshold) || !float.IsFinite(EliteCombatThreshold)) throw new InvalidDataException("Music combat thresholds must be finite.");
        if (CombatExitThreshold is < 0f or > 1f || CombatEntryThreshold is < 0f or > 1f || FullCombatThreshold is < 0f or > 1f || EliteCombatThreshold is < 0f or > 1f) throw new InvalidDataException("Music combat thresholds must be between zero and one.");
        if (!(CombatExitThreshold <= CombatEntryThreshold && CombatEntryThreshold <= FullCombatThreshold && FullCombatThreshold <= EliteCombatThreshold)) throw new InvalidDataException("Music combat thresholds must be ordered exit, entry, full, elite.");
        if (MinimumSituationDwellTicks < 0 || TransitionCooldownTicks < 0) throw new InvalidDataException("Music transition timing values cannot be negative.");
        if (string.IsNullOrWhiteSpace(SoundFontRelativePath) || string.IsNullOrWhiteSpace(CatalogRelativePath)) throw new InvalidDataException("Music asset paths are required.");
        if (ExpectedSoundFontSha256.Length != 64 || ExpectedSoundFontSha256.Any(static value => !Uri.IsHexDigit(value))) throw new InvalidDataException("Expected SoundFont SHA-256 must contain 64 hexadecimal characters.");
        return this;
    }
}

public static class MusicSettingsLoader
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static MusicSettings LoadFromFile(string path) => LoadFromJson(File.ReadAllText(path));

    public static MusicSettings LoadFromJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("music", out JsonElement music)) return new MusicSettings().Validate();
        return (music.Deserialize<MusicSettings>(Options) ?? new MusicSettings()).Validate();
    }
}
