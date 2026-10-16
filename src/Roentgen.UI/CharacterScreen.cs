using Roentgen.Contracts;
using Roentgen.Rpg;

namespace Roentgen.UI;

public enum CharacterScreenPage
{
    Summary = 0,
    Attributes = 1,
    Skills = 2,
    Perks = 3,
    BackgroundTraits = 4,
    History = 5,
    DerivedStatistics = 6,
    Advancement = 7
}

public sealed record AttributeScreenEntry(AttributeId Id, int PermanentValue, int EffectiveValue, bool CanIncrease);
public sealed record SkillScreenEntry(SkillId Id, int BaseValue, int EffectiveValue, int NextRankCost, bool Specialized, bool CanIncrease);
public sealed record PerkScreenEntry(string Id, string Name, string Description, int Rank, int MaximumRank, bool Available, IReadOnlyList<string> UnmetRequirements);
public sealed record CharacterScreenSnapshot(
    StableId CharacterId,
    CharacterScreenPage Page,
    int Level,
    long CurrentExperience,
    long ExperienceToNextLevel,
    int UnspentSkillPoints,
    int UnspentPerkPoints,
    int UnspentAttributePoints,
    string BackgroundId,
    IReadOnlyList<string> TraitIds,
    IReadOnlyList<AttributeScreenEntry> Attributes,
    IReadOnlyList<SkillScreenEntry> Skills,
    IReadOnlyList<PerkScreenEntry> Perks,
    DerivedStatSnapshot DerivedStatistics,
    ulong Revision);

public sealed class CharacterScreenController
{
    private readonly ProgressionRepository repository;
    private readonly ProgressionCapabilityService capabilities;
    private readonly PerkCatalog perks;
    private readonly ProgressionCommandService commands;
    private readonly PerkContext context;

    public CharacterScreenController(ProgressionRepository repository, ProgressionCapabilityService capabilities, PerkCatalog perks, ProgressionCommandService commands, PerkContext context)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        this.perks = perks ?? throw new ArgumentNullException(nameof(perks));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public CharacterScreenPage CurrentPage { get; private set; }
    public void SetPage(CharacterScreenPage page) => CurrentPage = page;
    public void NextPage() => CurrentPage = (CharacterScreenPage)(((int)CurrentPage + 1) % Enum.GetValues<CharacterScreenPage>().Length);
    public void PreviousPage() => CurrentPage = (CharacterScreenPage)(((int)CurrentPage + Enum.GetValues<CharacterScreenPage>().Length - 1) % Enum.GetValues<CharacterScreenPage>().Length);

    public CharacterScreenSnapshot BuildSnapshot(StableId characterId)
    {
        CharacterProgressionState state = repository.GetRequired(characterId);
        CharacterCapabilitySnapshot capability = capabilities.Get(characterId);
        AttributeScreenEntry[] attributeEntries = ProgressionRules.AttributeOrder.Select(id => new AttributeScreenEntry(id, state.Attributes[id], capability.Attributes[id], state.UnspentAttributePoints > 0 && state.Attributes[id] < ProgressionRules.MaximumAttribute)).ToArray();
        SkillScreenEntry[] skillEntries = ProgressionRules.SkillOrder.Select(id => new SkillScreenEntry(id, state.Skills[id], capability.Skills[id], SkillSet.GetPointCost(state.Skills[id]), state.SpecializedSkills.Contains(id), state.UnspentSkillPoints >= SkillSet.GetPointCost(state.Skills[id]) && state.Skills[id] < ProgressionRules.MaximumSkill)).ToArray();
        PerkScreenEntry[] perkEntries = perks.All.Select(perk =>
        {
            int rank = state.PerkRanks.GetValueOrDefault(perk.Id);
            string[] unmet = BuildUnmetRequirements(perk, state).ToArray();
            return new PerkScreenEntry(perk.Id, perk.Name, perk.Description, rank, perk.MaximumRank, state.UnspentPerkPoints > 0 && rank < perk.MaximumRank && unmet.Length == 0, unmet);
        }).ToArray();
        return new(characterId, CurrentPage, state.Level, state.CurrentExperience,
            state.Level >= ProgressionRules.MaximumLevel ? 0 : ExperienceSystem.ExperienceToNextLevel(state.Level),
            state.UnspentSkillPoints, state.UnspentPerkPoints, state.UnspentAttributePoints, state.BackgroundId,
            state.TraitIds.Order(StringComparer.Ordinal).ToArray(), attributeEntries, skillEntries, perkEntries,
            capability.DerivedStatistics, state.Revision);
    }

    public ProgressionCommandResult IncreaseAttribute(StableId characterId, AttributeId attribute) => commands.SpendAttribute(characterId, attribute);
    public ProgressionCommandResult IncreaseSkill(StableId characterId, SkillId skill, int ranks = 1) => commands.SpendSkill(characterId, skill, ranks);
    public ProgressionCommandResult AcquirePerk(StableId characterId, string perkId) => commands.AcquirePerk(characterId, perkId, context);

    private IEnumerable<string> BuildUnmetRequirements(PerkDefinition perk, CharacterProgressionState state)
    {
        foreach (PerkRequirement requirement in perk.Requirements)
        {
            bool met = perks.AreRequirementsMet(perk with { Requirements = [requirement] }, state, context);
            if (!met) yield return $"{requirement.Kind}: {requirement.Id} {requirement.MinimumValue}";
        }
    }
}
