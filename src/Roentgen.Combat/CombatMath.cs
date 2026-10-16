using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Combat;

public static class CombatMath
{
    public const long DirectionScale = 1_000_000;

    public static double Length(FixedVector3 value) => Math.Sqrt(
        (double)value.XMillimeters * value.XMillimeters +
        (double)value.YMillimeters * value.YMillimeters +
        (double)value.ZMillimeters * value.ZMillimeters);

    public static double Distance(FixedVector3 left, FixedVector3 right) => Length(right - left);

    public static FixedVector3 Normalize(FixedVector3 value)
    {
        double length = Length(value);
        if (length < 0.0001) return new FixedVector3(0, 0, -DirectionScale);
        return new FixedVector3(
            (long)Math.Round(value.XMillimeters / length * DirectionScale, MidpointRounding.AwayFromZero),
            (long)Math.Round(value.YMillimeters / length * DirectionScale, MidpointRounding.AwayFromZero),
            (long)Math.Round(value.ZMillimeters / length * DirectionScale, MidpointRounding.AwayFromZero));
    }

    public static FixedVector3 ScaleDirection(FixedVector3 normalizedDirection, long distanceMillimeters) => new(
        checked(normalizedDirection.XMillimeters * distanceMillimeters / DirectionScale),
        checked(normalizedDirection.YMillimeters * distanceMillimeters / DirectionScale),
        checked(normalizedDirection.ZMillimeters * distanceMillimeters / DirectionScale));

    public static FixedVector3 PointAlong(FixedVector3 origin, FixedVector3 normalizedDirection, long distanceMillimeters) => origin + ScaleDirection(normalizedDirection, distanceMillimeters);

    public static int DeterministicPermille(CampaignSeed campaignSeed, StableId actorId, StableId objectId, ulong tick, ulong sequence, string domain)
    {
        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.combat-roll");
        writer.WriteInt32(1);
        writer.WriteUInt64(campaignSeed.Value);
        writer.WriteStableId(actorId);
        writer.WriteStableId(objectId);
        writer.WriteUInt64(tick);
        writer.WriteUInt64(sequence);
        writer.WriteString(domain);
        StateChecksum checksum = writer.ComputeChecksum();
        return (int)(checksum.A % 1000UL);
    }

    public static FixedVector3 ApplySpread(FixedVector3 direction, int spreadPermille, CampaignSeed seed, StableId actorId, StableId weaponId, ulong tick, ulong sequence)
    {
        FixedVector3 normalized = Normalize(direction);
        if (spreadPermille <= 0) return normalized;
        int yawRoll = DeterministicPermille(seed, actorId, weaponId, tick, sequence, "spread.yaw") - 500;
        int pitchRoll = DeterministicPermille(seed, actorId, weaponId, tick, sequence, "spread.pitch") - 500;
        double yaw = yawRoll / 500.0 * spreadPermille / 1000.0 * 0.075;
        double pitch = pitchRoll / 500.0 * spreadPermille / 1000.0 * 0.075;
        double x = normalized.XMillimeters / (double)DirectionScale;
        double y = normalized.YMillimeters / (double)DirectionScale;
        double z = normalized.ZMillimeters / (double)DirectionScale;
        double cosYaw = Math.Cos(yaw);
        double sinYaw = Math.Sin(yaw);
        double rotatedX = x * cosYaw - z * sinYaw;
        double rotatedZ = x * sinYaw + z * cosYaw;
        double horizontal = Math.Sqrt(rotatedX * rotatedX + rotatedZ * rotatedZ);
        double angle = Math.Atan2(y, horizontal) + pitch;
        double cosPitch = Math.Cos(angle);
        return Normalize(new FixedVector3(
            (long)Math.Round(rotatedX / Math.Max(0.0001, horizontal) * cosPitch * DirectionScale),
            (long)Math.Round(Math.Sin(angle) * DirectionScale),
            (long)Math.Round(rotatedZ / Math.Max(0.0001, horizontal) * cosPitch * DirectionScale)));
    }

    public static int ClampPermille(int value) => Math.Clamp(value, 0, 1000);
}
