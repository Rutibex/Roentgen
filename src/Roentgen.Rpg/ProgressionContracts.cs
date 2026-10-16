using Roentgen.Contracts;

namespace Roentgen.Rpg;

public enum DerivedStatId
{
    MaximumHealth = 0,
    MaximumStamina = 1,
    CarryCapacityGrams = 2,
    MaximumFocusMilliunits = 3,
    FocusRecoveryPerSecondMilliunits = 4,
    Initiative = 5,
    CriticalReserveMilliunits = 6,
    KnockdownResistancePermille = 7,
    ReloadSpeedPermille = 8,
    MovementSpeedPermille = 9,
    SkillPointsPerLevel = 10
}

public enum SkillCheckDifficulty
{
    Routine = 20,
    Trained = 35,
    Difficult = 50,
    Expert = 65,
    Exceptional = 80,
    Legendary = 95
}

public enum SkillCheckOutcome
{
    Failure = 0,
    CostlySuccess = 1,
    Success = 2,
    ExceptionalSuccess = 3
}

public enum ProgressionPointKind
{
    Skill = 0,
    Perk = 1,
    Attribute = 2
}

public enum ExperienceRepeatPolicy
{
    Once = 0,
    OncePerSourceRevision = 1,
    Repeatable = 2
}

public enum PerkRequirementKind
{
    Level = 0,
    Attribute = 1,
    Skill = 2,
    PriorPerk = 3,
    Background = 4,
    Trait = 5,
    FactionTraining = 6,
    Discovery = 7
}

public enum PerkBalanceClass
{
    Behavior = 0,
    OptionUnlock = 1,
    Hybrid = 2,
    Numerical = 3
}

public enum CharacterCreationStage
{
    Uninitialized = 0,
    Attributes = 1,
    Specializations = 2,
    Background = 3,
    Traits = 4,
    History = 5,
    Equipment = 6,
    Review = 7,
    Finalized = 8
}

public enum NpcProgressionArchetype
{
    Civilian = 0,
    Guard = 1,
    Raider = 2,
    Technician = 3,
    Medic = 4,
    Scout = 5,
    Merchant = 6,
    EliteCombatant = 7,
    Creature = 8,
    Robot = 9
}

public sealed record ProgressionModifier(
    string Id,
    string SourceId,
    AttributeId? Attribute,
    SkillId? Skill,
    DerivedStatId? DerivedStat,
    int AdditiveValue,
    int MultiplierPermille,
    bool Permanent);

public sealed record StartingEquipmentGrant(string PackageId, IReadOnlyList<string> ItemDefinitionIds);

public sealed record PersonalHistoryTag(string Id, string Value);

public static class ProgressionRules
{
    public const int MinimumAttribute = 1;
    public const int MaximumAttribute = 10;
    public const int BaseAttribute = 4;
    public const int CreationAttributePoints = 14;
    public const int MinimumSkill = 0;
    public const int MaximumSkill = 100;
    public const int SpecializedSkillBonus = 15;
    public const int MaximumLevel = 50;
    public const int TicksPerSecond = 60;

    public static IReadOnlyList<AttributeId> AttributeOrder { get; } = Enum.GetValues<AttributeId>();
    public static IReadOnlyList<SkillId> SkillOrder { get; } = Enum.GetValues<SkillId>();
}
