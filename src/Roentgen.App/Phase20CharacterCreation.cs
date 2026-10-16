using Roentgen.Contracts;
using Roentgen.Rpg;

namespace Roentgen.App;

internal sealed class Phase20CharacterCreation
{
    private static readonly IReadOnlyDictionary<AttributeId, int> RecommendedAttributes = new Dictionary<AttributeId, int>
    {
        [AttributeId.Physique] = 6,
        [AttributeId.Awareness] = 7,
        [AttributeId.Vitality] = 6,
        [AttributeId.Presence] = 5,
        [AttributeId.Intellect] = 6,
        [AttributeId.Reflex] = 7,
        [AttributeId.Fortune] = 5
    };
    private readonly Dictionary<AttributeId, int> attributes = RecommendedAttributes.ToDictionary(static pair => pair.Key, static pair => pair.Value);
    private readonly List<SkillId> specializedSkills = [SkillId.Ballistics, SkillId.Engineering, SkillId.Science];
    private readonly List<string> traitIds = ["methodical", "adaptable"];

    private string characterName = "Field Operator";
    public string CharacterName
    {
        get => characterName;
        set
        {
            string cleaned = new string((value ?? string.Empty).Where(ch => !char.IsControl(ch)).ToArray()).TrimStart();
            characterName = cleaned.Length <= 24 ? cleaned : cleaned[..24];
        }
    }
    public IReadOnlyDictionary<AttributeId, int> Attributes => attributes;
    public IReadOnlyList<SkillId> SpecializedSkills => specializedSkills;
    public string BackgroundId { get; private set; } = "relay-technician";
    public IReadOnlyList<string> TraitIds => traitIds;
    public string EquipmentPackageId => Phase21RpgContent.Backgrounds.First(value => value.Id == BackgroundId).Equipment.PackageId;
    public int AttributePointsRemaining => ProgressionRules.CreationAttributePoints - (attributes.Values.Sum() - ProgressionRules.BaseAttribute * attributes.Count);

    public bool AdjustAttribute(AttributeId id, int delta)
    {
        int current = attributes[id];
        int next = current + delta;
        if (next is < ProgressionRules.MinimumAttribute or > ProgressionRules.MaximumAttribute) return false;
        if (delta > 0 && AttributePointsRemaining < delta) return false;
        attributes[id] = next;
        return true;
    }

    public bool CycleSpecialization(SkillId skill)
    {
        if (specializedSkills.Contains(skill))
        {
            specializedSkills.Remove(skill);
            return true;
        }
        if (specializedSkills.Count >= 3) return false;
        specializedSkills.Add(skill);
        specializedSkills.Sort();
        return true;
    }

    public void ResetAttributes()
    {
        foreach (AttributeId id in ProgressionRules.AttributeOrder) attributes[id] = ProgressionRules.BaseAttribute;
    }

    public void ApplyRecommendedAttributes()
    {
        foreach ((AttributeId id, int value) in RecommendedAttributes) attributes[id] = value;
    }

    public void SetBackground(string backgroundId)
    {
        _ = Phase21RpgContent.Backgrounds.First(value => value.Id == backgroundId);
        BackgroundId = backgroundId;
    }

    public bool ToggleTrait(string traitId)
    {
        BackgroundTraitCatalog catalog = Phase21RpgContent.CreateBackgroundTraitCatalog();
        _ = catalog.GetTrait(traitId);
        if (traitIds.Contains(traitId))
        {
            traitIds.Remove(traitId);
            return true;
        }
        if (traitIds.Count >= 2) return false;
        if (traitIds.Count == 1 && !catalog.AreCompatible(traitIds[0], traitId)) return false;
        traitIds.Add(traitId);
        traitIds.Sort(StringComparer.Ordinal);
        return true;
    }

    public CharacterCreationValidation Validate(StableId playerId)
    {
        CharacterCreationDraft draft = BuildDraft(playerId);
        return new CharacterCreationService(Phase21RpgContent.CreateBackgroundTraitCatalog()).Validate(draft);
    }

    public CharacterCreationDraft BuildDraft(StableId playerId)
    {
        var draft = new CharacterCreationDraft(playerId, 0x5048415345323000UL);
        foreach ((AttributeId id, int value) in attributes) _ = draft.TrySetAttribute(id, value);
        draft.SetSpecializations(specializedSkills);
        draft.SetBackground(BackgroundId);
        draft.SetTraits(traitIds);
        draft.SetHistory([
            new PersonalHistoryTag("prior-work", Phase21RpgContent.Backgrounds.First(value => value.Id == BackgroundId).Name),
            new PersonalHistoryTag("diagnostic-exposure", "survived a failed inspection array")
        ]);
        draft.SetEquipment(EquipmentPackageId);
        return draft;
    }
}
