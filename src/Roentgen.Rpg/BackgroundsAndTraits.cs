using Roentgen.Contracts;

namespace Roentgen.Rpg;

public sealed record BackgroundDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyDictionary<AttributeId, int> AttributeAdjustments,
    IReadOnlyDictionary<SkillId, int> SkillAdjustments,
    IReadOnlyList<string> KnowledgeTags,
    IReadOnlyList<string> DialogueTags,
    StartingEquipmentGrant Equipment,
    string StartingBenefitId);

public sealed record TraitDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<ProgressionModifier> Benefits,
    IReadOnlyList<ProgressionModifier> Drawbacks,
    IReadOnlyList<string> IncompatibleTraitIds,
    IReadOnlyList<string> GrantedTags);

public sealed class BackgroundTraitCatalog
{
    private readonly SortedDictionary<string, BackgroundDefinition> backgrounds;
    private readonly SortedDictionary<string, TraitDefinition> traits;
    public BackgroundTraitCatalog(IEnumerable<BackgroundDefinition> backgrounds, IEnumerable<TraitDefinition> traits)
    {
        this.backgrounds = new(backgrounds.ToDictionary(x => x.Id, StringComparer.Ordinal), StringComparer.Ordinal);
        this.traits = new(traits.ToDictionary(x => x.Id, StringComparer.Ordinal), StringComparer.Ordinal);
    }
    public IReadOnlyCollection<BackgroundDefinition> Backgrounds => backgrounds.Values;
    public IReadOnlyCollection<TraitDefinition> Traits => traits.Values;
    public BackgroundDefinition GetBackground(string id) => backgrounds.TryGetValue(id, out BackgroundDefinition? value) ? value : throw new KeyNotFoundException($"Background '{id}' is unknown.");
    public TraitDefinition GetTrait(string id) => traits.TryGetValue(id, out TraitDefinition? value) ? value : throw new KeyNotFoundException($"Trait '{id}' is unknown.");
    public bool AreCompatible(string first, string second) => !GetTrait(first).IncompatibleTraitIds.Contains(second, StringComparer.Ordinal) && !GetTrait(second).IncompatibleTraitIds.Contains(first, StringComparer.Ordinal);
}
