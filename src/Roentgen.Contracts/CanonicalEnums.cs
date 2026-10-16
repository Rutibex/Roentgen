namespace Roentgen.Contracts;

public enum AttributeId
{
    Physique = 0,
    Awareness = 1,
    Vitality = 2,
    Presence = 3,
    Intellect = 4,
    Reflex = 5,
    Fortune = 6
}

public enum SkillId
{
    Ballistics = 0,
    DirectedEnergy = 1,
    Demolitions = 2,
    Melee = 3,
    Unarmed = 4,
    Athletics = 5,
    Stealth = 6,
    Security = 7,
    Engineering = 8,
    Science = 9,
    Medicine = 10,
    Fieldcraft = 11,
    Influence = 12,
    Commerce = 13
}

public enum ReputationDimension
{
    Trust,
    Fear,
    Respect,
    Obligation,
    IdeologicalAlignment,
    KnownCrimes
}

public enum SimulationLevel
{
    Strategic,
    Reduced,
    Full
}

public enum QuestSolutionFamily
{
    Combat,
    Stealth,
    Security,
    Engineering,
    Science,
    Medicine,
    Fieldcraft,
    Influence,
    Commerce,
    FactionLeverage,
    Exploration,
    Companion,
    Destruction,
    Repair
}
