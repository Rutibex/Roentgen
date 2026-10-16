using Roentgen.Contracts;

namespace Roentgen.Music;

public sealed record MusicContext(
    StableId RegionId,
    StableId LocationId,
    StableId ControllingFactionId,
    StableId ActiveCompanionId,
    MusicLocationKind LocationKind,
    MusicThreatLevel Threat,
    float CombatIntensity,
    float PlayerHealthRatio,
    bool IsStealthy,
    bool IsDetected,
    bool IsInDialogue,
    bool IsInRoentgenFocus,
    MusicDiscoveryKind Discovery,
    MusicCampaignMoment CampaignMoment,
    int HourOfDay,
    MusicWeatherKind Weather,
    float Contamination,
    ulong SimulationTick)
{
    public static MusicContext Default => new(
        StableId.Empty, StableId.Empty, StableId.Empty, StableId.Empty,
        MusicLocationKind.Exterior, MusicThreatLevel.None, 0f, 1f,
        false, false, false, false, MusicDiscoveryKind.None,
        MusicCampaignMoment.Ordinary, 12, MusicWeatherKind.Clear, 0f, 0UL);
}

public readonly record struct MusicCueIdentity(
    string CueId,
    ulong Seed,
    string StyleId,
    string GeneratorVersion,
    string SoundFontFingerprint)
{
    public override string ToString() => $"{CueId}:{Seed:x16}:{StyleId}:{GeneratorVersion}:{SoundFontFingerprint}";
}

public sealed record MusicLoopRegion(string Id, long StartTick, long EndTick, bool IsPrimary);
public sealed record MusicTransitionPoint(string Id, long Tick, MusicTransitionQuantization Quantization, bool IsEntry, bool IsExit);
public sealed record MusicStemDefinition(string Id, MusicStemKind Kind, IReadOnlyList<string> TrackIds, float MinimumGain, float MaximumGain);
public sealed record MusicHarmonicProfile(int KeyRoot, string Scale, double Tempo, int MeterNumerator, int MeterDenominator);
public sealed record MusicIntensityProfile(float Minimum, float Maximum, float Preferred);

public sealed record MusicCuePlan(
    MusicCueIdentity Identity,
    string Title,
    MusicSituation Situation,
    IReadOnlyList<MusicLoopRegion> Loops,
    IReadOnlyList<MusicTransitionPoint> EntryPoints,
    IReadOnlyList<MusicTransitionPoint> ExitPoints,
    IReadOnlyList<MusicStemDefinition> Stems,
    MusicHarmonicProfile Harmony,
    MusicIntensityProfile Intensity,
    string Fingerprint);

public sealed record MusicTransitionPlan(
    MusicCueIdentity Source,
    MusicCueIdentity Destination,
    MusicTransitionKind Kind,
    MusicTransitionQuantization Quantization,
    long RequestedAtTick,
    float CrossfadeSeconds,
    string BridgeId,
    string Rationale);

public sealed record MusicMotifDefinition(
    StableId OwnerId,
    string MotifId,
    ulong Seed,
    IReadOnlyList<int> ScaleDegreeContour,
    IReadOnlyList<int> RhythmUnits,
    string HarmonicColor,
    IReadOnlyList<string> PreferredFamilies,
    string Fingerprint);

public sealed record MusicTelemetrySnapshot(
    MusicRuntimeState State,
    MusicSituation Situation,
    string CurrentCue,
    string NextCue,
    string ActiveDeck,
    long MusicalTick,
    int Bar,
    int Beat,
    int ActiveNotes,
    int PeakPolyphony,
    int PendingCommands,
    long RenderedFrames,
    long CompletedTransitions,
    long LateTransitions,
    long FallbackTransitions,
    long CommandRejections,
    double LastRenderMilliseconds,
    double PeakRenderMilliseconds,
    string SoundFontName,
    string SoundFontFingerprint,
    string FaultMessage);

public sealed record MusicSnapshotV19(
    int SchemaVersion,
    string GeneratorVersion,
    string StyleLibraryFingerprint,
    string SoundFontAssetId,
    string SoundFontSha256,
    IReadOnlyList<MusicMotifDefinition> Motifs,
    MusicSituation Situation,
    MusicCueIdentity CurrentCue,
    string CurrentLoopId,
    long MusicalTick,
    IReadOnlyDictionary<MusicStemKind, float> StemGains,
    float Intensity,
    MusicTransitionPlan? PendingTransition,
    IReadOnlyDictionary<string, ulong> StingerCooldowns);

public interface IMusicRuntime : IDisposable
{
    MusicRuntimeState State { get; }
    MusicTelemetrySnapshot Telemetry { get; }
    void Start();
    void Stop();
    void Pause();
    void Resume();
    void UpdateContext(MusicContext context);
    void RequestStinger(MusicSituation situation, StableId subjectId, ulong tick);
    MusicSnapshotV19 CaptureSnapshot();
    void RestoreSnapshot(MusicSnapshotV19 snapshot);
    void PumpControl();
}

public interface IMusicCueProvider
{
    MusicCuePlan PrepareCue(MusicSituation situation, MusicContext context, ulong seed);
}

public interface IMotifRegistry
{
    IReadOnlyList<MusicMotifDefinition> Motifs { get; }
    MusicMotifDefinition GetOrCreate(StableId ownerId, string role);
}
