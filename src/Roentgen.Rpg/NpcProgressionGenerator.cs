using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Rpg;

public sealed class NpcProgressionGenerator
{
    public CharacterProgressionState Generate(StableId characterId, NpcProgressionArchetype archetype, int level, ulong seed)
    {
        level = Math.Clamp(level, 1, ProgressionRules.MaximumLevel);
        var attributes = new AttributeSet();
        foreach (AttributeId id in AttributePriority(archetype))
        {
            int boosts = 1 + (int)((seed >> ((int)id * 5)) & 1UL);
            for (int i = 0; i < boosts; i++) attributes.TryAdjust(id, 1);
        }
        SkillId[] specialties = SkillPriority(archetype).Take(3).ToArray();
        SkillSet skills = Phase12SkillDefinitions.CreateStartingSkills(attributes, specialties);
        int levelBonus = (level - 1) / 2;
        foreach (SkillId skill in SkillPriority(archetype)) skills.Set(skill, skills[skill] + levelBonus);
        var state = new CharacterProgressionState(characterId, attributes, skills);
        state.ConfigureCreation(specialties, $"npc-{archetype.ToString().ToLowerInvariant()}", ["npc-generated", "npc-role"], [new("archetype", archetype.ToString()), new("seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        var xp = new ExperienceSystem();
        int awardIndex = 0;
        while (state.Level < level)
        {
            long needed = ExperienceSystem.ExperienceToNextLevel(state.Level);
            int amount = needed > int.MaxValue ? int.MaxValue : (int)needed;
            StableId awardId = StableIdFactory.Create("npc-generation", $"{seed}:level:{awardIndex++}");
            xp.Grant(state, new(awardId, "npc-generation", archetype.ToString(), amount, ExperienceRepeatPolicy.Once, 0));
        }
        return state;
    }

    private static IEnumerable<AttributeId> AttributePriority(NpcProgressionArchetype archetype) => archetype switch
    {
        NpcProgressionArchetype.Guard or NpcProgressionArchetype.Raider => [AttributeId.Physique, AttributeId.Vitality, AttributeId.Reflex, AttributeId.Awareness],
        NpcProgressionArchetype.Technician or NpcProgressionArchetype.Robot => [AttributeId.Intellect, AttributeId.Awareness, AttributeId.Reflex, AttributeId.Vitality],
        NpcProgressionArchetype.Medic => [AttributeId.Intellect, AttributeId.Awareness, AttributeId.Presence, AttributeId.Vitality],
        NpcProgressionArchetype.Scout or NpcProgressionArchetype.Creature => [AttributeId.Awareness, AttributeId.Reflex, AttributeId.Vitality, AttributeId.Physique],
        NpcProgressionArchetype.Merchant => [AttributeId.Presence, AttributeId.Intellect, AttributeId.Awareness, AttributeId.Fortune],
        NpcProgressionArchetype.EliteCombatant => [AttributeId.Reflex, AttributeId.Awareness, AttributeId.Physique, AttributeId.Vitality],
        _ => [AttributeId.Vitality, AttributeId.Awareness, AttributeId.Presence, AttributeId.Fortune]
    };

    private static IEnumerable<SkillId> SkillPriority(NpcProgressionArchetype archetype) => archetype switch
    {
        NpcProgressionArchetype.Guard => [SkillId.Ballistics, SkillId.Melee, SkillId.Athletics, SkillId.Fieldcraft],
        NpcProgressionArchetype.Raider => [SkillId.Ballistics, SkillId.Melee, SkillId.Demolitions, SkillId.Stealth],
        NpcProgressionArchetype.Technician => [SkillId.Engineering, SkillId.Science, SkillId.Security, SkillId.DirectedEnergy],
        NpcProgressionArchetype.Medic => [SkillId.Medicine, SkillId.Science, SkillId.Influence, SkillId.Fieldcraft],
        NpcProgressionArchetype.Scout => [SkillId.Fieldcraft, SkillId.Stealth, SkillId.Ballistics, SkillId.Athletics],
        NpcProgressionArchetype.Merchant => [SkillId.Commerce, SkillId.Influence, SkillId.Security, SkillId.Ballistics],
        NpcProgressionArchetype.EliteCombatant => [SkillId.Ballistics, SkillId.Melee, SkillId.Demolitions, SkillId.Athletics],
        NpcProgressionArchetype.Creature => [SkillId.Unarmed, SkillId.Athletics, SkillId.Fieldcraft, SkillId.Stealth],
        NpcProgressionArchetype.Robot => [SkillId.DirectedEnergy, SkillId.Ballistics, SkillId.Engineering, SkillId.Security],
        _ => [SkillId.Fieldcraft, SkillId.Influence, SkillId.Athletics, SkillId.Commerce]
    };
}
