using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Focus;

public enum FocusTemporalMode { Inactive, Entering, Planning, Committing, Executing, Completed, Interrupted, Exiting }
public enum DiagnosticKnowledgeLevel { Undetected, Silhouette, ExternalStructure, ArmorMapped, InternalStructure, FullyDiagnosed }
public enum FocusTargetNodeKind { Anatomy, Limb, Organ, ArmorPlate, EquippedWeapon, CarriedExplosive, Implant, MachineComponent, HazardComponent }
public enum FocusActionKind { Fire, Melee, Throw, Reload, SwitchWeapon }
public enum FocusTrackingPolicy { LockedAimPoint, TrackSamePart, TrackTargetCenter }
public enum FocusTargetLossPolicy { Cancel, TrackSamePart, FallbackToParent, FallbackToAnyValidPart, TransferToNextHostile }
public enum FocusForecastConfidence { Low, Medium, High }
public enum FocusInterruptionReason { None, PlayerDied, Stunned, Staggered, WeaponLost, ActionDisabled, TargetLost, InvalidState, UserCancelled }

public readonly record struct CombatResolutionSeed(ulong Root, ulong AttackSequence, ulong PelletSequence) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteUInt64(Root); writer.WriteUInt64(AttackSequence); writer.WriteUInt64(PelletSequence); }
    public ulong Derive(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var writer = new CanonicalWriter(); WriteCanonical(writer); writer.WriteString(domain);
        byte[] b = writer.ToArray(); ulong value = 1469598103934665603UL;
        foreach (byte x in b) value = (value ^ x) * 1099511628211UL;
        return value;
    }
}

public sealed record ForecastFactor(string Id, int ModifierPermille, string Explanation);
public sealed record PredictedConsequence(string Id, string Description, int ChancePermille, int SeverityPermille);
public sealed record ForecastUncertainty(string Id, string Description, int MagnitudePermille);

public sealed record FocusActionForecast(
    StableId ActionId, StableId ActorId, StableId TargetId, StableId TargetNodeId,
    int HitChancePermille, int GrazeChancePermille, int ExpectedDamageMilliunits,
    int MinimumDamage, int MaximumDamage, int ArmorAbsorptionPermille,
    int InjuryChancePermille, int IncapacitationChancePermille, int KillChancePermille,
    int FocusCostMilliunits, FocusForecastConfidence Confidence,
    IReadOnlyList<ForecastFactor> Factors,
    IReadOnlyList<PredictedConsequence> Consequences,
    IReadOnlyList<ForecastUncertainty> Uncertainties);

public sealed record FocusAccessibilitySettings(
    bool CinematicCuts = true, bool CameraShake = true, bool ExecutionZoom = true,
    int DiagnosticIntensityPermille = 1000, int PhosphorPersistencePermille = 650,
    int FlashIntensityPermille = 500, int SlowMotionPermille = 250,
    bool HighContrast = false, bool ToggleActivation = true)
{
    public FocusAccessibilitySettings Clamped() => this with {
        DiagnosticIntensityPermille = Math.Clamp(DiagnosticIntensityPermille,0,1000),
        PhosphorPersistencePermille = Math.Clamp(PhosphorPersistencePermille,0,1000),
        FlashIntensityPermille = Math.Clamp(FlashIntensityPermille,0,1000),
        SlowMotionPermille = Math.Clamp(SlowMotionPermille,50,1000)};
}
