using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.AI;

public enum AiStimulusKind
{
    Footstep = 0,
    Gunshot = 1,
    Explosion = 2,
    Door = 3,
    Speech = 4,
    Alarm = 5,
    Machinery = 6,
    CreatureCall = 7,
    BrokenObject = 8,
    SuspiciousMovement = 9
}

public sealed record AiStimulus(
    StableId Id,
    AiStimulusKind Kind,
    StableId SourceId,
    FixedVector3 Position,
    int IntensityPermille,
    int MaximumRangeMillimeters,
    StableId FactionContext,
    ulong CreatedTick,
    ulong ExpiresTick);

public interface IAiVisibilityQuery
{
    bool HasLineOfSight(FixedVector3 observerEye, FixedVector3 targetPoint, StableId observerId, StableId targetId);
    int LightingPermille(FixedVector3 position);
    int ConcealmentPermille(StableId targetId);
}

public sealed class UnobstructedAiVisibilityQuery : IAiVisibilityQuery
{
    public static UnobstructedAiVisibilityQuery Instance { get; } = new();
    public bool HasLineOfSight(FixedVector3 observerEye, FixedVector3 targetPoint, StableId observerId, StableId targetId) => true;
    public int LightingPermille(FixedVector3 position) => 1000;
    public int ConcealmentPermille(StableId targetId) => 0;
}

public sealed record PerceptionProfile(
    int ViewDistanceMillimeters = 55_000,
    int HorizontalFieldOfViewDegrees = 110,
    int HearingSensitivityPermille = 1000,
    int EvidenceShareRangeMillimeters = 25_000,
    int MemoryDecayPerSecondPermille = 24);

public sealed class AiStimulusRegistry
{
    private readonly SortedDictionary<StableId, AiStimulus> stimuli = [];
    public IReadOnlyCollection<AiStimulus> Stimuli => stimuli.Values;
    public ulong Revision { get; private set; }
    public void Emit(AiStimulus stimulus) { stimuli[stimulus.Id] = stimulus; Revision++; }
    public void Expire(ulong tick)
    {
        foreach (StableId id in stimuli.Values.Where(value => value.ExpiresTick <= tick).Select(value => value.Id).ToArray())
        {
            stimuli.Remove(id); Revision++;
        }
    }
}

public sealed class PerceptionService
{
    private readonly IAiVisibilityQuery visibility;
    public PerceptionService(IAiVisibilityQuery? visibility = null) => this.visibility = visibility ?? UnobstructedAiVisibilityQuery.Instance;

    public bool Observe(CharacterState observer, CharacterState target, AgentBrainState brain, PerceptionProfile profile, ulong tick)
    {
        FixedVector3 delta = target.FeetPosition - observer.FeetPosition;
        double distance = Length(delta);
        int injuryPenalty = Math.Clamp(observer.Injury.HeadTraumaPermille * 3 / 4, 0, 800);
        int effectiveRange = profile.ViewDistanceMillimeters * (1000 - injuryPenalty) / 1000;
        if (distance > effectiveRange || !WithinFieldOfView(observer.FacingYawUnits, delta, profile.HorizontalFieldOfViewDegrees)) return false;
        FixedVector3 eye = observer.FeetPosition + new FixedVector3(0, Math.Max(900, observer.Appearance.HeightMillimeters - 180), 0);
        FixedVector3 targetPoint = target.FeetPosition + new FixedVector3(0, Math.Max(700, target.Appearance.HeightMillimeters * 2 / 3), 0);
        if (!visibility.HasLineOfSight(eye, targetPoint, observer.Id, target.Id)) return false;
        int light = Math.Clamp(visibility.LightingPermille(target.FeetPosition), 100, 1000);
        int concealment = Math.Clamp(visibility.ConcealmentPermille(target.Id), 0, 900);
        int movement = target.Velocity == FixedVector3.Zero ? 750 : 1000;
        int rangeFactor = Math.Clamp(1000 - (int)(distance * 700 / Math.Max(1, effectiveRange)), 250, 1000);
        int confidence = light * (1000 - concealment) / 1000 * movement / 1000 * rangeFactor / 1000;
        if (confidence < 160) return false;
        brain.Awareness.Confirm(target.Id, target.FeetPosition, confidence, tick);
        observer.Attention = CharacterAttentionKind.TrackActor;
        observer.AttentionTargetId = target.Id;
        observer.AttentionTargetPosition = target.FeetPosition;
        observer.Revision++;
        return true;
    }

    public bool Hear(CharacterState observer, AgentBrainState brain, IEnumerable<AiStimulus> stimuli, PerceptionProfile profile, ulong tick)
    {
        AiStimulus? best = null;
        int bestEvidence = 0;
        foreach (AiStimulus stimulus in stimuli.OrderBy(static value => value.Id))
        {
            if (stimulus.ExpiresTick <= tick || stimulus.SourceId == observer.Id) continue;
            double distance = Length(stimulus.Position - observer.FeetPosition);
            int effectiveRange = stimulus.MaximumRangeMillimeters * profile.HearingSensitivityPermille / 1000;
            if (distance > effectiveRange) continue;
            int evidence = Math.Clamp(stimulus.IntensityPermille * (1000 - (int)(distance * 1000 / Math.Max(1, effectiveRange))) / 1000, 0, 1000);
            if (evidence > bestEvidence || (evidence == bestEvidence && best is not null && stimulus.Id.CompareTo(best.Id) < 0)) { best = stimulus; bestEvidence = evidence; }
        }
        if (best is null || bestEvidence < 80) return false;
        brain.Awareness.Hear(best.Position, bestEvidence, tick);
        observer.Attention = CharacterAttentionKind.ObservePoint;
        observer.AttentionTargetPosition = best.Position;
        observer.Revision++;
        return true;
    }

    public void ShareAlerts(AgentBrainRegistry registry, IReadOnlyDictionary<StableId, CharacterState> characters, PerceptionProfile profile, ulong tick)
    {
        foreach (AgentBrainState source in registry.Agents.Where(value => value.Awareness.Level >= AwarenessLevel.Alerted).OrderBy(static value => value.AgentId))
        {
            if (!characters.TryGetValue(source.AgentId, out CharacterState? sourceCharacter)) continue;
            foreach (AgentBrainState target in registry.Agents.Where(value => value.FactionId == source.FactionId && value.AgentId != source.AgentId).OrderBy(static value => value.AgentId))
            {
                if (!characters.TryGetValue(target.AgentId, out CharacterState? targetCharacter)) continue;
                if (Length(targetCharacter.FeetPosition - sourceCharacter.FeetPosition) > profile.EvidenceShareRangeMillimeters) continue;
                target.Awareness.Share(source.Awareness, 780, tick);
            }
        }
    }

    private static bool WithinFieldOfView(int facingYawUnits, FixedVector3 delta, int fieldOfViewDegrees)
    {
        if (delta.XMillimeters == 0 && delta.ZMillimeters == 0) return true;
        double facing = facingYawUnits / 10000.0;
        double target = Math.Atan2(delta.XMillimeters, delta.ZMillimeters);
        double difference = Math.Abs(Math.Atan2(Math.Sin(target - facing), Math.Cos(target - facing)));
        return difference <= fieldOfViewDegrees * Math.PI / 360.0;
    }

    private static double Length(FixedVector3 value) => Math.Sqrt((double)value.XMillimeters * value.XMillimeters + (double)value.YMillimeters * value.YMillimeters + (double)value.ZMillimeters * value.ZMillimeters);
}

/// <summary>Line-of-sight adapter backed by the shared world collision queries.</summary>
public sealed class CollisionAiVisibilityQuery(Roentgen.Physics.IPlayerCollisionWorld collisionWorld) : IAiVisibilityQuery
{
    public bool HasLineOfSight(FixedVector3 observerEye, FixedVector3 targetPoint, StableId observerId, StableId targetId)
    {
        FixedVector3 direction = targetPoint - observerEye;
        int distance = checked((int)Math.Min(int.MaxValue, Math.Round(Math.Sqrt((double)direction.XMillimeters * direction.XMillimeters + (double)direction.YMillimeters * direction.YMillimeters + (double)direction.ZMillimeters * direction.ZMillimeters), MidpointRounding.AwayFromZero)));
        return distance <= 0 || !collisionWorld.Raycast(observerEye, direction, distance, Roentgen.Physics.CollisionQueryMask.All, out _);
    }
    public int LightingPermille(FixedVector3 position) => 850;
    public int ConcealmentPermille(StableId targetId) => 0;
}
