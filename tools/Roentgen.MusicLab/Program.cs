using System.Buffers.Binary;
using System.Security.Cryptography;
using PlayromaMusic.Core.Orchestration;
using Roentgen.Contracts;
using Roentgen.Music;
using Roentgen.Music.Playroma;

const ulong DefaultSeed = 0x524F454E5447454EUL;
string command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
ulong seed = ParseSeed(args.Skip(1).FirstOrDefault());
string root = FindContentRoot();
var settings = new MusicSettings();

try
{
    switch (command)
    {
        case "inspect-font": InspectFont(); break;
        case "list-presets": ListPresets(); break;
        case "generate-cue": GenerateCue(ParseSituation(args.Skip(2).FirstOrDefault())); break;
        case "render-cue": RenderCue(ParseSituation(args.Skip(2).FirstOrDefault()), args.Skip(3).FirstOrDefault() ?? "artifacts/music/phase19-cue.wav"); break;
        case "test-transition": TestTransition(); break;
        case "compare-seeds": CompareSeeds(seed, ParseSeed(args.Skip(2).FirstOrDefault(), seed + 1)); break;
        case "stress": Stress(ParseInt(args.Skip(2).FirstOrDefault(), 30)); break;
        case "validate": Validate(); break;
        default: Help(); break;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}

void InspectFont()
{
    RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(settings, root);
    InstrumentCatalogDocument catalog = RoentgenPresetCatalog.Load(settings, root, asset);
    Console.WriteLine($"asset={asset.AssetId}");
    Console.WriteLine($"name={asset.Name}");
    Console.WriteLine($"sha256={asset.Sha256}");
    Console.WriteLine($"bytes={asset.Length}");
    Console.WriteLine($"presets={catalog.Presets.Count} enabled={catalog.EnabledPresetCount}");
}

void ListPresets()
{
    RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(settings, root);
    InstrumentCatalogDocument catalog = RoentgenPresetCatalog.Load(settings, root, asset);
    foreach (InstrumentPresetDescriptor item in catalog.Presets.OrderBy(static value => value.Bank).ThenBy(static value => value.Program))
        Console.WriteLine($"{item.Bank,3}:{item.Program,3} {item.Family,-20} {(item.IsExcluded ? "EXCLUDED" : "enabled ")} {item.Name}");
}

void GenerateCue(MusicSituation situation)
{
    RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(settings, root);
    InstrumentCatalogDocument catalog = RoentgenPresetCatalog.Load(settings, root, asset);
    var generator = new RoentgenCueGenerator(catalog, asset, settings.SampleRate);
    AdaptiveCueDocument cue = generator.PrepareDocument(situation, MusicContext.Default, seed);
    Console.WriteLine($"{cue.Plan.Identity}");
    Console.WriteLine($"title={cue.Plan.Title}");
    Console.WriteLine($"fingerprint={cue.Plan.Fingerprint}");
    Console.WriteLine($"style={cue.Plan.Identity.StyleId} tempo={cue.Plan.Harmony.Tempo:0.##} key={cue.Plan.Harmony.KeyRoot}/{cue.Plan.Harmony.Scale}");
    Console.WriteLine($"tracks={cue.Composition.Song.Tracks.Count} notes={cue.Composition.Song.EnumerateNotes().Count()} loop={cue.PrimaryLoopStartSample}-{cue.PrimaryLoopEndSample}");
    foreach (MusicStemDefinition stem in cue.Plan.Stems) Console.WriteLine($"stem {stem.Kind}: {string.Join(',', stem.TrackIds)}");
}

void RenderCue(MusicSituation situation, string path)
{
    using var runtime = new AdaptiveMusicRuntime(seed, settings, root);
    runtime.Start();
    runtime.UpdateContext(MusicContext.Default with { SimulationTick = 1, CampaignMoment = situation == MusicSituation.Ending ? MusicCampaignMoment.Ending : MusicCampaignMoment.Ordinary, CombatIntensity = situation is MusicSituation.FullCombat or MusicSituation.EliteCombat ? 0.9f : 0f, Threat = situation == MusicSituation.EliteCombat ? MusicThreatLevel.Overwhelming : MusicThreatLevel.None });
    float[] audio = new float[settings.SampleRate * 12 * 2];
    float[] block = new float[settings.RenderBlockFrames * 2];
    int written = 0;
    for (int spin = 0; spin < 400 && string.IsNullOrEmpty(runtime.Telemetry.CurrentCue); spin++) { runtime.PumpControl(); Thread.Sleep(10); }
    while (written < audio.Length)
    {
        runtime.PumpControl();
        int frames = Math.Min(settings.RenderBlockFrames, (audio.Length - written) / 2);
        runtime.Render(block, frames);
        block.AsSpan(0, frames * 2).CopyTo(audio.AsSpan(written));
        written += frames * 2;
    }
    WriteWav(path, audio, settings.SampleRate);
    Console.WriteLine($"wrote {path} cue={runtime.Telemetry.CurrentCue} situation={runtime.Telemetry.Situation}");
}

void TestTransition()
{
    using var runtime = new AdaptiveMusicRuntime(seed, settings, root);
    runtime.Start();
    MusicContext context = MusicContext.Default with { SimulationTick = 1 };
    runtime.UpdateContext(context);
    RenderSeconds(runtime, 3);
    runtime.UpdateContext(context with { SimulationTick = 400, CombatIntensity = 0.95f, Threat = MusicThreatLevel.Overwhelming, IsDetected = true });
    RenderSeconds(runtime, 8);
    MusicTelemetrySnapshot telemetry = runtime.Telemetry;
    Console.WriteLine($"situation={telemetry.Situation} cue={telemetry.CurrentCue} transitions={telemetry.CompletedTransitions} late={telemetry.LateTransitions} fallback={telemetry.FallbackTransitions}");
    if (telemetry.CompletedTransitions < 1) throw new InvalidOperationException("No adaptive transition completed.");
}

void Stress(int seconds)
{
    using var runtime = new AdaptiveMusicRuntime(seed, settings, root);
    runtime.Start();
    float[] block = new float[settings.RenderBlockFrames * 2];
    int blocks = seconds * settings.SampleRate / settings.RenderBlockFrames;
    for (int i = 0; i < blocks; i++)
    {
        ulong tick = (ulong)i * 10UL;
        float intensity = (i / 64) % 2 == 0 ? 0.92f : 0.05f;
        runtime.UpdateContext(MusicContext.Default with { SimulationTick = tick, CombatIntensity = intensity, Threat = intensity > 0.5f ? MusicThreatLevel.Engaged : MusicThreatLevel.None });
        runtime.PumpControl();
        runtime.Render(block, settings.RenderBlockFrames);
        if (block.Any(static value => !float.IsFinite(value))) throw new InvalidDataException("Non-finite music sample.");
    }
    MusicTelemetrySnapshot t = runtime.Telemetry;
    Console.WriteLine($"frames={t.RenderedFrames} transitions={t.CompletedTransitions} commands={t.PendingCommands} render={t.LastRenderMilliseconds:0.###}/{t.PeakRenderMilliseconds:0.###}ms poly={t.ActiveNotes}/{t.PeakPolyphony}");
}

void CompareSeeds(ulong first, ulong second)
{
    RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(settings, root);
    InstrumentCatalogDocument catalog = RoentgenPresetCatalog.Load(settings, root, asset);
    var generator = new RoentgenCueGenerator(catalog, asset, settings.SampleRate);
    string a = generator.PrepareDocument(MusicSituation.WastelandExploration, MusicContext.Default, first).Plan.Fingerprint;
    string b = generator.PrepareDocument(MusicSituation.WastelandExploration, MusicContext.Default, second).Plan.Fingerprint;
    Console.WriteLine($"0x{first:X16} {a}");
    Console.WriteLine($"0x{second:X16} {b}");
    Console.WriteLine(a == b ? "same" : "different");
}

void Validate()
{
    InspectFont();
    RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(settings, root);
    InstrumentCatalogDocument catalog = RoentgenPresetCatalog.Load(settings, root, asset);
    if (catalog.Presets.Count != 287) throw new InvalidDataException($"Expected 287 presets, found {catalog.Presets.Count}.");
    var generator = new RoentgenCueGenerator(catalog, asset, settings.SampleRate);
    foreach (MusicSituation situation in new[] { MusicSituation.WastelandExploration, MusicSituation.SafeSettlement, MusicSituation.Stealth, MusicSituation.FullCombat, MusicSituation.EliteCombat, MusicSituation.Revelation, MusicSituation.Ending })
    {
        AdaptiveCueDocument cue = generator.PrepareDocument(situation, MusicContext.Default, seed ^ (ulong)situation);
        if (cue.Plan.Loops.Count == 0) throw new InvalidDataException($"{situation} has no loop metadata.");
        if (cue.Composition.Song.Tracks.Where(static value => !value.Muted && !value.IsPercussion).GroupBy(static value => value.MidiChannel).Any(static value => value.Count() > 1)) throw new InvalidDataException($"{situation} has a melodic channel collision.");
        Console.WriteLine($"PASS {situation,-24} {cue.Plan.Identity.StyleId,-32} {cue.Plan.Fingerprint[..16]}");
    }
}

void RenderSeconds(AdaptiveMusicRuntime runtime, int seconds)
{
    float[] block = new float[settings.RenderBlockFrames * 2];
    int blocks = seconds * settings.SampleRate / settings.RenderBlockFrames;
    for (int i = 0; i < blocks; i++) { runtime.PumpControl(); runtime.Render(block, settings.RenderBlockFrames); Thread.Sleep(1); }
}

void WriteWav(string path, float[] samples, int sampleRate)
{
    string full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    using FileStream stream = File.Create(full); using BinaryWriter writer = new(stream);
    int dataBytes = samples.Length * 2;
    writer.Write("RIFF"u8); writer.Write(36 + dataBytes); writer.Write("WAVE"u8); writer.Write("fmt "u8); writer.Write(16); writer.Write((short)1); writer.Write((short)2); writer.Write(sampleRate); writer.Write(sampleRate * 4); writer.Write((short)4); writer.Write((short)16); writer.Write("data"u8); writer.Write(dataBytes);
    Span<byte> bytes = stackalloc byte[2];
    foreach (float value in samples) { short pcm = (short)Math.Clamp((int)Math.Round(Math.Clamp(value, -1f, 1f) * 32767f), short.MinValue, short.MaxValue); BinaryPrimitives.WriteInt16LittleEndian(bytes, pcm); writer.Write(bytes); }
}

static string FindContentRoot()
{
    string current = AppContext.BaseDirectory;
    for (int i = 0; i < 10; i++)
    {
        if (File.Exists(Path.Combine(current, "content", "music", "soundfonts", "default.sf2"))) return current;
        DirectoryInfo? parent = Directory.GetParent(current); if (parent is null) break; current = parent.FullName;
    }
    return AppContext.BaseDirectory;
}
static ulong ParseSeed(string? text, ulong fallback = DefaultSeed) => string.IsNullOrWhiteSpace(text) ? fallback : text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Convert.ToUInt64(text[2..], 16) : Convert.ToUInt64(text, System.Globalization.CultureInfo.InvariantCulture);
static int ParseInt(string? text, int fallback) => int.TryParse(text, out int value) ? value : fallback;
static MusicSituation ParseSituation(string? text) => Enum.TryParse(text, true, out MusicSituation value) ? value : MusicSituation.WastelandExploration;
static void Help() => Console.WriteLine("Roentgen.MusicLab commands: inspect-font, list-presets, generate-cue [seed] [situation], render-cue [seed] [situation] [wav], test-transition, compare-seeds, stress [seed] [seconds], validate");
