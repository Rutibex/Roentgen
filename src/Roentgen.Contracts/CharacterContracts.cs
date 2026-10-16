namespace Roentgen.Contracts;

public enum CharacterSpecies
{
    Human = 0,
    HumanoidMutant = 1,
    IndustrialQuadruped = 2,
    RoboticChassis = 3
}

public enum CharacterActionKind
{
    Idle = 0,
    Walk = 1,
    Run = 2,
    Sprint = 3,
    Crouch = 4,
    Turn = 5,
    Aim = 6,
    Reload = 7,
    Melee = 8,
    Block = 9,
    Stagger = 10,
    Fall = 11,
    Sit = 12,
    Sleep = 13,
    UseTerminal = 14,
    RepairObject = 15,
    Converse = 16,
    Climb = 17,
    Fire = 18,
    Throw = 19,
    Dying = 20,
    Dead = 21
}

public enum EquipmentGripKind
{
    Unarmed = 0,
    OneHanded = 1,
    TwoHanded = 2,
    Tool = 3,
    Shielded = 4
}

public enum CharacterAttentionKind
{
    Neutral = 0,
    ObservePoint = 1,
    TrackActor = 2,
    Converse = 3,
    InspectObject = 4,
    ReturnToWork = 5
}

public enum CharacterLodLevel
{
    Full = 0,
    Reduced = 1,
    Distant = 2
}

public enum AnatomicalRegion
{
    None = 0,
    Head = 1,
    Torso = 2,
    LeftArm = 3,
    RightArm = 4,
    LeftLeg = 5,
    RightLeg = 6,
    EquippedWeapon = 7,
    BackEquipment = 8,
    SensoryCluster = 9,
    Jaw = 10,
    FrontLeftLimb = 11,
    FrontRightLimb = 12,
    RearLeftLimb = 13,
    RearRightLimb = 14,
    ExposedGrowth = 15
}

public sealed record CharacterTargetPart(
    StableId Id,
    string Name,
    AnatomicalRegion Region,
    string BoneName,
    int RadiusMillimeters,
    int LengthMillimeters,
    int ConsequenceWeightPermille);

public sealed record CharacterAppearanceDescriptor(
    CharacterSpecies Species,
    string ArchetypeId,
    int HeightMillimeters,
    int BuildPermille,
    int AgePresentationPermille,
    int PosturePermille,
    int SkinPaletteIndex,
    int HairProfileIndex,
    int ClothingProfileIndex,
    int MarkingProfileIndex);


public readonly record struct CharacterInjuryPresentation(
    int HeadTraumaPermille,
    int TorsoTraumaPermille,
    int LeftArmImpairmentPermille,
    int RightArmImpairmentPermille,
    int LeftLegImpairmentPermille,
    int RightLegImpairmentPermille,
    int PainPermille,
    int FatiguePermille)
{
    public static CharacterInjuryPresentation None => new(0, 0, 0, 0, 0, 0, 0, 0);

    public CharacterInjuryPresentation Clamped() => new(
        Clamp(HeadTraumaPermille),
        Clamp(TorsoTraumaPermille),
        Clamp(LeftArmImpairmentPermille),
        Clamp(RightArmImpairmentPermille),
        Clamp(LeftLegImpairmentPermille),
        Clamp(RightLegImpairmentPermille),
        Clamp(PainPermille),
        Clamp(FatiguePermille));

    private static int Clamp(int value) => Math.Clamp(value, 0, 1000);
}
