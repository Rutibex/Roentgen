
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Playroma.Sound.Graph.Serialization;
using Roentgen.Audio;
using Roentgen.Audio.Playroma;
using Roentgen.Core;

const ulong DefaultSeed = 0x524F454E5447454EUL;
string command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
ulong seed = ParseSeed(args.Skip(1).FirstOrDefault());
var settings = new AudioSettings();

try
{
    switch (command)
    {
        case "catalog": PrintCatalog(seed); break;
        case "render": Render(seed, args.Skip(2).FirstOrDefault() ?? "artifacts/audio/phase18-diagnostic.wav", 8); break;
        case "stress": Stress(seed, ParseInt(args.Skip(2).FirstOrDefault(), 30)); break;
        case "compare-seeds": Compare(seed, ParseSeed(args.Skip(2).FirstOrDefault(), seed + 1)); break;
        case "validate": Validate(seed); break;
        default: Help(); break;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}

void PrintCatalog(ulong value)
{
    var serializer = new SoundGraphSerializer();
    foreach ((string id, var graph) in IntegratedSoundCatalog.Build(value, settings.Quality).OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        Console.WriteLine($"{id,-44} {Sha(serializer.Serialize(graph))}");
}

void Validate(ulong value)
{
    var catalog = IntegratedSoundCatalog.Build(value, settings.Quality);
    if (catalog.Count < 90) throw new InvalidOperationException($"Catalog too small: {catalog.Count}");
    using var runtime = new PlayromaAudioRuntime(value, settings);
    runtime.Start();
    runtime.Submit(Event(1, AudioEventKind.WeaponFire, "industrial-rifle"));
    float[] block = new float[runtime.RenderBlockFrames * runtime.Channels];
    for (int i = 0; i < 32; i++) runtime.Render(block, runtime.RenderBlockFrames);
    if (block.Any(static sample => !float.IsFinite(sample))) throw new InvalidDataException("Non-finite sample.");
    Console.WriteLine($"PASS assets={catalog.Count} frames={runtime.Telemetry.RenderedFrames} state={runtime.State}");
}

void Render(ulong value, string path, int seconds)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using var runtime = new PlayromaAudioRuntime(value, settings);
    runtime.Start();
    runtime.SetRegion(AudioRegion.BrokenMeridian, value ^ 0x42524F4B454EUL);
    int frames = runtime.SampleRate * seconds;
    float[] audio = new float[frames * runtime.Channels];
    int frame = 0;
    while (frame < frames)
    {
        if (frame % runtime.SampleRate == 0)
        {
            ulong tick = (ulong)(frame / 800);
            runtime.Submit(Event(tick + 1, frame % (runtime.SampleRate * 2) == 0 ? AudioEventKind.WeaponFire : AudioEventKind.FootContact, "industrial-rifle"));
        }
        int count = Math.Min(runtime.RenderBlockFrames, frames - frame);
        runtime.Render(audio.AsSpan(frame * runtime.Channels, count * runtime.Channels), count);
        frame += count;
    }
    WriteWav(path, audio, runtime.SampleRate, runtime.Channels);
    Console.WriteLine($"wrote {path} ({seconds}s, {audio.Length} samples)");
}

void Stress(ulong value, int seconds)
{
    using var runtime = new PlayromaAudioRuntime(value, settings);
    runtime.Start();
    float[] block = new float[runtime.RenderBlockFrames * runtime.Channels];
    int blocks = checked(seconds * runtime.SampleRate / runtime.RenderBlockFrames);
    AudioEventKind[] eventKinds =
    [
        AudioEventKind.WeaponFire,
        AudioEventKind.Impact,
        AudioEventKind.FootContact,
        AudioEventKind.Injury,
        AudioEventKind.Explosion,
        AudioEventKind.MachineMalfunction
    ];
    for (int i = 0; i < blocks; i++)
    {
        ulong tick = (ulong)(i * runtime.RenderBlockFrames / 800);
        for (int j = 0; j < eventKinds.Length; j++) runtime.Submit(Event(tick + (ulong)j + 1, eventKinds[j], j == 0 ? "industrial-rifle" : ""));
        runtime.Render(block, runtime.RenderBlockFrames);
        runtime.PumpControl();
    }
    AudioTelemetrySnapshot t = runtime.Telemetry;
    Console.WriteLine($"state={t.RuntimeState} frames={t.RenderedFrames} started={t.StartedVoices} rejected={t.RejectedVoices} stolen={t.StolenVoices} faulted={t.FaultedVoices} nonfinite={t.NonFiniteSamples}");
    if (t.FaultedVoices != 0 || t.NonFiniteSamples != 0) Environment.ExitCode = 2;
}

void Compare(ulong a, ulong b)
{
    string A = Fingerprint(a); string B = Fingerprint(b);
    Console.WriteLine($"0x{a:X16} {A}"); Console.WriteLine($"0x{b:X16} {B}");
    Console.WriteLine(A == B ? "same" : "different");
}

string Fingerprint(ulong value)
{
    var serializer = new SoundGraphSerializer();
    var builder = new StringBuilder();
    foreach ((string id, var graph) in IntegratedSoundCatalog.Build(value, settings.Quality).OrderBy(static pair => pair.Key, StringComparer.Ordinal)) builder.Append(id).Append(':').Append(Sha(serializer.Serialize(graph))).Append('\n');
    return Sha(builder.ToString());
}

SemanticAudioEvent Event(ulong tick, AudioEventKind kind, string variant)
{
    var values = new Dictionary<string, AudioValue>(StringComparer.Ordinal);
    if (variant.Length != 0) values["variant"] = AudioValue.FromText(variant);
    values["surface"] = AudioValue.FromInt64((long)AudioSurface.Concrete);
    values["gait"] = AudioValue.FromInt64((long)AudioGait.Run);
    return SemanticAudioEvent.Create(StableIdFactory.Create("audiolab.event", $"{tick}:{kind}:{variant}"), tick, kind, AudioEmitterState.At(new AudioVector3(2, 0, -4)), parameters: values);
}

static void WriteWav(string path, ReadOnlySpan<float> samples, int sampleRate, int channels)
{
    int dataBytes = checked(samples.Length * 2);
    using var stream = File.Create(path);
    Span<byte> header = stackalloc byte[44];
    Encoding.ASCII.GetBytes("RIFF", header); BinaryPrimitives.WriteInt32LittleEndian(header[4..], 36 + dataBytes); Encoding.ASCII.GetBytes("WAVEfmt ", header[8..]);
    BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16); BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1); BinaryPrimitives.WriteInt16LittleEndian(header[22..], (short)channels);
    BinaryPrimitives.WriteInt32LittleEndian(header[24..], sampleRate); BinaryPrimitives.WriteInt32LittleEndian(header[28..], sampleRate * channels * 2); BinaryPrimitives.WriteInt16LittleEndian(header[32..], (short)(channels * 2)); BinaryPrimitives.WriteInt16LittleEndian(header[34..], 16); Encoding.ASCII.GetBytes("data", header[36..]); BinaryPrimitives.WriteInt32LittleEndian(header[40..], dataBytes);
    stream.Write(header);
    byte[] bytes = new byte[dataBytes];
    for (int i = 0; i < samples.Length; i++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), (short)Math.Clamp((int)Math.Round(samples[i] * short.MaxValue), short.MinValue, short.MaxValue));
    stream.Write(bytes);
}

static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
static ulong ParseSeed(string? value, ulong fallback = DefaultSeed) => string.IsNullOrWhiteSpace(value) ? fallback : value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ulong.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : ulong.Parse(value, CultureInfo.InvariantCulture);
static int ParseInt(string? value, int fallback) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
static void Help() { Console.WriteLine("Roentgen AudioLab Phase 18"); Console.WriteLine("  catalog [seed]"); Console.WriteLine("  render [seed] [wav-path]"); Console.WriteLine("  stress [seed] [seconds]"); Console.WriteLine("  compare-seeds [seed-a] [seed-b]"); Console.WriteLine("  validate [seed]"); }
