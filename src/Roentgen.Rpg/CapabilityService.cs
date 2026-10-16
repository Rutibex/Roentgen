using Roentgen.Contracts;

namespace Roentgen.Rpg;

public sealed record CharacterCapabilitySnapshot(
    StableId CharacterId,
    int Level,
    IReadOnlyDictionary<AttributeId, int> Attributes,
    IReadOnlyDictionary<SkillId, int> Skills,
    DerivedStatSnapshot DerivedStatistics,
    IReadOnlySet<string> BehaviorTags,
    ulong SourceRevision);

public sealed class ProgressionCapabilityService
{
    private readonly ProgressionRepository repository;
    private readonly BackgroundTraitCatalog backgroundTraits;
    private readonly PerkCatalog perks;
    private readonly DerivedStatCalculator derived = new();

    public ProgressionCapabilityService(ProgressionRepository repository, BackgroundTraitCatalog backgroundTraits, PerkCatalog perks)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.backgroundTraits = backgroundTraits ?? throw new ArgumentNullException(nameof(backgroundTraits));
        this.perks = perks ?? throw new ArgumentNullException(nameof(perks));
    }

    public CharacterCapabilitySnapshot Get(StableId characterId)
    {
        CharacterProgressionState state = repository.GetRequired(characterId);
        var skills = new SortedDictionary<SkillId, int>();
        foreach ((SkillId id, int value) in state.Skills.Values) skills[id] = value;
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        DerivedStatSnapshot baseDerived = derived.Calculate(state);
        var derivedValues = new SortedDictionary<DerivedStatId, int>();
        foreach ((DerivedStatId id, int value) in baseDerived.Values) derivedValues[id] = value;
        foreach (string traitId in state.TraitIds)
        {
            TraitDefinition trait = backgroundTraits.GetTrait(traitId);
            tags.UnionWith(trait.GrantedTags);
            foreach (ProgressionModifier modifier in trait.Benefits.Concat(trait.Drawbacks)) ApplyModifier(modifier, skills, derivedValues, tags);
        }
        foreach ((string perkId, int rank) in state.PerkRanks)
        {
            PerkDefinition perk = perks.Get(perkId);
            for (int index = 0; index < rank; index++)
                foreach (PerkEffectDefinition effect in perk.Effects)
                    tags.Add($"{effect.EffectType}:{effect.TargetId}:{effect.Value}");
        }
        return new(state.CharacterId, state.Level, state.Attributes.Values, skills,
            new DerivedStatSnapshot(derivedValues, state.Revision), tags, state.Revision);
    }

    public int GetEffectiveSkill(StableId characterId, SkillId skill) => Get(characterId).Skills[skill];
    public bool HasBehavior(StableId characterId, string effectType, string targetId) => Get(characterId).BehaviorTags.Any(tag => tag.StartsWith($"{effectType}:{targetId}:", StringComparison.Ordinal));
    public int SumPerkEffect(StableId characterId, string effectType, string targetId)
    {
        CharacterProgressionState state = repository.GetRequired(characterId);
        return perks.GetEffects(state, effectType).Where(x => string.Equals(x.TargetId, targetId, StringComparison.Ordinal)).Sum(x => x.Value);
    }

    private static void ApplyModifier(ProgressionModifier modifier, SortedDictionary<SkillId, int> skills, SortedDictionary<DerivedStatId, int> derived, SortedSet<string> tags)
    {
        if (modifier.Skill is SkillId skill) skills[skill] = Math.Clamp(skills.GetValueOrDefault(skill) + modifier.AdditiveValue, 0, 150);
        if (modifier.DerivedStat is DerivedStatId stat)
        {
            int value = derived.GetValueOrDefault(stat);
            value = checked(value + modifier.AdditiveValue);
            value = (int)((long)value * Math.Max(0, modifier.MultiplierPermille) / 1000L);
            derived[stat] = Math.Max(0, value);
        }
        if (modifier.Attribute is null && modifier.Skill is null && modifier.DerivedStat is null) tags.Add($"{modifier.Id}:{modifier.SourceId}:{modifier.AdditiveValue}");
    }
}
