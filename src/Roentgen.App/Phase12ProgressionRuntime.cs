using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Focus;
using Roentgen.Rpg;
using Roentgen.UI;

namespace Roentgen.App;

internal sealed class Phase12ProgressionRuntime
{
    public Phase12ProgressionRuntime(StableId playerId, Phase20CharacterCreation? characterCreation = null)
    {
        BackgroundTraits = Phase21RpgContent.CreateBackgroundTraitCatalog();
        Perks = Phase21RpgContent.CreatePerkCatalog();
        Repository = new ProgressionRepository();
        CharacterCreationDraft draft = characterCreation?.BuildDraft(playerId) ?? CreateDefaultDraft(playerId);
        CharacterCreationResult creation = new CharacterCreationService(BackgroundTraits).Finalize(draft);
        Repository.Add(creation.Progression);
        Capabilities = new ProgressionCapabilityService(Repository, BackgroundTraits, Perks);
        Commands = new ProgressionCommandService(Repository, Perks);
        CombatCapabilities = new RpgCombatCapabilityProvider(Capabilities);
        FocusCapabilities = new RpgFocusCapabilityProvider(Capabilities);
        var context = new PerkContext(new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
        CharacterScreen = new CharacterScreenController(Repository, Capabilities, Perks, Commands, context);
        StartingEquipment = creation.Equipment;
    }

    public ProgressionRepository Repository { get; }
    public BackgroundTraitCatalog BackgroundTraits { get; }
    public PerkCatalog Perks { get; }
    public ProgressionCapabilityService Capabilities { get; }
    public ProgressionCommandService Commands { get; }
    public RpgCombatCapabilityProvider CombatCapabilities { get; }
    public RpgFocusCapabilityProvider FocusCapabilities { get; }
    public CharacterScreenController CharacterScreen { get; }
    public StartingEquipmentGrant StartingEquipment { get; }

    private static CharacterCreationDraft CreateDefaultDraft(StableId playerId)
    {
        var draft = new CharacterCreationDraft(playerId, 0x5048415345313200UL);
        _ = draft.TrySetAttribute(AttributeId.Awareness, 7);
        _ = draft.TrySetAttribute(AttributeId.Reflex, 7);
        _ = draft.TrySetAttribute(AttributeId.Intellect, 6);
        _ = draft.TrySetAttribute(AttributeId.Vitality, 6);
        _ = draft.TrySetAttribute(AttributeId.Physique, 6);
        _ = draft.TrySetAttribute(AttributeId.Presence, 5);
        _ = draft.TrySetAttribute(AttributeId.Fortune, 5);
        draft.SetSpecializations([SkillId.Ballistics, SkillId.Engineering, SkillId.Science]);
        draft.SetBackground("relay-technician");
        draft.SetTraits(["methodical", "adaptable"]);
        draft.SetHistory([new("prior-work", "regional relay maintenance"), new("diagnostic-exposure", "survived a failed inspection array")]);
        draft.SetEquipment("relay-technician-kit");
        return draft;
    }

    public int GetMaximumHealth(StableId playerId) => Capabilities.Get(playerId).DerivedStatistics[DerivedStatId.MaximumHealth];
}
