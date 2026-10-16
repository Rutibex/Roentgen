namespace Roentgen.Contracts;

public enum DamageType
{
    Kinetic = 0,
    Cutting = 1,
    Blunt = 2,
    Thermal = 3,
    Electrical = 4,
    Radiological = 5,
    Chemical = 6,
    Biological = 7,
    Sonic = 8
}

public enum CombatAttackKind
{
    Hitscan = 0,
    Projectile = 1,
    Melee = 2,
    Explosive = 3
}

public enum WeaponFamilyKind
{
    Unarmed = 0,
    Revolver = 1,
    SelfLoadingPistol = 2,
    Rifle = 3,
    Shotgun = 4,
    ImprovisedMelee = 5,
    Grenade = 6,
    Radiographic = 7,
    CreatureNatural = 8
}

public enum CombatActorRole
{
    Player = 0,
    Ally = 1,
    Neutral = 2,
    Hostile = 3,
    Creature = 4
}

public enum CombatActionKind
{
    None = 0,
    Aim = 1,
    Fire = 2,
    Reload = 3,
    Melee = 4,
    Block = 5,
    Throw = 6,
    Stagger = 7,
    Dying = 8,
    Dead = 9
}

public enum CombatStatusKind
{
    Bleeding = 0,
    Burning = 1,
    Shocked = 2,
    Poisoned = 3,
    Irradiated = 4,
    Stunned = 5,
    Suppressed = 6,
    Crippled = 7
}

public enum BodyPartFunctionalState
{
    Healthy = 0,
    Impaired = 1,
    Crippled = 2,
    Destroyed = 3
}

public enum ArmorLayerKind
{
    Underlayer = 0,
    Clothing = 1,
    Primary = 2,
    Outer = 3,
    PoweredFrame = 4
}

public enum CombatHitOutcome
{
    Miss = 0,
    Environment = 1,
    Actor = 2,
    Blocked = 3,
    Jammed = 4,
    Invalid = 5
}

public enum CombatFeedbackKind
{
    ShotFired = 0,
    ShotMissed = 1,
    ArmorStopped = 2,
    BodyPartHit = 3,
    InjuryApplied = 4,
    WeaponJammed = 5,
    ReloadStarted = 6,
    ReloadCompleted = 7,
    MeleeImpact = 8,
    Explosion = 9,
    ActorDied = 10,
    ActorRetreated = 11,
    ActionRejected = 12,
    StatusDamage = 13
}
