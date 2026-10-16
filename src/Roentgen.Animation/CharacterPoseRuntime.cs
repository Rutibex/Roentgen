using System.Numerics;
using Roentgen.Contracts;

namespace Roentgen.Animation;

public sealed record CharacterPoseInput(
    CharacterActionKind Action,
    int ActionProgressPermille,
    int LocomotionPhasePermille,
    Vector3 VelocityMetersPerSecond,
    int FacingYawUnits,
    Vector3 AimDirection,
    Vector3 LookDirection,
    CharacterInjuryPresentation Injury,
    string EquipmentProfileId,
    CharacterLodLevel Lod,
    float LeftGroundHeightMeters = 0f,
    float RightGroundHeightMeters = 0f,
    Vector3 LeftGroundNormal = default,
    Vector3 RightGroundNormal = default);

public sealed class CharacterPoseRuntime
{
    public PoseBuffer Evaluate(GeneratedCharacterProduct character, CharacterPoseInput input)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(input);
        var pose = new PoseBuffer(character.Skeleton);
        if (character.Skeleton.IndexOf("front_l_upper") >= 0)
        {
            ApplyQuadrupedLocomotion(pose, input);
            ApplyQuadrupedAttention(pose, input);
            return pose;
        }

        ApplyLocomotion(pose, input);
        ApplyAction(pose, character, input);
        ApplyAimAndLook(pose, input);
        ApplyInjury(pose, input.Injury);
        if (input.Lod != CharacterLodLevel.Distant)
        {
            ApplyFootPlacement(pose, input);
        }

        return pose;
    }


    private static void ApplyQuadrupedLocomotion(PoseBuffer pose, CharacterPoseInput input)
    {
        float phase = input.LocomotionPhasePermille / 1000f * MathF.Tau;
        float speed = new Vector2(input.VelocityMetersPerSecond.X, input.VelocityMetersPerSecond.Z).Length();
        float amplitude = Math.Clamp(speed / 4f, 0.15f, 1f);
        ApplyQuadrupedLeg(pose, "front_l", MathF.Sin(phase), amplitude);
        ApplyQuadrupedLeg(pose, "front_r", MathF.Sin(phase + MathF.PI), amplitude);
        ApplyQuadrupedLeg(pose, "rear_l", MathF.Sin(phase + MathF.PI), amplitude);
        ApplyQuadrupedLeg(pose, "rear_r", MathF.Sin(phase), amplitude);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("spine_mid"), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Sin(phase) * 0.09f * amplitude), new Vector3(0f, MathF.Abs(MathF.Sin(phase)) * 0.025f * amplitude, 0f), 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("tail"), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Sin(phase * 0.5f) * 0.35f), Vector3.Zero, 1f);
    }

    private static void ApplyQuadrupedLeg(PoseBuffer pose, string prefix, float phaseValue, float amplitude)
    {
        int upper = pose.Skeleton.IndexOf($"{prefix}_upper");
        int lower = pose.Skeleton.IndexOf($"{prefix}_lower");
        float swing = phaseValue * 0.55f * amplitude;
        float knee = MathF.Max(0f, -phaseValue) * 0.75f * amplitude;
        pose.ApplyAdditive(upper, Quaternion.CreateFromAxisAngle(Vector3.UnitX, swing), Vector3.Zero, 1f);
        pose.ApplyAdditive(lower, Quaternion.CreateFromAxisAngle(Vector3.UnitX, knee), Vector3.Zero, 1f);
    }

    private static void ApplyQuadrupedAttention(PoseBuffer pose, CharacterPoseInput input)
    {
        Vector3 direction = input.LookDirection.LengthSquared() > 0.0001f ? Vector3.Normalize(input.LookDirection) : -Vector3.UnitZ;
        float yaw = Math.Clamp(MathF.Atan2(direction.X, -direction.Z), -0.8f, 0.8f);
        float pitch = Math.Clamp(-MathF.Asin(Math.Clamp(direction.Y, -1f, 1f)), -0.45f, 0.45f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("neck"), Quaternion.CreateFromYawPitchRoll(yaw * 0.45f, pitch * 0.4f, 0f), Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("head"), Quaternion.CreateFromYawPitchRoll(yaw * 0.55f, pitch * 0.6f, 0f), Vector3.Zero, 1f);
        if (input.Action == CharacterActionKind.Melee)
        {
            float strike = MathF.Sin(input.ActionProgressPermille / 1000f * MathF.PI);
            pose.ApplyAdditive(pose.Skeleton.IndexOf("jaw"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, strike * 0.65f), Vector3.Zero, 1f);
        }
    }

    private static void ApplyLocomotion(PoseBuffer pose, CharacterPoseInput input)
    {
        float phase = input.LocomotionPhasePermille / 1000f * MathF.Tau;
        float speed = new Vector2(input.VelocityMetersPerSecond.X, input.VelocityMetersPerSecond.Z).Length();
        float actionScale = input.Action switch
        {
            CharacterActionKind.Run => 1.25f,
            CharacterActionKind.Sprint => 1.55f,
            CharacterActionKind.Crouch => 0.65f,
            _ => Math.Clamp(speed / 4.2f, 0f, 1f)
        };
        float swing = MathF.Sin(phase) * 0.55f * actionScale;
        float knee = MathF.Max(0f, MathF.Sin(phase + MathF.PI * 0.5f)) * 0.45f * actionScale;
        int upperArmLeft = pose.Skeleton.IndexOf("upper_arm_l");
        int upperArmRight = pose.Skeleton.IndexOf("upper_arm_r");
        int thighLeft = pose.Skeleton.IndexOf("thigh_l");
        int thighRight = pose.Skeleton.IndexOf("thigh_r");
        int shinLeft = pose.Skeleton.IndexOf("shin_l");
        int shinRight = pose.Skeleton.IndexOf("shin_r");
        int pelvis = pose.Skeleton.IndexOf("pelvis");
        pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -swing * 0.75f), Vector3.Zero, 1f);
        pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, swing * 0.75f), Vector3.Zero, 1f);
        pose.ApplyAdditive(thighLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, swing), Vector3.Zero, 1f);
        pose.ApplyAdditive(thighRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -swing), Vector3.Zero, 1f);
        pose.ApplyAdditive(shinLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, knee), Vector3.Zero, 1f);
        pose.ApplyAdditive(shinRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.Max(0f, -MathF.Sin(phase + MathF.PI * 0.5f)) * 0.45f * actionScale), Vector3.Zero, 1f);
        pose.ApplyAdditive(pelvis, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -swing * 0.12f), new Vector3(0f, MathF.Abs(MathF.Sin(phase)) * 0.025f * actionScale, 0f), 1f);
        if (speed < 0.05f && input.Action == CharacterActionKind.Idle)
        {
            float breath = MathF.Sin(phase * 0.5f) * 0.008f;
            pose.ApplyAdditive(pose.Skeleton.IndexOf("chest"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, breath * 0.35f), new Vector3(0f, breath, 0f), 1f);
        }

        if (input.Action == CharacterActionKind.Crouch)
        {
            pose.ApplyAdditive(pelvis, Quaternion.Identity, new Vector3(0f, -0.38f, 0.08f), 1f);
            pose.ApplyAdditive(thighLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.65f), Vector3.Zero, 1f);
            pose.ApplyAdditive(thighRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.65f), Vector3.Zero, 1f);
            pose.ApplyAdditive(shinLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.85f), Vector3.Zero, 1f);
            pose.ApplyAdditive(shinRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.85f), Vector3.Zero, 1f);
        }
    }

    private static void ApplyAction(PoseBuffer pose, GeneratedCharacterProduct character, CharacterPoseInput input)
    {
        float progress = Math.Clamp(input.ActionProgressPermille / 1000f, 0f, 1f);
        int chest = pose.Skeleton.IndexOf("chest");
        int upperArmLeft = pose.Skeleton.IndexOf("upper_arm_l");
        int upperArmRight = pose.Skeleton.IndexOf("upper_arm_r");
        int forearmLeft = pose.Skeleton.IndexOf("forearm_l");
        int forearmRight = pose.Skeleton.IndexOf("forearm_r");
        switch (input.Action)
        {
            case CharacterActionKind.Aim:
                ApplyTwoHandedGrip(pose, upperArmLeft, upperArmRight, forearmLeft, forearmRight, 1f);
                break;
            case CharacterActionKind.Fire:
                ApplyTwoHandedGrip(pose, upperArmLeft, upperArmRight, forearmLeft, forearmRight, 1f);
                float recoil = MathF.Sin(Math.Clamp(progress * 2f, 0f, 1f) * MathF.PI);
                pose.ApplyAdditive(chest, Quaternion.CreateFromAxisAngle(Vector3.UnitX, recoil * 0.08f), new Vector3(0f, 0f, recoil * 0.04f), 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, recoil * 0.12f), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Throw:
                float throwArc = MathF.Sin(progress * MathF.PI);
                pose.ApplyAdditive(chest, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.35f * throwArc), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromYawPitchRoll(-0.6f * throwArc, -1.35f * throwArc, 0.2f), Vector3.Zero, 1f);
                pose.ApplyAdditive(forearmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.8f * throwArc), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Reload:
                ApplyTwoHandedGrip(pose, upperArmLeft, upperArmRight, forearmLeft, forearmRight, 0.7f);
                float reloadArc = MathF.Sin(progress * MathF.PI);
                pose.ApplyAdditive(forearmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -1.05f * reloadArc), new Vector3(-0.08f * reloadArc, -0.12f * reloadArc, 0f), 1f);
                break;
            case CharacterActionKind.Melee:
                float strike = MathF.Sin(progress * MathF.PI);
                pose.ApplyAdditive(chest, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.45f * strike), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromYawPitchRoll(-0.7f * strike, -1.15f * strike, 0.25f), Vector3.Zero, 1f);
                pose.ApplyAdditive(forearmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.55f * strike), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Block:
                pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromYawPitchRoll(-0.35f, -0.9f, -0.2f), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromYawPitchRoll(0.35f, -0.9f, 0.2f), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Stagger:
                float stagger = MathF.Sin(progress * MathF.PI);
                pose.ApplyAdditive(pose.Skeleton.IndexOf("pelvis"), Quaternion.CreateFromYawPitchRoll(0.18f * stagger, 0f, -0.12f * stagger), new Vector3(0f, -0.08f * stagger, 0.12f * stagger), 1f);
                pose.ApplyAdditive(chest, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f * stagger), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.45f * stagger), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.45f * stagger), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Fall:
            case CharacterActionKind.Dying:
                pose.ApplyAdditive(pose.Skeleton.IndexOf("root"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.46f * progress), new Vector3(0f, -0.45f * progress, 0f), 1f);
                pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.8f * progress), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.8f * progress), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Dead:
                pose.ApplyAdditive(pose.Skeleton.IndexOf("root"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.46f), new Vector3(0f, -0.45f, 0f), 1f);
                pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.8f), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.8f), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Sleep:
                pose.ApplyAdditive(pose.Skeleton.IndexOf("root"), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f), new Vector3(0f, -0.72f, 0f), 1f);
                pose.ApplyAdditive(pose.Skeleton.IndexOf("head"), Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.25f), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Climb:
                float climbCycle = MathF.Sin(progress * MathF.Tau);
                pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -1.55f + climbCycle * 0.25f), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -1.55f - climbCycle * 0.25f), Vector3.Zero, 1f);
                pose.ApplyAdditive(pose.Skeleton.IndexOf("thigh_l"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.55f - climbCycle * 0.35f), Vector3.Zero, 1f);
                pose.ApplyAdditive(pose.Skeleton.IndexOf("thigh_r"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.55f + climbCycle * 0.35f), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.UseTerminal:
            case CharacterActionKind.RepairObject:
                pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -1.1f), Vector3.Zero, 1f);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -1.0f), Vector3.Zero, 1f);
                pose.ApplyAdditive(forearmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.65f), Vector3.Zero, 1f);
                pose.ApplyAdditive(forearmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.65f), Vector3.Zero, 1f);
                break;
            case CharacterActionKind.Sit:
                ApplySitting(pose);
                break;
            case CharacterActionKind.Converse:
                float gesture = MathF.Sin(progress * MathF.Tau);
                pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromYawPitchRoll(0.2f * gesture, -0.35f, 0.15f), Vector3.Zero, 0.8f);
                pose.ApplyAdditive(forearmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.65f), Vector3.Zero, 0.8f);
                pose.ApplyAdditive(pose.Skeleton.IndexOf("jaw"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.08f + MathF.Abs(MathF.Sin(progress * MathF.Tau * 2f)) * 0.12f), Vector3.Zero, 1f);
                break;
        }

        _ = character;
    }

    private static void ApplyTwoHandedGrip(PoseBuffer pose, int upperArmLeft, int upperArmRight, int forearmLeft, int forearmRight, float amount)
    {
        pose.ApplyAdditive(upperArmRight, Quaternion.CreateFromYawPitchRoll(0.15f, -1.15f, 0.2f), Vector3.Zero, amount);
        pose.ApplyAdditive(forearmRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.55f), Vector3.Zero, amount);
        pose.ApplyAdditive(upperArmLeft, Quaternion.CreateFromYawPitchRoll(-0.55f, -1.05f, -0.45f), Vector3.Zero, amount);
        pose.ApplyAdditive(forearmLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.85f), Vector3.Zero, amount);
    }

    private static void ApplySitting(PoseBuffer pose)
    {
        int pelvis = pose.Skeleton.IndexOf("pelvis");
        int thighLeft = pose.Skeleton.IndexOf("thigh_l");
        int thighRight = pose.Skeleton.IndexOf("thigh_r");
        int shinLeft = pose.Skeleton.IndexOf("shin_l");
        int shinRight = pose.Skeleton.IndexOf("shin_r");
        pose.ApplyAdditive(pelvis, Quaternion.Identity, new Vector3(0f, -0.58f, 0f), 1f);
        pose.ApplyAdditive(thighLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -1.42f), Vector3.Zero, 1f);
        pose.ApplyAdditive(thighRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -1.42f), Vector3.Zero, 1f);
        pose.ApplyAdditive(shinLeft, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1.35f), Vector3.Zero, 1f);
        pose.ApplyAdditive(shinRight, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1.35f), Vector3.Zero, 1f);
    }

    private static void ApplyAimAndLook(PoseBuffer pose, CharacterPoseInput input)
    {
        Vector3 direction = input.LookDirection.LengthSquared() > 0.0001f ? Vector3.Normalize(input.LookDirection) : Vector3.UnitZ;
        float yaw = Math.Clamp(MathF.Atan2(direction.X, direction.Z), -1.15f, 1.15f);
        float pitch = Math.Clamp(-MathF.Asin(Math.Clamp(direction.Y, -1f, 1f)), -0.75f, 0.75f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("chest"), Quaternion.CreateFromYawPitchRoll(yaw * 0.22f, pitch * 0.18f, 0f), Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("neck"), Quaternion.CreateFromYawPitchRoll(yaw * 0.35f, pitch * 0.3f, 0f), Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("head"), Quaternion.CreateFromYawPitchRoll(yaw * 0.43f, pitch * 0.52f, 0f), Vector3.Zero, 1f);
        Quaternion eyeRotation = Quaternion.CreateFromYawPitchRoll(yaw * 0.18f, pitch * 0.18f, 0f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("eye_l"), eyeRotation, Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("eye_r"), eyeRotation, Vector3.Zero, 1f);
    }

    private static void ApplyInjury(PoseBuffer pose, CharacterInjuryPresentation raw)
    {
        CharacterInjuryPresentation injury = raw.Clamped();
        float leftArm = injury.LeftArmImpairmentPermille / 1000f;
        float rightArm = injury.RightArmImpairmentPermille / 1000f;
        float leftLeg = injury.LeftLegImpairmentPermille / 1000f;
        float rightLeg = injury.RightLegImpairmentPermille / 1000f;
        float torso = injury.TorsoTraumaPermille / 1000f;
        pose.ApplyAdditive(pose.Skeleton.IndexOf("chest"), Quaternion.CreateFromAxisAngle(Vector3.UnitX, torso * 0.25f), new Vector3(0f, -torso * 0.05f, 0f), 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("upper_arm_l"), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -leftArm * 0.65f), Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("upper_arm_r"), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rightArm * 0.65f), Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("thigh_l"), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, leftLeg * 0.12f), Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("thigh_r"), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -rightLeg * 0.12f), Vector3.Zero, 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("pelvis"), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (rightLeg - leftLeg) * 0.09f), Vector3.Zero, 1f);
    }

    private static void ApplyFootPlacement(PoseBuffer pose, CharacterPoseInput input)
    {
        float left = Math.Clamp(input.LeftGroundHeightMeters, -0.3f, 0.3f);
        float right = Math.Clamp(input.RightGroundHeightMeters, -0.3f, 0.3f);
        float pelvisOffset = Math.Min(left, right);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("pelvis"), Quaternion.Identity, new Vector3(0f, pelvisOffset, 0f), 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("foot_l"), QuaternionExtensions.FromToRotationSafe(Vector3.UnitY, SafeNormal(input.LeftGroundNormal)), new Vector3(0f, left - pelvisOffset, 0f), 1f);
        pose.ApplyAdditive(pose.Skeleton.IndexOf("foot_r"), QuaternionExtensions.FromToRotationSafe(Vector3.UnitY, SafeNormal(input.RightGroundNormal)), new Vector3(0f, right - pelvisOffset, 0f), 1f);
    }

    private static Vector3 SafeNormal(Vector3 value) => value.LengthSquared() < 0.0001f ? Vector3.UnitY : Vector3.Normalize(value);
}

internal static class QuaternionExtensions
{
    public static Quaternion FromToRotationSafe(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        float dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);
        if (dot > 0.9999f)
        {
            return Quaternion.Identity;
        }

        if (dot < -0.9999f)
        {
            Vector3 axis = MathF.Abs(from.X) < 0.9f ? Vector3.Normalize(Vector3.Cross(from, Vector3.UnitX)) : Vector3.Normalize(Vector3.Cross(from, Vector3.UnitY));
            return Quaternion.CreateFromAxisAngle(axis, MathF.PI);
        }

        Vector3 cross = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(cross, 1f + dot));
    }
}
