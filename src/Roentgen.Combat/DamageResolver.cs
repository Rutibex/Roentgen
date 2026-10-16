using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.Combat;

public sealed record ArmorDamageDetail(StableId ArmorId, DamageType Type, int Incoming, int ThresholdStopped, int ResistanceStopped, int Penetrated);

public sealed record DamageResolution(
    StableId ActorId,
    StableId BodyPartId,
    AnatomicalRegion Region,
    int RawDamage,
    int ArmorStopped,
    int FinalDamage,
    int HealthDamage,
    BodyPartFunctionalState PreviousState,
    BodyPartFunctionalState NewState,
    bool ActorDied,
    IReadOnlyList<ArmorDamageDetail> ArmorDetails);

public sealed class DamageResolver
{
    public DamageResolution Apply(
        CombatActorState actor,
        BodyPartRuntimeState part,
        IReadOnlyList<DamageComponent> damage,
        int damageScalePermille,
        bool blocked,
        ulong tick)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(damage);
        int rawTotal = 0;
        int stoppedTotal = 0;
        int finalTotal = 0;
        var details = new List<ArmorDamageDetail>();
        foreach (DamageComponent rawComponent in damage)
        {
            DamageComponent component = rawComponent.Clamped();
            int incoming = checked(component.Amount * Math.Clamp(damageScalePermille, 0, 5000) / 1000);
            if (blocked && component.Type is DamageType.Kinetic or DamageType.Cutting or DamageType.Blunt) incoming = incoming * 40 / 100;
            rawTotal = checked(rawTotal + incoming);
            int remaining = incoming;
            foreach (ArmorRuntimeState armor in actor.Armor.Where(value => value.Covers(part.Definition.Region)).OrderByDescending(static value => value.Layer).ThenBy(static value => value.Id))
            {
                if (remaining <= 0 || armor.ConditionPermille <= 0) continue;
                int condition = Math.Max(100, armor.ConditionPermille);
                int threshold = Math.Min(remaining, armor.Threshold(component.Type) * condition / 1000);
                remaining -= threshold;
                int resistance = remaining * armor.ResistancePermille(component.Type) * condition / 1_000_000;
                remaining -= resistance;
                int absorbed = threshold + resistance;
                if (absorbed > 0)
                {
                    armor.ConditionPermille = Math.Max(0, armor.ConditionPermille - Math.Max(1, absorbed / 3));
                    armor.Revision++;
                }
                details.Add(new ArmorDamageDetail(armor.Id, component.Type, incoming, threshold, resistance, remaining));
            }
            stoppedTotal = checked(stoppedTotal + incoming - remaining);
            finalTotal = checked(finalTotal + remaining);
        }

        BodyPartFunctionalState previous = part.FunctionalState;
        part.CurrentIntegrity = Math.Max(0, part.CurrentIntegrity - finalTotal);
        part.TraumaPermille = Math.Clamp(1000 - part.CurrentIntegrity * 1000 / part.MaximumIntegrity, 0, 1000);
        part.FunctionalState = part.TraumaPermille switch
        {
            >= 1000 => BodyPartFunctionalState.Destroyed,
            >= 700 => BodyPartFunctionalState.Crippled,
            >= 350 => BodyPartFunctionalState.Impaired,
            _ => BodyPartFunctionalState.Healthy
        };
        part.Revision++;
        int healthDamage = finalTotal == 0 ? 0 : Math.Max(1, finalTotal * Math.Max(250, part.Definition.ConsequenceWeightPermille) / 1000);
        actor.Health = Math.Max(0, actor.Health - healthDamage);
        actor.MoralePermille = Math.Max(0, actor.MoralePermille - Math.Min(300, finalTotal * 5));
        actor.Revision++;
        ApplyStatuses(actor, part, damage, finalTotal, tick);
        UpdateInjuryPresentation(actor);
        bool lethalPart = part.FunctionalState == BodyPartFunctionalState.Destroyed && part.Definition.Region is AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster or AnatomicalRegion.Torso;
        bool died = actor.Health <= 0 || lethalPart;
        if (died)
        {
            actor.Health = 0;
            actor.IsDead = true;
            actor.Action = CombatActionKind.Dying;
            actor.ActionStartedTick = tick;
            actor.ActionImpactTick = tick;
            actor.ActionEndsTick = tick + 60;
            actor.Presentation.Action = CharacterActionKind.Dying;
            actor.Presentation.ActionProgressPermille = 0;
            actor.Presentation.Velocity = FixedVector3.Zero;
            actor.Revision++;
        }
        else if (finalTotal >= 25)
        {
            actor.Action = CombatActionKind.Stagger;
            actor.ActionStartedTick = tick;
            actor.ActionImpactTick = tick;
            actor.ActionEndsTick = tick + Math.Min(36UL, (ulong)(12 + finalTotal / 2));
            actor.Presentation.Action = CharacterActionKind.Stagger;
            actor.Presentation.ActionProgressPermille = 0;
        }
        return new DamageResolution(actor.Id, part.Definition.Id, part.Definition.Region, rawTotal, stoppedTotal, finalTotal, healthDamage, previous, part.FunctionalState, died, details);
    }

    private static void ApplyStatuses(CombatActorState actor, BodyPartRuntimeState part, IReadOnlyList<DamageComponent> damage, int finalDamage, ulong tick)
    {
        if (finalDamage <= 0) return;
        foreach (DamageComponent component in damage)
        {
            CombatStatusKind? status = component.Type switch
            {
                DamageType.Cutting when finalDamage >= 12 => CombatStatusKind.Bleeding,
                DamageType.Thermal => CombatStatusKind.Burning,
                DamageType.Electrical => CombatStatusKind.Shocked,
                DamageType.Chemical or DamageType.Biological => CombatStatusKind.Poisoned,
                DamageType.Radiological => CombatStatusKind.Irradiated,
                DamageType.Sonic when finalDamage >= 15 => CombatStatusKind.Stunned,
                _ => null
            };
            if (status is not null)
            {
                actor.SetStatus(new CombatStatusState(status.Value, Math.Clamp(finalDamage * 12, 100, 1000), tick + (ulong)Math.Clamp(60 + finalDamage * 3, 60, 900), tick));
            }
        }
        if (part.FunctionalState is BodyPartFunctionalState.Crippled or BodyPartFunctionalState.Destroyed)
        {
            actor.SetStatus(new CombatStatusState(CombatStatusKind.Crippled, part.TraumaPermille, ulong.MaxValue, tick));
        }
    }

    private static void UpdateInjuryPresentation(CombatActorState actor)
    {
        int Get(AnatomicalRegion region) => actor.BodyParts.Where(part => part.Definition.Region == region).Select(static part => part.TraumaPermille).DefaultIfEmpty(0).Max();
        int head = Math.Max(Get(AnatomicalRegion.Head), Get(AnatomicalRegion.SensoryCluster));
        int torso = Math.Max(Get(AnatomicalRegion.Torso), Get(AnatomicalRegion.ExposedGrowth));
        int leftArm = Math.Max(Get(AnatomicalRegion.LeftArm), Get(AnatomicalRegion.FrontLeftLimb));
        int rightArm = Math.Max(Get(AnatomicalRegion.RightArm), Get(AnatomicalRegion.FrontRightLimb));
        int leftLeg = Math.Max(Get(AnatomicalRegion.LeftLeg), Get(AnatomicalRegion.RearLeftLimb));
        int rightLeg = Math.Max(Get(AnatomicalRegion.RightLeg), Get(AnatomicalRegion.RearRightLimb));
        int pain = Math.Clamp(actor.BodyParts.Sum(static part => part.TraumaPermille) / Math.Max(1, actor.BodyParts.Count), 0, 1000);
        actor.Presentation.Injury = new CharacterInjuryPresentation(head, torso, leftArm, rightArm, leftLeg, rightLeg, pain, 1000 - actor.StaminaPermille).Clamped();
        actor.Presentation.Revision++;
    }
}
