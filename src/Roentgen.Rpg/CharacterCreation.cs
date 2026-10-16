using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Rpg;

public sealed record CharacterCreationRecommendation(
    IReadOnlyDictionary<AttributeId, int> Attributes,
    IReadOnlyList<SkillId> SpecializedSkills,
    string BackgroundId,
    IReadOnlyList<string> TraitIds,
    string EquipmentPackageId,
    IReadOnlyList<string> Explanations);

public sealed record CharacterCreationValidation(bool IsValid, IReadOnlyList<string> Errors);
public sealed record CharacterCreationResult(CharacterProgressionState Progression, StartingEquipmentGrant Equipment, StateChecksum CreationChecksum);

public sealed class CharacterCreationDraft : ICanonicalWritable
{
    private readonly SortedSet<SkillId> specializedSkills = [];
    private readonly SortedSet<string> traitIds = new(StringComparer.Ordinal);
    private readonly List<PersonalHistoryTag> history = [];

    public CharacterCreationDraft(StableId characterId, ulong personalHistorySeed)
    {
        if (characterId.IsEmpty) throw new ArgumentException("Character ID cannot be empty.", nameof(characterId));
        CharacterId = characterId;
        PersonalHistorySeed = personalHistorySeed;
    }
    public StableId CharacterId { get; }
    public ulong PersonalHistorySeed { get; }
    public CharacterCreationStage Stage { get; private set; } = CharacterCreationStage.Attributes;
    public AttributeSet Attributes { get; } = new();
    public IReadOnlySet<SkillId> SpecializedSkills => specializedSkills;
    public string? BackgroundId { get; private set; }
    public IReadOnlySet<string> TraitIds => traitIds;
    public string? EquipmentPackageId { get; private set; }
    public IReadOnlyList<PersonalHistoryTag> History => history;

    public bool TrySetAttribute(AttributeId id, int value)
    {
        if (Stage == CharacterCreationStage.Finalized) return false;
        int old = Attributes[id];
        Attributes.Set(id, value);
        if (Attributes.AllocatedAboveBase > ProgressionRules.CreationAttributePoints) { Attributes.Set(id, old); return false; }
        Stage = CharacterCreationStage.Attributes;
        return true;
    }
    public void SetSpecializations(IEnumerable<SkillId> skills)
    {
        specializedSkills.Clear(); specializedSkills.UnionWith(skills);
        Stage = CharacterCreationStage.Specializations;
    }
    public void SetBackground(string id) { ArgumentException.ThrowIfNullOrWhiteSpace(id); BackgroundId = id; Stage = CharacterCreationStage.Background; }
    public void SetTraits(IEnumerable<string> ids) { traitIds.Clear(); traitIds.UnionWith(ids); Stage = CharacterCreationStage.Traits; }
    public void SetHistory(IEnumerable<PersonalHistoryTag> tags) { history.Clear(); history.AddRange(tags.OrderBy(x => x.Id, StringComparer.Ordinal)); Stage = CharacterCreationStage.History; }
    public void SetEquipment(string packageId) { ArgumentException.ThrowIfNullOrWhiteSpace(packageId); EquipmentPackageId = packageId; Stage = CharacterCreationStage.Equipment; }
    public void ApplyRecommendation(CharacterCreationRecommendation recommendation)
    {
        foreach ((AttributeId id, int value) in recommendation.Attributes) Attributes.Set(id, value);
        SetSpecializations(recommendation.SpecializedSkills);
        SetBackground(recommendation.BackgroundId);
        SetTraits(recommendation.TraitIds);
        SetEquipment(recommendation.EquipmentPackageId);
        Stage = CharacterCreationStage.Review;
    }
    public void MarkFinalized() { if (Stage == CharacterCreationStage.Finalized) throw new InvalidOperationException("Draft is already finalized."); Stage = CharacterCreationStage.Finalized; }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(CharacterId); writer.WriteUInt64(PersonalHistorySeed); writer.WriteInt32((int)Stage); Attributes.WriteCanonical(writer);
        writer.WriteCount(specializedSkills.Count); foreach (SkillId id in specializedSkills) writer.WriteInt32((int)id);
        writer.WriteString(BackgroundId); writer.WriteCount(traitIds.Count); foreach (string id in traitIds) writer.WriteString(id);
        writer.WriteString(EquipmentPackageId); writer.WriteCount(history.Count); foreach (PersonalHistoryTag tag in history) { writer.WriteString(tag.Id); writer.WriteString(tag.Value); }
    }
}

public sealed class CharacterCreationService
{
    private readonly BackgroundTraitCatalog catalog;
    public CharacterCreationService(BackgroundTraitCatalog catalog) => this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public CharacterCreationValidation Validate(CharacterCreationDraft draft)
    {
        var errors = new List<string>();
        if (draft.Attributes.AllocatedAboveBase != ProgressionRules.CreationAttributePoints) errors.Add($"Exactly {ProgressionRules.CreationAttributePoints} attribute points must be allocated.");
        if (draft.SpecializedSkills.Count != 3) errors.Add("Exactly three specialized skills are required.");
        if (string.IsNullOrWhiteSpace(draft.BackgroundId)) errors.Add("A background is required.");
        if (draft.TraitIds.Count != 2) errors.Add("Exactly two traits are required.");
        if (draft.TraitIds.Count == 2)
        {
            string[] traits = draft.TraitIds.ToArray();
            if (!catalog.AreCompatible(traits[0], traits[1])) errors.Add("Selected traits are incompatible.");
        }
        if (string.IsNullOrWhiteSpace(draft.EquipmentPackageId)) errors.Add("A starting-equipment package is required.");
        return new(errors.Count == 0, errors);
    }

    public CharacterCreationResult Finalize(CharacterCreationDraft draft)
    {
        CharacterCreationValidation validation = Validate(draft);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
        BackgroundDefinition background = catalog.GetBackground(draft.BackgroundId!);
        var attributes = draft.Attributes.Clone();
        foreach ((AttributeId id, int delta) in background.AttributeAdjustments)
            if (!attributes.TryAdjust(id, delta)) throw new InvalidOperationException($"Background '{background.Id}' exceeds attribute bounds.");
        SkillSet skills = Phase12SkillDefinitions.CreateStartingSkills(attributes, draft.SpecializedSkills);
        foreach ((SkillId id, int delta) in background.SkillAdjustments) skills.Set(id, skills[id] + delta);
        var progression = new CharacterProgressionState(draft.CharacterId, attributes, skills);
        progression.ConfigureCreation(draft.SpecializedSkills, background.Id, draft.TraitIds, draft.History);
        draft.MarkFinalized();
        var writer = new CanonicalWriter(); draft.WriteCanonical(writer); progression.WriteCanonical(writer);
        StartingEquipmentGrant equipment = background.Equipment.PackageId == draft.EquipmentPackageId ? background.Equipment : new(draft.EquipmentPackageId!, background.Equipment.ItemDefinitionIds);
        return new(progression, equipment, writer.ComputeChecksum());
    }
}

public sealed class CharacterQuestionnaire
{
    public CharacterCreationRecommendation Recommend(IReadOnlyDictionary<string, int> answers)
    {
        int combat = answers.GetValueOrDefault("combat");
        int technical = answers.GetValueOrDefault("technical");
        int social = answers.GetValueOrDefault("social");
        int survival = answers.GetValueOrDefault("survival");
        string background = technical >= combat && technical >= social ? "relay-technician" : social >= combat ? "settlement-mediator" : survival > combat ? "perimeter-scout" : "demolition-recovery-worker";
        SkillId[] skills = background switch
        {
            "relay-technician" => [SkillId.Engineering, SkillId.Science, SkillId.Security],
            "settlement-mediator" => [SkillId.Influence, SkillId.Commerce, SkillId.Medicine],
            "perimeter-scout" => [SkillId.Fieldcraft, SkillId.Stealth, SkillId.Ballistics],
            _ => [SkillId.Demolitions, SkillId.Ballistics, SkillId.Engineering]
        };
        var attributes = new AttributeSet();
        foreach (SkillId skill in skills)
        {
            AttributeId primary = Phase12SkillDefinitions.All[skill].PrimaryAttribute;
            if (attributes.AllocatedAboveBase < ProgressionRules.CreationAttributePoints && attributes[primary] < 10) attributes.TryAdjust(primary, 1);
        }
        AttributeId[] order = [AttributeId.Awareness, AttributeId.Reflex, AttributeId.Intellect, AttributeId.Vitality, AttributeId.Presence, AttributeId.Physique, AttributeId.Fortune];
        int cursor = 0;
        while (attributes.AllocatedAboveBase < ProgressionRules.CreationAttributePoints)
        {
            AttributeId id = order[cursor++ % order.Length];
            attributes.TryAdjust(id, 1);
        }
        return new(attributes.Values, skills, background, ["methodical", "adaptable"], $"{background}-kit", ["Recommendation is a starting point and remains fully editable."]);
    }
}
