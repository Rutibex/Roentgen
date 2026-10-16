using PlayromaMusic.Audio.Scheduling;
using PlayromaMusic.Core.Model;
using PlayromaMusic.Core.Orchestration;
using PlayromaMusic.Core.Persistence;
using PlayromaMusic.Generators.Structured;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Music.Playroma;

public sealed class RoentgenCueGenerator : IMusicCueProvider
{
    public const string GeneratorVersion = "roentgen-playroma-adaptive-cue-v19-buildfix18";
    private readonly InstrumentCatalogDocument _catalog;
    private readonly RoentgenSoundFontAsset _font;
    private readonly MusicChannelAllocator _allocator = new();
    private readonly StructuredCompositionService _composer = new();
    private readonly int _sampleRate;
    private static readonly StableId GlassBasinRegion = StableIdFactory.Create("music-region", "glass-basin");
    private static readonly StableId OssuaryFieldsRegion = StableIdFactory.Create("music-region", "ossuary-fields");
    private static readonly StableId BlackwaterWorksRegion = StableIdFactory.Create("music-region", "blackwater-works");
    private static readonly StableId BrokenMeridianRegion = StableIdFactory.Create("music-region", "broken-meridian");
    private static readonly StableId RedQuarryRegion = StableIdFactory.Create("music-region", "red-quarry-country");
    private static readonly StableId QuietRangeRegion = StableIdFactory.Create("music-region", "quiet-range");

    public RoentgenCueGenerator(InstrumentCatalogDocument catalog, RoentgenSoundFontAsset font, int sampleRate)
    {
        _catalog = catalog;
        _font = font;
        _sampleRate = sampleRate;
    }

    public MusicCuePlan PrepareCue(MusicSituation situation, MusicContext context, ulong seed) => PrepareDocument(situation, context, seed).Plan;

    public AdaptiveCueDocument PrepareDocument(MusicSituation situation, MusicContext context, ulong seed)
    {
        string style = StyleFor(situation, context);
        ProjectDocument project = _composer.CreateProject(seed, style);
        project.Title = $"Roentgen {situation} {seed:x8}";
        project.Song.Title = project.Title;
        project.Audio.SoundFontPath = _font.FullPath;
        project.Audio.SampleRate = _sampleRate;
        project.Orchestration.Catalog = CloneCatalog(_catalog);
        project.Orchestration.Randomization.MaximumEnsembleSize = Math.Min(8, project.Orchestration.Randomization.MaximumEnsembleSize);
        project.Orchestration.Randomization.PermitLayering = true;
        _composer.RebuildFromCurrentPlans(project, replanOrchestration: true);
        MusicChannelAllocationResult allocation = _allocator.Allocate(project.Song);
        if (!allocation.Success && project.Song.Tracks.Count(static value => !value.Muted) < 2)
            throw new InvalidDataException("Generated cue could not be repaired into the available MIDI channel budget.");

        SongPlaybackSnapshot playback = SongPlaybackSnapshot.Create(project.Song, _sampleRate, 0.5);
        (long loopStartTick, long loopEndTick) = SelectLoop(project.Song);
        long loopStart = playback.TickToSample(loopStartTick);
        long loopEnd = Math.Max(loopStart + _sampleRate, playback.TickToSample(loopEndTick));
        string fingerprint = ProjectFingerprint.Compute(project);
        MusicCueIdentity identity = new($"cue-{situation.ToString().ToLowerInvariant()}", seed, style, GeneratorVersion, _font.Sha256);
        MusicHarmonicProfile harmony = BuildHarmony(project.Song);
        List<MusicStemDefinition> stems = BuildStems(project.Song);
        var loop = new MusicLoopRegion("primary", loopStartTick, loopEndTick, true);
        long barTicks = BarTicks(project.Song);
        var entries = new List<MusicTransitionPoint>
        {
            new("start", 0, MusicTransitionQuantization.Immediate, true, false),
            new("loop-entry", loopStartTick, MusicTransitionQuantization.NextPhrase, true, false)
        };
        var exits = project.Song.Sections.Select(section => new MusicTransitionPoint("exit-" + section.Id, section.EndTick, MusicTransitionQuantization.EndOfSection, false, true)).ToList();
        for (long tick = barTicks; tick < project.Song.LengthTicks; tick += barTicks) exits.Add(new MusicTransitionPoint("bar-" + tick, tick, MusicTransitionQuantization.NextBar, false, true));
        MusicCuePlan plan = new(identity, project.Title, situation, [loop], entries, exits, stems, harmony, IntensityFor(situation), fingerprint);
        Dictionary<int, MusicStemKind> channelStems = project.Song.Tracks.Where(static track => !track.Muted).ToDictionary(static track => track.MidiChannel, static track => StemFor(track.Role));
        bool looping = situation is not (MusicSituation.Victory or MusicSituation.Revelation or MusicSituation.Tragedy or MusicSituation.Ending or MusicSituation.Credits);
        return new AdaptiveCueDocument(plan, project, playback, channelStems, loopStart, loopEnd, looping);
    }

    public static string StyleFor(MusicSituation situation, MusicContext context) => situation switch
    {
        MusicSituation.Title => "roentgen-title",
        MusicSituation.CharacterCreation => "modal-noir",
        MusicSituation.WastelandExploration => ExplorationStyle(context),
        MusicSituation.InteriorExploration => context.LocationKind == MusicLocationKind.Medical ? "roentgen-medical-horror" : "roentgen-industrial-ruin",
        MusicSituation.SafeSettlement => "roentgen-safe-settlement",
        MusicSituation.UneasySettlement => "roentgen-suspicion",
        MusicSituation.TradeHub => "roentgen-trade-hub",
        MusicSituation.Suspicion or MusicSituation.Stealth or MusicSituation.Detected => "roentgen-stealth",
        MusicSituation.LowCombat => "jrpg-battle-strategic",
        MusicSituation.FullCombat => "roentgen-combat",
        MusicSituation.EliteCombat => "roentgen-elite-combat",
        MusicSituation.FinalConvergence => "roentgen-final-convergence",
        MusicSituation.Escape => "roentgen-escape",
        MusicSituation.Dialogue => "modal-noir",
        MusicSituation.Discovery => "cinematic-ambient",
        MusicSituation.Revelation => "roentgen-revelation",
        MusicSituation.Tragedy => "roentgen-tragedy",
        MusicSituation.Victory => "roentgen-victory",
        MusicSituation.Ending or MusicSituation.Credits => "roentgen-ending",
        _ => "deep-drone"
    };

    private static string ExplorationStyle(MusicContext context)
    {
        if (context.Weather == MusicWeatherKind.Contamination) return "dark-ambient";
        if (context.RegionId == GlassBasinRegion) return "roentgen-glass-basin";
        if (context.RegionId == OssuaryFieldsRegion) return "roentgen-ossuary-fields";
        if (context.RegionId == BlackwaterWorksRegion) return "roentgen-blackwater-works";
        if (context.RegionId == BrokenMeridianRegion) return "roentgen-broken-meridian";
        if (context.RegionId == RedQuarryRegion) return "roentgen-red-quarry";
        if (context.RegionId == QuietRangeRegion) return "roentgen-quiet-range";
        return "roentgen-wasteland-exploration";
    }

    private static (long Start, long End) SelectLoop(SongDocument song)
    {
        if (song.Sections.Count >= 3) return (song.Sections[1].StartTick, song.Sections[^2].EndTick);
        if (song.Sections.Count >= 1) return (song.Sections[0].StartTick, song.Sections[^1].EndTick);
        return (0, Math.Max(song.TicksPerQuarterNote * 16L, song.LengthTicks));
    }

    private static MusicHarmonicProfile BuildHarmony(SongDocument song)
    {
        TempoChange tempo = song.TempoMap.FirstOrDefault() ?? new TempoChange { BeatsPerMinute = 120 };
        MeterChange meter = song.MeterMap.FirstOrDefault() ?? new MeterChange { Numerator = 4, Denominator = 4 };
        return new MusicHarmonicProfile((int)song.KeyRoot, song.Scale.ToString(), tempo.BeatsPerMinute, meter.Numerator, meter.Denominator);
    }

    private static List<MusicStemDefinition> BuildStems(SongDocument song) => song.Tracks
        .Where(static track => !track.Muted)
        .GroupBy(static track => StemFor(track.Role))
        .Select(group => new MusicStemDefinition(group.Key.ToString().ToLowerInvariant(), group.Key, group.Select(static value => value.Id).ToArray(), 0f, 1f))
        .OrderBy(static value => value.Kind)
        .ToList();

    private static MusicStemKind StemFor(TrackRole role) => role switch
    {
        TrackRole.Drums => MusicStemKind.Percussion,
        TrackRole.Lead or TrackRole.CounterMelody => MusicStemKind.Melody,
        TrackRole.Arpeggio => MusicStemKind.Pulse,
        TrackRole.Texture => MusicStemKind.Texture,
        TrackRole.Bass or TrackRole.Chords or TrackRole.Harmony => MusicStemKind.Foundation,
        _ => MusicStemKind.Foundation
    };

    private static MusicIntensityProfile IntensityFor(MusicSituation situation) => situation switch
    {
        MusicSituation.EliteCombat or MusicSituation.FinalConvergence => new(0.75f, 1f, 0.95f),
        MusicSituation.FullCombat or MusicSituation.Escape => new(0.55f, 0.95f, 0.8f),
        MusicSituation.LowCombat or MusicSituation.Detected => new(0.35f, 0.75f, 0.58f),
        MusicSituation.Dialogue or MusicSituation.SafeSettlement => new(0.05f, 0.45f, 0.25f),
        _ => new(0.12f, 0.65f, 0.38f)
    };

    private static long BarTicks(SongDocument song)
    {
        MeterChange meter = song.MeterMap.FirstOrDefault() ?? new MeterChange { Numerator = 4, Denominator = 4 };
        return song.TicksPerQuarterNote * 4L / Math.Max(1, meter.Denominator) * Math.Max(1, meter.Numerator);
    }

    private static InstrumentCatalogDocument CloneCatalog(InstrumentCatalogDocument source) => new()
    {
        SoundFontId = source.SoundFontId,
        SoundFontName = source.SoundFontName,
        SourcePath = source.SourcePath,
        SourceFingerprint = source.SourceFingerprint,
        ScannedAtUtc = DateTimeOffset.UnixEpoch,
        IsComplete = source.IsComplete,
        Presets = source.Presets.Select(static value => value.Clone()).ToList(),
        Diagnostics = source.Diagnostics.ToList()
    };
}
