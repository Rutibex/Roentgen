using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Entities;

public sealed class CharacterState : ICanonicalWritable
{
    public CharacterState(StableId id, CharacterAppearanceDescriptor appearance, FixedVector3 feetPosition)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Character ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(appearance);
        Id = id;
        Appearance = appearance;
        FeetPosition = feetPosition;
        FacingYawUnits = 0;
        Action = CharacterActionKind.Idle;
        Attention = CharacterAttentionKind.Neutral;
        Lod = CharacterLodLevel.Full;
    }

    public StableId Id { get; }
    public CharacterAppearanceDescriptor Appearance { get; }
    public FixedVector3 FeetPosition { get; set; }
    public FixedVector3 Velocity { get; set; }
    public int FacingYawUnits { get; set; }
    public CharacterActionKind Action { get; set; }
    public int ActionProgressPermille { get; set; }
    public int LocomotionPhasePermille { get; set; }
    public CharacterAttentionKind Attention { get; set; }
    public StableId AttentionTargetId { get; set; }
    public FixedVector3 AttentionTargetPosition { get; set; }
    public StableId EquippedObjectId { get; set; }
    public string EquipmentPoseProfileId { get; set; } = "unarmed";
    public CharacterLodLevel Lod { get; set; }
    public CharacterInjuryPresentation Injury { get; set; } = CharacterInjuryPresentation.None;
    public ulong Revision { get; set; }

    public StateChecksum ComputeChecksum()
    {
        var writer = new CanonicalWriter();
        WriteCanonical(writer);
        return writer.ComputeChecksum();
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStableId(Id);
        writer.WriteInt32((int)Appearance.Species);
        writer.WriteString(Appearance.ArchetypeId);
        writer.WriteInt32(Appearance.HeightMillimeters);
        writer.WriteInt32(Appearance.BuildPermille);
        writer.WriteInt32(Appearance.AgePresentationPermille);
        writer.WriteInt32(Appearance.PosturePermille);
        writer.WriteInt32(Appearance.SkinPaletteIndex);
        writer.WriteInt32(Appearance.HairProfileIndex);
        writer.WriteInt32(Appearance.ClothingProfileIndex);
        writer.WriteInt32(Appearance.MarkingProfileIndex);
        WriteVector(writer, FeetPosition);
        WriteVector(writer, Velocity);
        writer.WriteInt32(FacingYawUnits);
        writer.WriteInt32((int)Action);
        writer.WriteInt32(ActionProgressPermille);
        writer.WriteInt32(LocomotionPhasePermille);
        writer.WriteInt32((int)Attention);
        writer.WriteStableId(AttentionTargetId);
        WriteVector(writer, AttentionTargetPosition);
        writer.WriteStableId(EquippedObjectId);
        writer.WriteString(EquipmentPoseProfileId);
        writer.WriteInt32((int)Lod);
        WriteInjury(writer, Injury);
        writer.WriteUInt64(Revision);
    }

    private static void WriteInjury(CanonicalWriter writer, CharacterInjuryPresentation raw)
    {
        CharacterInjuryPresentation value = raw.Clamped();
        writer.WriteInt32(value.HeadTraumaPermille);
        writer.WriteInt32(value.TorsoTraumaPermille);
        writer.WriteInt32(value.LeftArmImpairmentPermille);
        writer.WriteInt32(value.RightArmImpairmentPermille);
        writer.WriteInt32(value.LeftLegImpairmentPermille);
        writer.WriteInt32(value.RightLegImpairmentPermille);
        writer.WriteInt32(value.PainPermille);
        writer.WriteInt32(value.FatiguePermille);
    }

    private static void WriteVector(CanonicalWriter writer, FixedVector3 value)
    {
        writer.WriteInt64(value.XMillimeters);
        writer.WriteInt64(value.YMillimeters);
        writer.WriteInt64(value.ZMillimeters);
    }
}

