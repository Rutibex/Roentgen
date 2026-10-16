using Roentgen.Campaign;
using Roentgen.Contracts;
using Roentgen.Factions;
using FactionReputationDimension = Roentgen.Factions.ReputationDimension;
using Roentgen.Entities;
using Roentgen.Items;
using Roentgen.Narrative;
using Roentgen.Persistence;
using Roentgen.Rpg;
using Roentgen.Simulation;
using Roentgen.UI;

namespace Roentgen.App;

internal static class Phase20ScreenFactory
{
    internal static readonly string[] GameplayTabs = ["Inventory", "Equipment", "Character", "Skills", "Perks", "Map", "Journal", "Factions", "Crafting", "Companion"];
    internal static readonly string[] SettingsTabs = ["Gameplay", "Controls", "Video", "Audio", "Accessibility"];
    internal static readonly string[] InventoryTabs = Enum.GetNames<InventoryCategory>();
    internal static readonly string[] EquipmentTabs = ["Slots", "Compatible Items"];
    internal static readonly string[] SkillTabs = ["All", "Combat", "Physical", "Survival", "Technical", "Science + Medicine", "Social"];
    internal static readonly string[] PerkTabs = ["All", "Available", "Owned", "Locked"];
    internal static readonly string[] CraftingTabs = ["All", "Favorites", "Weapons", "Ammunition", "Medicine", "Tools", "Survival", "Components", "Upgrades", "Special"];
    internal static readonly string[] MapTabs = ["All", "Settlements", "Ruins", "Infrastructure", "Quests"];
    internal static readonly string[] JournalTabs = ["Active", "Completed", "Failed", "Campaign"];
    internal static readonly string[] FactionTabs = ["Overview", "Reputation", "Settlements", "Relations", "Crimes", "Trade"];
    internal static readonly string[] CompanionTabs = ["Overview", "Commands", "Equipment", "Behavior"];

    public static Phase20ScreenModel MainMenu(ILocalizationService text, int selection, bool hasSave, string status)
    {
        Phase20MenuEntry[] entries =
        [
            new("continue", text.Get("menu.continue"), Enabled: hasSave, Description: "Load the most recent compatible campaign."),
            new("new", text.Get("menu.newCampaign"), Description: "Generate a new deterministic campaign and create a character."),
            new("load", text.Get("menu.loadGame"), Enabled: hasSave),
            new("settings", text.Get("menu.settings")),
            new("accessibility", text.Get("menu.accessibility")),
            new("tutorials", text.Get("menu.tutorials")),
            new("credits", text.Get("menu.credits")),
            new("quit", text.Get("menu.quit"), Dangerous: true)
        ];
        return new Phase20ScreenModel(Phase20ScreenKind.MainMenu, text.Get("app.title"), text.Get("app.subtitle"), entries, selection, [], 0, null, text.Get("footer.navigate"), status, true);
    }

    public static Phase20ScreenModel NewCampaign(Phase20ScreenKind kind, Phase20CharacterCreation creation, CampaignSeed seed, DifficultyPreset difficulty, int selection, string status, bool nameEditing = false)
    {
        return kind switch
        {
            Phase20ScreenKind.NewCampaign => BuildCampaignIdentity(creation, seed, difficulty, selection, status, nameEditing),
            Phase20ScreenKind.CharacterAttributes => BuildAttributes(creation, selection, status),
            Phase20ScreenKind.CharacterSkills => BuildSpecializations(creation, selection, status),
            Phase20ScreenKind.CharacterBackground => BuildBackgrounds(creation, selection, status),
            Phase20ScreenKind.CharacterTraits => BuildTraits(creation, selection, status),
            Phase20ScreenKind.CharacterReview => BuildReview(creation, seed, selection, status),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static Phase20ScreenModel BuildCampaignIdentity(Phase20CharacterCreation creation, CampaignSeed seed, DifficultyPreset difficulty, int selection, string status, bool nameEditing)
    {
        Phase20MenuEntry[] entries =
        [
            new("name", "Character name", creation.CharacterName, "The name shown in save files and character summaries.", Presentation: Phase20EntryPresentation.TextBox),
            new("seed", "Campaign seed", $"0x{seed.Value:X16}", "The same seed and generation versions reproduce the same base world.", Presentation: Phase20EntryPresentation.Stepper),
            new("randomize", "Randomize campaign seed", Description: "Generate a new random deterministic seed.", Presentation: Phase20EntryPresentation.Button),
            new("difficulty", "Difficulty", difficulty.ToString(), "Difficulty can be changed later in Settings.", Presentation: Phase20EntryPresentation.Dropdown)
        ];
        return new Phase20ScreenModel(Phase20ScreenKind.NewCampaign, "New Campaign", "Identity and campaign parameters", entries, selection, CreationTabs(0), 0,
            new Phase20Panel("Campaign Contract", ["Finite 30–50 hour central campaign", "Six connected environmental regions", "A coherent beginning, escalation, convergence, ending, and epilogue", "Procedural variation with deterministic provenance"]),
            string.Empty, status, TextEntryActive: nameEditing, ActionPrompts:
            [
                new(UiAction.WizardBack, "Back", ShowBinding: false),
                new(UiAction.WizardNext, "Next: Attributes", !string.IsNullOrWhiteSpace(creation.CharacterName), ShowBinding: false, Tooltip: "Continue to attribute allocation")
            ]);
    }

    public static Phase20ScreenModel PauseMenu(int selection, string status) => new(
        Phase20ScreenKind.PauseMenu,
        "Paused",
        "Simulation time is stopped",
        [
            new("resume", "Resume"),
            new("save", "Save Game"),
            new("load", "Load Game"),
            new("settings", "Settings"),
            new("accessibility", "Accessibility"),
            new("tutorials", "Tutorials"),
            new("main", "Return to Main Menu", Dangerous: true)
        ],
        selection,
        [],
        0,
        new Phase20Panel("Paused Systems", ["Interface and camera time remain active.", "Audio and music use pause-menu mix behavior.", "The deterministic simulation clock does not advance."]),
        "ENTER SELECT  •  ESC RESUME",
        status);

    public static Phase20ScreenModel Inventory(Phase13ItemRuntime items, int selection, InventoryCategory category, InventorySortMode sort, string status)
    {
        InventoryScreenModel inventory = items.BuildPlayerInventory();
        IEnumerable<ItemRowModel> filtered = inventory.Items.Where(item => MatchesCategory(item.Kind, category));
        filtered = sort switch
        {
            InventorySortMode.Kind => filtered.OrderBy(static item => item.Kind).ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase),
            InventorySortMode.Mass => filtered.OrderByDescending(static item => item.MassGrams).ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase),
            InventorySortMode.Value => filtered.OrderByDescending(static item => item.ValueMinorUnits).ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase),
            InventorySortMode.Condition => filtered.OrderBy(static item => item.ConditionPermille).ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
        };
        ItemRowModel[] rows = filtered.ToArray();
        Phase20MenuEntry[] entries = rows.Select(item => new Phase20MenuEntry(
            item.ItemId.ToString(),
            item.Quantity > 1 ? $"{item.Name} x{item.Quantity}" : item.Name,
            InventoryValue(item, sort),
            $"{PlayerKind(item.Kind)} • {item.MassGrams / 1000f:F2} kg • {item.ValueMinorUnits} scrip • condition {item.ConditionPermille / 10.0:F0}%",
            true, false, PlayerKind(item.Kind).ToUpperInvariant())).ToArray();
        ItemRowModel? selected = rows.Length == 0 ? null : rows[Math.Clamp(selection, 0, rows.Length - 1)];
        StableId selectedId = selected?.ItemId ?? StableId.Empty;
        Phase20Panel? detail = selected is null ? new Phase20Panel("Inventory", ["No items match this category.", "Choose another category above or acquire more supplies."]) : BuildItemDetail(items, selectedId);
        bool canEquip = selected is not null && items.CanEquipInventoryItem(selectedId);
        bool canRepair = selected is not null && items.CanRepairInventoryItem(selectedId);
        bool canSplit = selected is not null && items.CanSplitInventoryItem(selectedId);
        bool canDrop = selected is not null && selected.Kind != ItemKind.Quest;
        return GameplayScreen(Phase20ScreenKind.Inventory, "Inventory", $"{category} • sorted by {sort}", entries, selection, 0, detail, string.Empty, string.IsNullOrWhiteSpace(status) ? items.LastStatus : status) with
        {
            SubTabs = InventoryTabs,
            SelectedSubTab = (int)category,
            Meters =
            [
                new Phase20ProgressMeter("Mass", inventory.TotalMassGrams, inventory.MaximumMassGrams, $"{inventory.TotalMassGrams / 1000f:F1}/{inventory.MaximumMassGrams / 1000f:F1} kg"),
                new Phase20ProgressMeter("Bulk", inventory.TotalBulkMilliunits, inventory.MaximumBulkMilliunits, $"{inventory.TotalBulkMilliunits / 1000f:F1}/{inventory.MaximumBulkMilliunits / 1000f:F1}")
            ],
            ActionPrompts =
            [
                new(UiAction.InventoryUse, "Use", selected?.Kind == ItemKind.Consumable, ShowBinding: false),
                new(UiAction.InventoryEquip, "Equip", canEquip, ShowBinding: false),
                new(UiAction.InventoryRepair, "Repair", canRepair, ShowBinding: false),
                new(UiAction.InventorySplit, "Split", canSplit, ShowBinding: false),
                new(UiAction.InventoryDrop, "Drop", canDrop, true, ShowBinding: false),
                new(UiAction.InventoryInspect, "Inspect", selected is not null, ShowBinding: false),
                new(UiAction.InventorySort, $"Sort: {sort}", ShowBinding: false),
                new(UiAction.Cancel, "Close")
            ]
        };
    }

    public static Phase20ScreenModel ItemInspection(Phase13ItemRuntime items, StableId itemId, string status)
    {
        ItemInspectionModel inspection = items.BuildInspection(itemId);
        return new Phase20ScreenModel(Phase20ScreenKind.ItemInspection, inspection.Title, "Detailed item inspection",
            [new("close", "Return to inventory", Description: "Close inspection and return to the previous inventory view.")], 0, [], 0,
            new Phase20Panel("Inspection", inspection.Details), string.Empty, status, true, true,
            ActionPrompts: [new(UiAction.Cancel, "Back")]);
    }

    public static Phase20ScreenModel ContainerLoot(PlayerPresentationSnapshot snapshot, int selection, string status)
    {
        ContainerState? container = snapshot.OpenContainer;
        if (container is null) return new Phase20ScreenModel(Phase20ScreenKind.ContainerLoot, "Container", "No container is open", [], 0, [], 0, null, string.Empty, status, true, true);
        Phase20MenuEntry[] entries = snapshot.ContainerItems.Select(item => new Phase20MenuEntry(item.Id.ToString(), item.Quantity > 1 ? $"{item.DisplayName} x{item.Quantity}" : item.DisplayName,
            Description: $"Stored in {container.DisplayName}.", Badge: "LOOT")).ToArray();
        PortableObjectState? selected = entries.Length == 0 ? null : snapshot.ContainerItems[Math.Clamp(selection, 0, snapshot.ContainerItems.Count - 1)];
        Phase20Panel detail = selected is null
            ? new Phase20Panel(container.DisplayName, ["The container is empty."])
            : new Phase20Panel(selected.DisplayName, [$"Quantity: {selected.Quantity}", "Take this item individually or use Take All."]);
        return new Phase20ScreenModel(Phase20ScreenKind.ContainerLoot, container.DisplayName, "Loot container", entries, selection, [], 0, detail, string.Empty, status, true, true,
            ActionPrompts:
            [
                new(UiAction.ContainerTake, "Take", selected is not null, ShowBinding: false),
                new(UiAction.ContainerTakeAll, "Take All", entries.Length > 0, ShowBinding: false),
                new(UiAction.Cancel, "Close")
            ]);
    }

    private static bool MatchesCategory(ItemKind kind, InventoryCategory category) => category switch
    {
        InventoryCategory.All => true,
        InventoryCategory.Weapons => kind == ItemKind.Weapon,
        InventoryCategory.Armor => kind == ItemKind.Armor,
        InventoryCategory.Aid => kind == ItemKind.Consumable,
        InventoryCategory.Ammo => kind == ItemKind.Ammunition,
        InventoryCategory.Tools => kind is ItemKind.Tool or ItemKind.Component or ItemKind.Container,
        InventoryCategory.Materials => kind is ItemKind.Material or ItemKind.Currency,
        InventoryCategory.Quest => kind is ItemKind.Quest or ItemKind.Document,
        InventoryCategory.Misc => kind == ItemKind.Miscellaneous,
        _ => true
    };

    private static string InventoryValue(ItemRowModel item, InventorySortMode sort) => sort switch
    {
        InventorySortMode.Mass => $"{item.MassGrams / 1000f:F2} kg",
        InventorySortMode.Value => $"{item.ValueMinorUnits} scrip",
        InventorySortMode.Condition => $"{item.ConditionPermille / 10.0:F0}%",
        InventorySortMode.Kind => PlayerKind(item.Kind),
        _ => item.Quantity > 1 ? $"x{item.Quantity}" : $"{item.MassGrams / 1000f:F2} kg"
    };

    private static string PlayerKind(ItemKind kind) => kind switch
    {
        ItemKind.Consumable => "Aid",
        ItemKind.Ammunition => "Ammo",
        ItemKind.Miscellaneous => "Misc",
        _ => kind.ToString()
    };

    public static Phase20ScreenModel Equipment(Phase13ItemRuntime items, int selection, EquipmentViewMode view, EquipmentSlot selectedSlot, string status)
    {
        if (view == EquipmentViewMode.CompatibleItems)
        {
            IReadOnlyList<EquipmentCandidateUiModel> candidates = items.BuildEquipmentCandidates(selectedSlot);
            Phase20MenuEntry[] entries = candidates.Select(candidate => new Phase20MenuEntry(
                candidate.ItemId.ToString(), candidate.Name,
                items.IsEquippedInSlot(selectedSlot, candidate.ItemId) ? "EQUIPPED" : $"{candidate.ConditionPermille / 10.0:F0}%",
                $"{candidate.Kind} • {candidate.MassGrams / 1000f:F2} kg • {candidate.ValueMinorUnits} scrip",
                true, false, items.IsEquippedInSlot(selectedSlot, candidate.ItemId) ? "CURRENT" : "CANDIDATE")).ToArray();
            EquipmentCandidateUiModel? selectedCandidate = candidates.Count == 0 ? null : candidates[Math.Clamp(selection, 0, candidates.Count - 1)];
            StableId candidateId = selectedCandidate?.ItemId ?? StableId.Empty;
            EquipmentComparisonModel comparison = items.BuildEquipmentComparison(selectedSlot, candidateId);
            StableId currentId = items.GetEquippedItem(selectedSlot);
            bool canEquip = selectedCandidate is not null && candidateId != currentId;
            bool canRepair = !currentId.IsEmpty && items.CanRepairInventoryItem(currentId);
            return GameplayScreen(Phase20ScreenKind.Equipment, "Equipment", $"{EquipmentSlotName(selectedSlot)} • choose a compatible replacement", entries, selection, 1,
                new Phase20Panel(comparison.Title, comparison.Lines), string.Empty, string.IsNullOrWhiteSpace(status) ? items.LastStatus : status) with
            {
                SubTabs = EquipmentTabs,
                SelectedSubTab = (int)view,
                ActionPrompts =
                [
                    new(UiAction.EquipmentEquip, currentId.IsEmpty ? "Equip" : "Change", canEquip, ShowBinding: false),
                    new(UiAction.EquipmentRepair, "Repair Current", canRepair, ShowBinding: false),
                    new(UiAction.EquipmentInspect, "Inspect", selectedCandidate is not null, ShowBinding: false),
                    new(UiAction.EquipmentBack, "Back to Slots", ShowBinding: false),
                    new(UiAction.Cancel, "Close")
                ]
            };
        }

        IReadOnlyList<EquipmentSlotUiModel> slots = items.BuildEquipmentSlots();
        Phase20MenuEntry[] slotEntries = slots.Select(slot => new Phase20MenuEntry(
            slot.Slot.ToString(), slot.Label, slot.EquippedItemName,
            slot.EquippedItemId.IsEmpty ? "Empty equipment slot." : $"Currently equipped: {slot.EquippedItemName}",
            true, false, slot.EquippedItemId.IsEmpty ? "EMPTY" : null)).ToArray();
        EquipmentSlotUiModel? selectedModel = slots.Count == 0 ? null : slots[Math.Clamp(selection, 0, slots.Count - 1)];
        EquipmentSlot selected = selectedModel?.Slot ?? selectedSlot;
        EquipmentComparisonModel detail = items.BuildEquipmentComparison(selected, StableId.Empty);
        bool hasItem = selectedModel is not null && !selectedModel.EquippedItemId.IsEmpty;
        return GameplayScreen(Phase20ScreenKind.Equipment, "Equipment", "Slot-oriented loadout, replacement selection, and direct comparison", slotEntries, selection, 1,
            new Phase20Panel(detail.Title, detail.Lines), string.Empty, string.IsNullOrWhiteSpace(status) ? items.LastStatus : status) with
        {
            SubTabs = EquipmentTabs,
            SelectedSubTab = (int)view,
            ActionPrompts =
            [
                new(UiAction.EquipmentChange, "Change", selectedModel?.HasCompatibleItems == true, ShowBinding: false, Tooltip: "Browse inventory items compatible with this slot"),
                new(UiAction.EquipmentUnequip, "Unequip", hasItem, ShowBinding: false),
                new(UiAction.EquipmentRepair, "Repair", selectedModel?.CanRepair == true, ShowBinding: false),
                new(UiAction.EquipmentInspect, "Inspect", hasItem, ShowBinding: false),
                new(UiAction.Cancel, "Close")
            ]
        };
    }

    public static Phase20ScreenModel Character(Phase12ProgressionRuntime progression, StableId playerId, int selection, string status)
    {
        CharacterScreenSnapshot snapshot = progression.CharacterScreen.BuildSnapshot(playerId);
        Phase20MenuEntry[] entries =
        [
            new("level", "Level", snapshot.Level.ToString()),
            new("experience", "Experience", $"{snapshot.CurrentExperience}/{snapshot.ExperienceToNextLevel}"),
            new("background", "Background", snapshot.BackgroundId),
            new("traits", "Traits", string.Join(", ", snapshot.TraitIds)),
            new("skill-points", "Unspent skill points", snapshot.UnspentSkillPoints.ToString()),
            new("perk-points", "Unspent perk points", snapshot.UnspentPerkPoints.ToString()),
            new("attribute-points", "Unspent attribute points", snapshot.UnspentAttributePoints.ToString())
        ];
        string[] derived = Enum.GetValues<DerivedStatId>().Select(id => $"{id}: {snapshot.DerivedStatistics[id]}").ToArray();
        return GameplayScreen(Phase20ScreenKind.Character, "Character", "Background, traits, advancement, and derived statistics", entries, selection, 2, new Phase20Panel("Derived Statistics", derived), "TAB NEXT PAGE  •  ESC CLOSE", status);
    }

    public static Phase20ScreenModel Skills(Phase12ProgressionRuntime progression, StableId playerId, int selection, SkillCategory category, string status)
    {
        CharacterScreenSnapshot snapshot = progression.CharacterScreen.BuildSnapshot(playerId);
        SkillScreenEntry[] filtered = snapshot.Skills.Where(skill => SkillMatchesCategory(skill.Id, category)).ToArray();
        Phase20MenuEntry[] entries = filtered.Select(skill => new Phase20MenuEntry(
            skill.Id.ToString(), SkillName(skill.Id), skill.EffectiveValue.ToString(),
            $"Permanent {skill.BaseValue} • next rank {skill.NextRankCost} point{(skill.NextRankCost == 1 ? string.Empty : "s")}",
            true, false, skill.Specialized ? "SPECIALIZED" : null, Phase20EntryPresentation.Progress, skill.EffectiveValue / 100f)).ToArray();
        SkillScreenEntry? selected = filtered.Length == 0 ? null : filtered[Math.Clamp(selection, 0, filtered.Length - 1)];
        Phase20Panel detail = selected is null
            ? new Phase20Panel("Skills", ["No skills are in this category."])
            : new Phase20Panel(SkillName(selected.Id),
            [
                $"Category: {SkillCategoryFor(selected.Id)}",
                $"Permanent value: {selected.BaseValue}",
                $"Effective value: {selected.EffectiveValue}" + (selected.EffectiveValue == selected.BaseValue ? string.Empty : $" ({selected.EffectiveValue - selected.BaseValue:+#;-#;0} from specialization/effects)"),
                $"Next rank cost: {selected.NextRankCost} skill point{(selected.NextRankCost == 1 ? string.Empty : "s")}",
                selected.Specialized ? "Specialized: yes — this skill received the character-creation specialization bonus." : "Specialized: no",
                selected.CanIncrease ? "Ready to increase." : snapshot.UnspentSkillPoints <= 0 ? "No unspent skill points remain." : "Maximum rank reached or the next rank costs more points than are available."
            ]);
        return GameplayScreen(Phase20ScreenKind.Skills, "Skills", $"Unspent skill points: {snapshot.UnspentSkillPoints}", entries, selection, 3, detail, string.Empty, status) with
        {
            SubTabs = SkillTabs,
            SelectedSubTab = (int)category,
            ActionPrompts = [new(UiAction.SkillIncrease, "Increase", selected?.CanIncrease == true, ShowBinding: false), new(UiAction.Cancel, "Close")]
        };
    }

    public static Phase20ScreenModel Perks(Phase12ProgressionRuntime progression, StableId playerId, int selection, PerkFilter filter, string status)
    {
        CharacterScreenSnapshot snapshot = progression.CharacterScreen.BuildSnapshot(playerId);
        PerkScreenEntry[] filtered = snapshot.Perks.Where(perk => PerkMatchesFilter(perk, filter)).ToArray();
        Phase20MenuEntry[] entries = filtered.Select(perk =>
        {
            string state = PerkState(perk);
            return new Phase20MenuEntry(perk.Id, perk.Name, $"Rank {perk.Rank}/{perk.MaximumRank}", perk.Description, true, false, state);
        }).ToArray();
        PerkScreenEntry? selected = filtered.Length == 0 ? null : filtered[Math.Clamp(selection, 0, filtered.Length - 1)];
        Phase20Panel detail;
        if (selected is null) detail = new Phase20Panel("Perks", ["No perks match this filter."]);
        else
        {
            PerkDefinition definition = progression.Perks.Get(selected.Id);
            var lines = new List<string>
            {
                selected.Description,
                $"Family: {definition.Family}",
                $"Rank: {selected.Rank}/{selected.MaximumRank}",
                $"State: {PerkState(selected)}"
            };
            if (selected.UnmetRequirements.Count == 0) lines.Add("Requirements: satisfied");
            else
            {
                lines.Add("Requirements still needed:");
                lines.AddRange(selected.UnmetRequirements.Select(value => $"  - {FriendlyRequirement(value)}"));
            }
            if (selected.Rank >= selected.MaximumRank) lines.Add("This perk is fully ranked.");
            else if (selected.Available) lines.Add("Spend 1 perk point to acquire the next rank.");
            else if (selected.UnmetRequirements.Count == 0 && snapshot.UnspentPerkPoints == 0) lines.Add("Requirements are met, but no perk points remain.");
            detail = new Phase20Panel(selected.Name, lines);
        }
        return GameplayScreen(Phase20ScreenKind.Perks, "Perks", $"Unspent perk points: {snapshot.UnspentPerkPoints}", entries, selection, 4, detail, string.Empty, status) with
        {
            Presentation = Phase20ScreenPresentation.CardGridTwoColumns,
            SubTabs = PerkTabs,
            SelectedSubTab = (int)filter,
            ActionPrompts = [new(UiAction.PerkAcquire, "Acquire", selected?.Available == true, ShowBinding: false), new(UiAction.Cancel, "Close")]
        };
    }

    public static Phase20ScreenModel Map(Phase15SocietyRuntime society, Phase17CampaignRuntime campaign, int selection, string status)
    {
        SettlementState[] settlements = society.World.Settlements.OrderBy(static value => value.Plan.DisplayName, StringComparer.Ordinal).ToArray();
        Phase20MenuEntry[] entries = settlements.Select(settlement => new Phase20MenuEntry(
            settlement.Id.ToString(), settlement.Plan.DisplayName, settlement.Plan.Topology.ToString(),
            $"Population {settlement.Population.TotalPopulation}; stability {settlement.StabilityPermille / 10.0:F1}%", true, false,
            society.World.GetFaction(settlement.ControllingFactionId).Plan.DisplayName)).ToArray();
        SettlementState? selected = entries.Length == 0 ? null : settlements[Math.Clamp(selection, 0, settlements.Length - 1)];
        Phase20Panel? detail = selected is null ? null : new Phase20Panel("Regional Intelligence", [
            $"Controller: {society.World.GetFaction(selected.ControllingFactionId).Plan.DisplayName}",
            $"Population: {selected.Population.TotalPopulation}/{selected.Population.HousingCapacity}",
            $"Stability: {selected.StabilityPermille / 10.0:F1}%",
            $"Migration pressure: {selected.Population.MigrationPressurePermille / 10.0:F1}%",
            $"Services: {string.Join(", ", selected.Services.Where(static value => value.Operating).Select(static value => value.Kind))}",
            $"Campaign milestones: {campaign.Campaign.Milestones.Count(value => value.Status is CampaignMilestoneStatus.Resolved or CampaignMilestoneStatus.Recovered)}/{campaign.Campaign.Milestones.Count}"
        ]);
        return GameplayScreen(Phase20ScreenKind.Map, "Regional Map", $"Day {society.World.Day}", entries, selection, 5, detail, "ENTER SET WAYPOINT  •  ESC CLOSE", status);
    }

    public static Phase20ScreenModel Journal(
        Phase16NarrativeRuntime narrative,
        Phase17CampaignRuntime campaign,
        int selection,
        JournalFilter filter,
        StableId trackedQuestId,
        Func<StableId, string> displayName,
        Func<StableId, bool> questHasMapTarget,
        string status)
    {
        var builder = new Phase16NarrativeScreenBuilder();
        IReadOnlyList<QuestJournalEntry> journal = builder.BuildJournal(narrative.World, displayName);
        var entries = new List<Phase20MenuEntry>();

        if (filter == JournalFilter.Campaign)
        {
            foreach (GeneratedMilestonePlan plan in campaign.Campaign.Plan.Milestones.OrderBy(static value => value.Sequence))
            {
                CampaignMilestoneRuntimeState state = campaign.Campaign.GetMilestone(plan.Id);
                string badge = state.Status is CampaignMilestoneStatus.Resolved or CampaignMilestoneStatus.Recovered ? "RESOLVED" : state.Status.ToString().ToUpperInvariant();
                entries.Add(new Phase20MenuEntry($"campaign:{plan.Id}", $"{plan.Sequence + 1:00}. {plan.Kernel.DisplayName}", state.Status.ToString(), "Major campaign milestone", true, false, badge));
            }
        }
        else
        {
            IEnumerable<QuestJournalEntry> visible = journal.Where(entry => JournalMatchesFilter(entry.Status, filter));
            foreach (QuestJournalEntry quest in visible.OrderBy(static value => value.Status).ThenBy(static value => value.Title, StringComparer.OrdinalIgnoreCase))
            {
                string progress = $"{quest.CompletedObjectiveCount}/{quest.RequiredObjectiveCount} required objectives";
                string badge = quest.QuestId == trackedQuestId ? "TRACKED" : quest.Status.ToString().ToUpperInvariant();
                entries.Add(new Phase20MenuEntry($"quest:{quest.QuestId}", quest.Title, progress, quest.CurrentObjective, true, false, badge));
            }
        }

        Phase20MenuEntry? selectedEntry = entries.Count == 0 ? null : entries[Math.Clamp(selection, 0, entries.Count - 1)];
        Phase20Panel detail;
        bool selectedIsQuest = false;
        bool selectedTracked = false;
        bool canShowOnMap = false;
        if (selectedEntry is null)
        {
            detail = new Phase20Panel("Journal", filter == JournalFilter.Campaign
                ? ["No campaign milestones are available."]
                : [$"No {JournalTabs[(int)filter].ToLowerInvariant()} quests are currently recorded."]);
        }
        else if (TryParseJournalId(selectedEntry.Id, "quest", out StableId questId))
        {
            selectedIsQuest = true;
            QuestJournalEntry quest = journal.First(value => value.QuestId == questId);
            selectedTracked = questId == trackedQuestId;
            canShowOnMap = questHasMapTarget(questId);
            var lines = new List<string>
            {
                $"Status: {quest.Status}",
                quest.Themes.Count > 0 ? $"Themes: {string.Join(", ", quest.Themes)}" : "Themes: unknown",
                $"Progress: {quest.CompletedObjectiveCount}/{quest.RequiredObjectiveCount} required objectives",
                string.Empty,
                "CURRENT STEP",
                quest.CurrentObjective,
                string.Empty,
                "OBJECTIVES"
            };
            foreach (QuestObjectiveRow objective in quest.Objectives)
            {
                string mark = objective.Status switch
                {
                    ObjectiveStatus.Completed => "[X]",
                    ObjectiveStatus.Failed => "[!]",
                    ObjectiveStatus.Active => "[>]",
                    _ => "[ ]"
                };
                lines.Add($"{mark} {objective.Text}{(objective.Optional ? " (optional)" : string.Empty)}");
            }
            if (quest.References.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("KNOWN PEOPLE AND PLACES");
                foreach (QuestReferenceRow reference in quest.References.Take(6)) lines.Add($"{reference.Label}: {reference.DisplayName}");
            }
            if (quest.RouteHints.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("KNOWN APPROACHES");
                foreach (string route in quest.RouteHints.Take(4)) lines.Add(route);
            }
            if (quest.ConsequenceHints.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("POSSIBLE OUTCOMES");
                foreach (string consequence in quest.ConsequenceHints.Take(4)) lines.Add(consequence);
            }
            detail = new Phase20Panel(quest.Title, lines, Status: selectedTracked ? "Tracked quest" : null);
        }
        else if (TryParseJournalId(selectedEntry.Id, "campaign", out StableId milestoneId))
        {
            GeneratedMilestonePlan plan = campaign.Campaign.Plan.Milestones.First(value => value.Id == milestoneId);
            CampaignMilestoneRuntimeState state = campaign.Campaign.GetMilestone(milestoneId);
            detail = new Phase20Panel(plan.Kernel.DisplayName,
            [
                $"Campaign stage: {plan.Sequence + 1}/{campaign.Campaign.Plan.Milestones.Count}",
                $"Status: {state.Status}",
                $"Region: {plan.Region}",
                $"Quest: {plan.QuestPlan.Kernel.DisplayName}",
                state.Status is CampaignMilestoneStatus.Resolved or CampaignMilestoneStatus.Recovered ? "This milestone has been resolved." : "This milestone still affects the central campaign."
            ]);
        }
        else detail = new Phase20Panel("Journal", ["Select an entry to inspect it."]);

        IReadOnlyList<Phase20ActionPrompt> actions = selectedIsQuest
            ? [
                new(selectedTracked ? UiAction.JournalUntrack : UiAction.JournalTrack, selectedTracked ? "Untrack" : "Track", ShowBinding: false),
                new(UiAction.JournalShowOnMap, "Show on Map", canShowOnMap, ShowBinding: false, Tooltip: canShowOnMap ? "Open the atlas at this quest's current objective" : "No mapped objective is known for this quest"),
                new(UiAction.Cancel, "Close")
              ]
            : [new(UiAction.Cancel, "Close")];

        return GameplayScreen(Phase20ScreenKind.Journal, "Quest Journal", "Objectives, campaign progress, and tracked goals", entries, selection, 6, detail, string.Empty, status) with
        {
            SubTabs = JournalTabs,
            SelectedSubTab = (int)filter,
            ActionPrompts = actions
        };
    }

    public static Phase20ScreenModel Factions(Phase15SocietyRuntime society, StableId playerId, int selection, FactionScreenSection section, string status)
    {
        var builder = new Phase15SocietyScreenBuilder();
        SocietyWorldState world = society.World;

        if (section == FactionScreenSection.Settlements)
        {
            SettlementState[] settlements = world.Settlements.OrderBy(static value => value.Plan.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(static value => value.Id).ToArray();
            Phase20MenuEntry[] entries = settlements.Select(value => new Phase20MenuEntry(
                $"settlement:{value.Id}",
                value.Plan.DisplayName,
                world.GetFaction(value.ControllingFactionId).Plan.DisplayName,
                $"Population {value.Population.TotalPopulation} • stability {value.StabilityPermille / 10.0:F0}% • {value.Services.Count(static service => service.Operating)} services open",
                true, false, "SETTLEMENT")).ToArray();
            SettlementSocietyScreenModel? selected = settlements.Length == 0 ? null : builder.BuildSettlement(world, settlements[Math.Clamp(selection, 0, settlements.Length - 1)].Id);
            Phase20Panel detail = selected is null
                ? new Phase20Panel("Settlements", ["No known settlements are currently recorded."])
                : new Phase20Panel(selected.Name,
                [
                    $"Controller: {selected.ControllingFactionName}",
                    $"Layout: {selected.Topology}",
                    $"Population: {selected.Population} / housing {selected.HousingCapacity}",
                    $"Stability: {SocietyUiText.Bar(selected.StabilityPermille)} {SocietyUiText.Percent(selected.StabilityPermille)}",
                    $"Migration pressure: {SocietyUiText.Bar(selected.MigrationPressurePermille)} {SocietyUiText.Percent(selected.MigrationPressurePermille)}",
                    $"Local tax: {SocietyUiText.Percent(selected.TaxPermille)}",
                    "",
                    "SERVICES",
                    .. selected.Services.Take(7),
                    "",
                    "RESOURCES",
                    .. selected.Resources.Take(6),
                    "",
                    "ACTIVE TRADE",
                    .. (selected.ActiveShipments.Count == 0 ? new[] { "No active shipments." } : selected.ActiveShipments.Take(5).ToArray())
                ]);
            return GameplayScreen(Phase20ScreenKind.Factions, "Factions & Settlements", "Political standing, communities, relations, crimes, and trade", entries, selection, 7, detail, string.Empty, status) with
            {
                SubTabs = FactionTabs,
                SelectedSubTab = (int)section,
                Presentation = Phase20ScreenPresentation.CardGridTwoColumns,
                ActionPrompts =
                [
                    new(UiAction.FactionShowOnMap, "Show on Map", selected is not null, ShowBinding: false, Tooltip: "Open the Wasteland Atlas at this settlement"),
                    new(UiAction.Cancel, "Close")
                ]
            };
        }

        IReadOnlyList<FactionSummaryRowModel> factions = builder.BuildFactionList(world);
        Phase20MenuEntry[] factionEntries = factions.Select(faction =>
        {
            ReputationScreenModel reputation = builder.BuildReputation(world, playerId, faction.FactionId);
            string value = section == FactionScreenSection.Reputation ? reputation.Standing : $"Cohesion {faction.CohesionPermille / 10.0:F0}%";
            return new Phase20MenuEntry(
                $"faction:{faction.FactionId}", faction.Name, value,
                $"{faction.ControlledSettlementCount} settlements • legitimacy {faction.LegitimacyPermille / 10.0:F0}% • {faction.LeadershipStatus}",
                true, false, section == FactionScreenSection.Reputation ? reputation.Standing.ToUpperInvariant() : $"{faction.ControlledSettlementCount} SETTLEMENTS");
        }).ToArray();

        Phase20Panel detailPanel = new("Factions", ["Select a faction to inspect it."]);
        if (factions.Count > 0)
        {
            FactionSummaryRowModel summary = factions[Math.Clamp(selection, 0, factions.Count - 1)];
            FactionDetailScreenModel faction = builder.BuildFactionDetail(world, summary.FactionId);
            ReputationScreenModel reputation = builder.BuildReputation(world, playerId, summary.FactionId);
            FactionState state = world.GetFaction(summary.FactionId);
            detailPanel = section switch
            {
                FactionScreenSection.Reputation => BuildFactionReputationPanel(world, playerId, state, reputation),
                FactionScreenSection.Relations => BuildFactionRelationsPanel(world, state),
                FactionScreenSection.Crimes => BuildFactionCrimesPanel(world, playerId, state),
                FactionScreenSection.Trade => BuildFactionTradePanel(world, state, faction),
                _ => BuildFactionOverviewPanel(world, state, summary, faction, reputation)
            };
        }

        return GameplayScreen(Phase20ScreenKind.Factions, "Factions & Settlements", "Political standing, communities, relations, crimes, and trade", factionEntries, selection, 7, detailPanel, string.Empty, status) with
        {
            SubTabs = FactionTabs,
            SelectedSubTab = (int)section,
            Presentation = Phase20ScreenPresentation.CardGridTwoColumns,
            ActionPrompts = [new(UiAction.Cancel, "Close")]
        };
    }

    private static Phase20Panel BuildFactionOverviewPanel(SocietyWorldState world, FactionState state, FactionSummaryRowModel summary, FactionDetailScreenModel faction, ReputationScreenModel reputation)
    {
        var lines = new List<string>
        {
            $"Your standing: {reputation.Standing}",
            $"Cohesion: {SocietyUiText.Bar(summary.CohesionPermille)} {SocietyUiText.Percent(summary.CohesionPermille)}",
            $"Legitimacy: {SocietyUiText.Bar(summary.LegitimacyPermille)} {SocietyUiText.Percent(summary.LegitimacyPermille)}",
            $"Leadership: {summary.LeadershipStatus}",
            $"Controlled settlements: {summary.ControlledSettlementCount}",
            $"Treasury: {summary.TreasuryMinorUnits} scrip",
            "",
            "FOUNDING",
            faction.FoundingEvent,
            $"Survival strategy: {faction.SurvivalMethod}",
            "",
            "CURRENT PRIORITIES"
        };
        lines.AddRange(faction.Objectives.Take(4).Select(static value => "• " + value));
        if (faction.Ideology.Count > 0)
        {
            lines.Add("");
            lines.Add("IDEOLOGICAL LEANINGS");
            lines.AddRange(faction.Ideology.Take(5));
        }
        if (faction.Taboos.Count > 0)
        {
            lines.Add("");
            lines.Add("TABOOS");
            lines.AddRange(faction.Taboos.Take(4).Select(static value => "• " + value));
        }
        return new Phase20Panel(faction.Name, lines);
    }

    private static Phase20Panel BuildFactionReputationPanel(SocietyWorldState world, StableId playerId, FactionState faction, ReputationScreenModel reputation)
    {
        FactionReputationState state = world.GetOrCreateReputation(playerId, faction.Id);
        var lines = new List<string> { $"Standing: {reputation.Standing}", "" };
        foreach ((FactionReputationDimension dimension, int value) in state.Values.OrderBy(static pair => pair.Key))
        {
            bool signed = dimension is not (FactionReputationDimension.Fear or FactionReputationDimension.KnownCrimeSeverity or FactionReputationDimension.PublicFamiliarity);
            lines.Add($"{SocietyUiText.ReputationLabel(dimension)}: {SocietyUiText.Bar(value, signed: signed)} {SocietyUiText.ReputationValue(dimension, value)}");
        }
        lines.Add("");
        lines.Add("RECENT REPUTATION EVENTS");
        lines.AddRange(reputation.Evidence.Count == 0 ? new[] { "No notable reputation events are known." } : reputation.Evidence.Take(7).ToArray());
        if (reputation.Warrants.Count > 0)
        {
            lines.Add("");
            lines.Add("ACTIVE WARRANTS");
            lines.AddRange(reputation.Warrants);
        }
        return new Phase20Panel(faction.Plan.DisplayName, lines);
    }

    private static Phase20Panel BuildFactionRelationsPanel(SocietyWorldState world, FactionState faction)
    {
        var lines = new List<string>();
        foreach (FactionRelationshipState relation in world.Relationships.Where(value => value.SourceFactionId == faction.Id).OrderBy(static value => value.TargetFactionId))
        {
            string target = world.GetFaction(relation.TargetFactionId).Plan.DisplayName;
            lines.Add($"{target} — {SocietyUiText.Humanize(relation.DerivedKind)}");
            lines.Add($"  Trust {SocietyUiText.SignedPercent(relation.TrustPermille)} • Fear {SocietyUiText.Percent(relation.FearPermille)} • Respect {SocietyUiText.SignedPercent(relation.RespectPermille)}");
            lines.Add($"  Trade reliance {SocietyUiText.Percent(relation.TradeReliancePermille)} • Border tension {SocietyUiText.Percent(relation.BorderTensionPermille)}");
            foreach (DiplomaticAgreementState agreement in relation.Agreements.Where(static value => value.Active).Take(3))
                lines.Add($"  Agreement: {SocietyUiText.Humanize(agreement.Kind)}");
            lines.Add(string.Empty);
        }
        if (lines.Count == 0) lines.Add("No formal relationships are currently known.");
        return new Phase20Panel(faction.Plan.DisplayName, lines);
    }

    private static Phase20Panel BuildFactionCrimesPanel(SocietyWorldState world, StableId playerId, FactionState faction)
    {
        CrimeRecordState[] crimes = world.Crimes.Where(value => value.SuspectedActorId == playerId && value.JurisdictionFactionId == faction.Id).OrderByDescending(static value => value.OccurredDay).ToArray();
        var lines = new List<string>();
        foreach (CrimeRecordState crime in crimes)
        {
            string location = world.TryGetSettlement(crime.LocationId, out SettlementState? settlement) && settlement is not null ? settlement.Plan.DisplayName : "Unknown site";
            lines.Add($"{SocietyUiText.Humanize(crime.Kind)} — {SocietyUiText.Humanize(crime.Status)}");
            lines.Add($"  Day {crime.OccurredDay} at {location} • severity {SocietyUiText.Percent(crime.SeverityPermille)}");
            lines.Add($"  Identification confidence {SocietyUiText.Percent(crime.IdentityConfidencePermille)}{(crime.FineMinorUnits > 0 ? $" • fine {crime.FineMinorUnits} scrip" : string.Empty)}");
            lines.Add(string.Empty);
        }
        if (lines.Count == 0) lines.Add("This faction has no recorded crimes attributed to you.");
        return new Phase20Panel(faction.Plan.DisplayName, lines);
    }

    private static Phase20Panel BuildFactionTradePanel(SocietyWorldState world, FactionState faction, FactionDetailScreenModel detail)
    {
        HashSet<StableId> controlled = faction.ControlledSettlements.ToHashSet();
        var lines = new List<string> { $"Treasury: {faction.TreasuryMinorUnits} scrip", "", "MATERIAL PRIORITIES" };
        lines.AddRange(detail.MaterialNeeds.Take(6));
        lines.Add("");
        lines.Add("TRADE ROUTES");
        string[] routes = world.TradeRoutes.Where(value => controlled.Contains(value.ExporterSettlementId) || controlled.Contains(value.ImporterSettlementId)).OrderBy(static value => value.Id).Select(value =>
            $"{world.GetSettlement(value.ExporterSettlementId).Plan.DisplayName} → {world.GetSettlement(value.ImporterSettlementId).Plan.DisplayName}: {SocietyUiText.Humanize(value.Commodity)} • risk {SocietyUiText.Percent(value.BaseRiskPermille)} • {(value.Active ? "Active" : "Suspended")}").ToArray();
        lines.AddRange(routes.Length == 0 ? new[] { "No faction-linked trade routes are currently operating." } : routes);
        return new Phase20Panel(faction.Plan.DisplayName, lines);
    }

    public static Phase20ScreenModel Crafting(Phase13ItemRuntime items, int selection, CraftingCategory category, bool hideUnavailable, IReadOnlySet<string> favorites, string status)
    {
        CraftingScreenModel crafting = items.BuildCrafting();
        CraftingRecipeUiModel[] all = crafting.AvailableRecipes.Select(items.BuildCraftingRecipeUi).ToArray();
        IEnumerable<CraftingRecipeUiModel> visible = all;
        if (category == CraftingCategory.Favorites) visible = visible.Where(recipe => favorites.Contains(recipe.RecipeId));
        else if (category != CraftingCategory.All) visible = visible.Where(recipe => recipe.Category == category);
        if (hideUnavailable) visible = visible.Where(static recipe => recipe.MaximumCraftCount > 0);
        CraftingRecipeUiModel[] recipes = visible
            .OrderByDescending(recipe => favorites.Contains(recipe.RecipeId))
            .ThenBy(static recipe => recipe.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Phase20MenuEntry[] entries = recipes.Select(recipe => new Phase20MenuEntry(
            recipe.RecipeId, recipe.DisplayName,
            recipe.Outputs.Count == 0 ? recipe.Operation.ToString() : recipe.Outputs[0],
            $"{recipe.Station} • {SkillName(recipe.Skill)} {recipe.EffectiveSkill}/{recipe.Difficulty} • can craft {recipe.MaximumCraftCount}",
            true, false,
            favorites.Contains(recipe.RecipeId) ? "FAVORITE" : recipe.MaximumCraftCount > 0 ? "READY" : "MISSING")).ToArray();
        CraftingRecipeUiModel? selected = recipes.Length == 0 ? null : recipes[Math.Clamp(selection, 0, recipes.Length - 1)];
        Phase20Panel detail;
        if (selected is null)
        {
            detail = new Phase20Panel("Recipe Browser", hideUnavailable
                ? ["No craftable recipes match this category.", "Show unavailable recipes to see missing requirements."]
                : ["No known recipes match this category."]);
        }
        else
        {
            var lines = new List<string>
            {
                $"Operation: {selected.Operation}",
                $"Station: {FriendlyStation(selected.Station)}",
                $"Skill: {SkillName(selected.Skill)} {selected.EffectiveSkill} / difficulty {selected.Difficulty} — {selected.RiskLabel}",
                "",
                "OUTPUT"
            };
            lines.AddRange(selected.Outputs.Select(value => $"  {value}"));
            lines.Add("");
            lines.Add("MATERIALS");
            lines.AddRange(selected.Ingredients.Select(ingredient => $"  [{(ingredient.Satisfied ? "OK" : "NEED")}] {ingredient.Label}: {ingredient.Have}/{ingredient.Need}"));
            lines.Add("");
            lines.Add(selected.MaximumCraftCount > 0 ? $"Can craft now: {selected.MaximumCraftCount}" : "Cannot craft: one or more required materials are missing.");
            lines.Add("Crafting results remain deterministic; low skill can still produce a costly success or failure.");
            detail = new Phase20Panel(selected.DisplayName, lines);
        }
        int maxCrafts = selected?.MaximumCraftCount ?? 0;
        bool favorite = selected is not null && favorites.Contains(selected.RecipeId);
        return GameplayScreen(Phase20ScreenKind.Crafting, "Crafting", $"{crafting.AvailableRecipes.Count} known recipes • unavailable {(hideUnavailable ? "hidden" : "shown")}", entries, selection, 8, detail, string.Empty,
            string.IsNullOrWhiteSpace(status) ? crafting.Status : status) with
        {
            Presentation = Phase20ScreenPresentation.CardGridTwoColumns,
            SubTabs = CraftingTabs,
            SelectedSubTab = (int)category,
            ActionPrompts =
            [
                new(UiAction.CraftingCraftOne, "Craft 1", maxCrafts >= 1, ShowBinding: false),
                new(UiAction.CraftingCraftFive, "Craft 5", maxCrafts >= 5, ShowBinding: false),
                new(UiAction.CraftingCraftAll, "Craft All", maxCrafts >= 1, ShowBinding: false),
                new(UiAction.CraftingToggleFavorite, favorite ? "Unfavorite" : "Favorite", selected is not null, ShowBinding: false),
                new(UiAction.CraftingToggleUnavailable, hideUnavailable ? "Show Unavailable" : "Hide Unavailable", ShowBinding: false),
                new(UiAction.Cancel, "Close")
            ]
        };
    }

    public static Phase20ScreenModel Merchant(
        Phase13ItemRuntime items,
        int merchantSelection,
        int playerSelection,
        int merchantScroll,
        int playerScroll,
        MerchantPane activePane,
        string status)
    {
        MerchantScreenModel trade = items.BuildMerchant();
        ItemRowModel[] merchantRows = trade.MerchantItems.OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        ItemRowModel[] playerRows = trade.BuyerItems.OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        Phase20MenuEntry[] merchantEntries = merchantRows.Select(row =>
        {
            MerchantTradePreviewUiModel preview = items.BuildMerchantTradePreview(row.ItemId, merchantSelling: true);
            return new Phase20MenuEntry(row.ItemId.ToString(), row.Quantity > 1 ? $"{row.Name} x{row.Quantity}" : row.Name,
                $"{preview.UnitPriceMinorUnits} scrip", $"Buy price • condition {row.ConditionPermille / 10.0:F0}% • {PlayerKind(row.Kind)}", preview.MaximumAffordableQuantity > 0);
        }).ToArray();
        Phase20MenuEntry[] playerEntries = playerRows.Select(row =>
        {
            MerchantTradePreviewUiModel preview = items.BuildMerchantTradePreview(row.ItemId, merchantSelling: false);
            return new Phase20MenuEntry(row.ItemId.ToString(), row.Quantity > 1 ? $"{row.Name} x{row.Quantity}" : row.Name,
                $"{preview.UnitPriceMinorUnits} scrip", $"Sell price • condition {row.ConditionPermille / 10.0:F0}% • {PlayerKind(row.Kind)}", preview.MaximumAffordableQuantity > 0);
        }).ToArray();

        ItemRowModel? selected = activePane == MerchantPane.MerchantStock
            ? merchantRows.ElementAtOrDefault(Math.Clamp(merchantSelection, 0, Math.Max(0, merchantRows.Length - 1)))
            : playerRows.ElementAtOrDefault(Math.Clamp(playerSelection, 0, Math.Max(0, playerRows.Length - 1)));
        MerchantTradePreviewUiModel? previewSelected = selected is null ? null : items.BuildMerchantTradePreview(selected.ItemId, activePane == MerchantPane.MerchantStock);
        Phase20Panel detail;
        if (previewSelected is null)
        {
            detail = new Phase20Panel("Trade", [activePane == MerchantPane.MerchantStock ? "The merchant has no tradable stock." : "You have no tradable items."]);
        }
        else
        {
            int conditionEffect = previewSelected.ConditionPermille - 1000;
            int scarcityEffect = previewSelected.ScarcityPermille - 1000;
            int taxEffect = previewSelected.TaxPermille - 1000;
            int riskEffect = previewSelected.RiskPermille - 1000;
            int relationshipEffect = previewSelected.RelationshipPermille - 1000;
            var lines = new List<string>
            {
                $"Action: {(previewSelected.MerchantSelling ? "Buy from merchant" : "Sell to merchant")}",
                $"Available: {previewSelected.QuantityAvailable}",
                $"Unit price: {previewSelected.UnitPriceMinorUnits} scrip",
                $"Maximum affordable: {previewSelected.MaximumAffordableQuantity}",
                string.Empty,
                "PRICE FACTORS",
                $"Base value: {previewSelected.BaseValueMinorUnits} scrip",
                $"Condition: {SignedPercent(conditionEffect)}",
                $"Scarcity: {SignedPercent(scarcityEffect)}",
                $"Tax: {SignedPercent(taxEffect)}",
                $"Merchant risk: {SignedPercent(riskEffect)}",
                $"Relationship: {SignedPercent(relationshipEffect)}",
                previewSelected.CommerceLabel
            };
            if (activePane == MerchantPane.MerchantStock && selected is not null)
            {
                EquipmentComparisonModel? comparison = items.BuildEquipmentComparisonForCandidate(selected.ItemId);
                if (comparison is not null)
                {
                    lines.Add(string.Empty);
                    lines.Add("EQUIPMENT COMPARISON");
                    lines.AddRange(comparison.Lines.Take(9));
                }
            }
            lines.Add(string.Empty);
            lines.Add("Double-click an item to trade one immediately, or use the quantity buttons below.");
            detail = new Phase20Panel(previewSelected.Name, lines);
        }

        InventoryScreenModel inventory = items.BuildPlayerInventory();
        int maxTrade = previewSelected?.MaximumAffordableQuantity ?? 0;
        IReadOnlyList<Phase20ActionPrompt> actions = activePane == MerchantPane.MerchantStock
            ? [
                new(UiAction.MerchantBuyOne, "Buy 1", maxTrade >= 1, ShowBinding: false),
                new(UiAction.MerchantBuyFive, "Buy 5", maxTrade >= 5, ShowBinding: false),
                new(UiAction.MerchantBuyAll, "Buy Max", maxTrade >= 1, ShowBinding: false),
                new(UiAction.MerchantInspect, "Inspect", selected is not null, ShowBinding: false),
                new(UiAction.MerchantSwitchPane, "Your Inventory", ShowBinding: false),
                new(UiAction.Cancel, "Close")
              ]
            : [
                new(UiAction.MerchantSellOne, "Sell 1", maxTrade >= 1, ShowBinding: false),
                new(UiAction.MerchantSellFive, "Sell 5", maxTrade >= 5, ShowBinding: false),
                new(UiAction.MerchantSellAll, "Sell Max", maxTrade >= 1, ShowBinding: false),
                new(UiAction.MerchantInspect, "Inspect", selected is not null, ShowBinding: false),
                new(UiAction.MerchantSwitchPane, "Merchant Stock", ShowBinding: false),
                new(UiAction.Cancel, "Close")
              ];

        return new Phase20ScreenModel(
            Phase20ScreenKind.Merchant,
            "Prospect Quartermaster",
            $"Your scrip {items.CurrencyBalance(items.PlayerId)} • Merchant funds {items.CurrencyBalance(items.MerchantId)} • Carry {inventory.TotalMassGrams / 1000f:F1}/{inventory.MaximumMassGrams / 1000f:F1} kg • Commerce {items.EffectiveCommerce}",
            merchantEntries,
            merchantSelection,
            [],
            0,
            detail,
            string.Empty,
            string.IsNullOrWhiteSpace(status) ? items.LastStatus : status,
            Presentation: Phase20ScreenPresentation.MerchantTrade,
            ActionPrompts: actions,
            SecondaryEntries: playerEntries,
            SecondarySelectedIndex: playerSelection,
            SecondaryScrollOffset: playerScroll,
            SecondaryTitle: "YOUR INVENTORY",
            SecondaryPaneActive: activePane == MerchantPane.PlayerInventory,
            ScrollOffset: merchantScroll);
    }

    private static string SignedPercent(int permilleDelta)
    {
        double percent = permilleDelta / 10.0;
        return percent > 0 ? $"+{percent:F0}%" : $"{percent:F0}%";
    }

    private static bool SkillMatchesCategory(SkillId skill, SkillCategory category) => category == SkillCategory.All || SkillCategoryFor(skill) == category;

    private static SkillCategory SkillCategoryFor(SkillId skill) => skill switch
    {
        SkillId.Ballistics or SkillId.DirectedEnergy or SkillId.Demolitions or SkillId.Melee or SkillId.Unarmed => SkillCategory.Combat,
        SkillId.Athletics or SkillId.Stealth => SkillCategory.Physical,
        SkillId.Fieldcraft => SkillCategory.Survival,
        SkillId.Security or SkillId.Engineering => SkillCategory.Technical,
        SkillId.Science or SkillId.Medicine => SkillCategory.ScientificMedical,
        SkillId.Influence or SkillId.Commerce => SkillCategory.Social,
        _ => SkillCategory.All
    };

    private static string SkillName(SkillId skill) => skill switch
    {
        SkillId.DirectedEnergy => "Directed Energy",
        _ => SplitPascal(skill.ToString())
    };

    private static string SplitPascal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            if (index > 0 && char.IsUpper(c) && !char.IsUpper(value[index - 1])) result.Append(' ');
            result.Append(c);
        }
        return result.ToString();
    }

    private static string EquipmentSlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Longarm => "Longarm",
        EquipmentSlot.Melee => "Melee Weapon",
        EquipmentSlot.BackEquipment => "Back Equipment",
        EquipmentSlot.QuickConsumable1 => "Quick Aid 1",
        EquipmentSlot.QuickConsumable2 => "Quick Aid 2",
        EquipmentSlot.Torso => "Torso Armor",
        _ => SplitPascal(slot.ToString())
    };

    private static bool PerkMatchesFilter(PerkScreenEntry perk, PerkFilter filter) => filter switch
    {
        PerkFilter.Available => perk.Rank < perk.MaximumRank && perk.UnmetRequirements.Count == 0,
        PerkFilter.Owned => perk.Rank > 0,
        PerkFilter.Locked => perk.Rank == 0 && perk.UnmetRequirements.Count > 0,
        _ => true
    };

    private static string PerkState(PerkScreenEntry perk)
    {
        if (perk.Rank >= perk.MaximumRank) return "MAX";
        if (perk.Rank > 0) return perk.Available ? "OWNED +" : "OWNED";
        if (perk.UnmetRequirements.Count > 0) return "LOCKED";
        return perk.Available ? "AVAILABLE" : "READY";
    }

    private static string FriendlyRequirement(string value)
        => value.Replace("PriorPerk:", "Perk:", StringComparison.OrdinalIgnoreCase)
            .Replace("FactionTraining:", "Faction training:", StringComparison.OrdinalIgnoreCase);

    private static string FriendlyStation(CraftingStationKind station) => station switch
    {
        CraftingStationKind.MedicalStation => "Medical Station",
        CraftingStationKind.ChemistryStation => "Chemistry Station",
        CraftingStationKind.EnergyBench => "Energy Bench",
        CraftingStationKind.CookingFire => "Cooking Fire",
        CraftingStationKind.ArmorFrame => "Armor Frame",
        CraftingStationKind.DiagnosticTable => "Diagnostic Table",
        _ => "Workbench"
    };

    public static Phase20ScreenModel Companion(Phase10CombatController combat, int selection, CompanionScreenSection section, string status)
    {
        CompanionInterfaceSnapshot? companion = combat.BuildCompanionInterfaceSnapshot();
        if (companion is null)
        {
            return GameplayScreen(Phase20ScreenKind.Companion, "Companion", "No recruitable companion is active", [], 0, 9,
                new Phase20Panel("No Active Companion", ["Recruit or meet an allied companion before issuing companion orders."]), string.Empty, status) with
            {
                SubTabs = CompanionTabs,
                SelectedSubTab = (int)section,
                ActionPrompts = [new(UiAction.Cancel, "Close")]
            };
        }

        Phase20MenuEntry[] entries = section switch
        {
            CompanionScreenSection.Commands =>
            [
                new("follow", "Follow", "ISSUE", "Stay close and move with you.", Presentation: Phase20EntryPresentation.Button),
                new("stay", "Wait Here", "ISSUE", "Hold the current position until given another order.", Presentation: Phase20EntryPresentation.Button),
                new("regroup", "Regroup", "ISSUE", "Return immediately to your position.", Presentation: Phase20EntryPresentation.Button),
                new("attack", "Attack Target", "ISSUE", "Engage the nearest current hostile.", Presentation: Phase20EntryPresentation.Button),
                new("cover", "Defend / Take Cover", "ISSUE", "Seek cover and fight defensively.", Presentation: Phase20EntryPresentation.Button),
                new("avoid", "Avoid Combat", "ISSUE", "Avoid initiating combat and prioritize safety.", Presentation: Phase20EntryPresentation.Button)
            ],
            CompanionScreenSection.Behavior =>
            [
                new("prefer-ranged", "Prefer Ranged Weapons", "SET", "Prefer ranged weapons when the current loadout permits it.", Presentation: Phase20EntryPresentation.Button),
                new("prefer-melee", "Prefer Melee Weapons", "SET", "Prefer melee weapons when the current loadout permits it.", Presentation: Phase20EntryPresentation.Button),
                new("cover", "Use Cover", "SET", "Prioritize defensible cover positions under threat.", Presentation: Phase20EntryPresentation.Button),
                new("avoid", "Low Aggression", "SET", "Avoid initiating combat unless directly threatened or ordered.", Presentation: Phase20EntryPresentation.Button)
            ],
            CompanionScreenSection.Equipment =>
            [
                new("equipment:weapon", "Current Weapon", companion.WeaponName, $"Loaded {companion.WeaponAmmunition} • reserve {companion.WeaponReserve}", Enabled: false),
                .. companion.Armor.Select((armor, index) => new Phase20MenuEntry($"equipment:armor:{index}", $"Armor Layer {index + 1}", armor, "Companion equipment is shown from authoritative combat state.", Enabled: false))
            ],
            _ =>
            [
                new("overview:health", "Health", $"{companion.Health} / {companion.MaximumHealth}", "Current combat health.", Enabled: false, Presentation: Phase20EntryPresentation.Progress, NormalizedValue: companion.MaximumHealth <= 0 ? 0 : companion.Health / (float)companion.MaximumHealth),
                new("overview:morale", "Morale", $"{companion.MoralePermille / 10.0:F0}%", "Willingness to keep fighting under pressure.", Enabled: false, Presentation: Phase20EntryPresentation.Progress, NormalizedValue: companion.MoralePermille / 1000f),
                new("overview:stamina", "Stamina", $"{companion.StaminaPermille / 10.0:F0}%", "Current physical reserve.", Enabled: false, Presentation: Phase20EntryPresentation.Progress, NormalizedValue: companion.StaminaPermille / 1000f),
                new("overview:distance", "Distance", $"{companion.DistanceMeters:F0} m", "Current distance from you.", Enabled: false)
            ]
        };

        var detailLines = new List<string>
        {
            $"Status: {companion.Disposition}",
            $"Current activity: {companion.CurrentAction}",
            $"Current order: {companion.CurrentOrder}",
            $"Weapon: {companion.WeaponName} • {companion.WeaponAmmunition}/{companion.WeaponReserve}",
            $"Fear: {SocietyUiText.Bar(companion.FearPermille)} {SocietyUiText.Percent(companion.FearPermille)}",
            $"Duty: {SocietyUiText.Bar(companion.DutyPermille)} {SocietyUiText.Percent(companion.DutyPermille)}"
        };
        if (companion.Armor.Count > 0)
        {
            detailLines.Add("");
            detailLines.Add("EQUIPMENT");
            detailLines.AddRange(companion.Armor);
        }
        if (companion.RecentOrders.Count > 0)
        {
            detailLines.Add("");
            detailLines.Add("RECENT ORDERS");
            detailLines.AddRange(companion.RecentOrders);
        }
        bool issueOrder = section is CompanionScreenSection.Commands or CompanionScreenSection.Behavior;
        return GameplayScreen(Phase20ScreenKind.Companion, companion.DisplayName, $"Companion • {companion.Disposition} • {companion.DistanceMeters:F0} m away", entries, selection, 9,
            new Phase20Panel(section == CompanionScreenSection.Equipment ? "Loadout" : "Companion Status", detailLines, Status: combat.LastCompanionCommandStatus), string.Empty, status) with
        {
            SubTabs = CompanionTabs,
            SelectedSubTab = (int)section,
            Meters =
            [
                new("HEALTH", companion.Health, companion.MaximumHealth, $"{companion.Health}/{companion.MaximumHealth}"),
                new("MORALE", companion.MoralePermille, 1000, $"{companion.MoralePermille / 10.0:F0}%")
            ],
            Presentation = section is CompanionScreenSection.Commands or CompanionScreenSection.Behavior ? Phase20ScreenPresentation.CardGridTwoColumns : Phase20ScreenPresentation.Standard,
            ActionPrompts = issueOrder
                ? [new(UiAction.Confirm, "Issue Order", entries.Length > 0), new(UiAction.Cancel, "Close")]
                : [new(UiAction.Cancel, "Close")]
        };
    }

    public static Phase20ScreenModel Settings(Phase20UserSettings settings, int selection, Phase20SettingsSection section, string status)
    {
        Phase20MenuEntry[] entries = section switch
        {
            Phase20SettingsSection.Gameplay =>
            [
                new("difficulty", "Difficulty", settings.Gameplay.Difficulty.ToString(), Presentation: Phase20EntryPresentation.Dropdown),
                new("tutorials", "Tutorials", settings.Gameplay.TutorialsEnabled ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
                new("autosaves", "Autosaves", settings.Gameplay.AutosavesEnabled ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
                new("autosave-slots", "Autosave slots", settings.Gameplay.AutosaveSlots.ToString(), Presentation: Phase20EntryPresentation.Stepper),
                new("autosave-interval", "Autosave interval", $"{settings.Gameplay.AutosaveIntervalMinutes} min", Presentation: Phase20EntryPresentation.Stepper)
            ],
            Phase20SettingsSection.Controls =>
            [
                new("mouse", "Mouse look sensitivity", settings.Gameplay.MouseSensitivity.ToString("F2"), Presentation: Phase20EntryPresentation.Slider, NormalizedValue: Normalize(settings.Gameplay.MouseSensitivity, 0.02f, 1f)),
                new("gamepad", "Gamepad look sensitivity", settings.Gameplay.GamepadLookSensitivity.ToString("F1"), Presentation: Phase20EntryPresentation.Slider, NormalizedValue: Normalize(settings.Gameplay.GamepadLookSensitivity, 0.2f, 8f)),
                new("invert", "Invert vertical look", settings.Gameplay.InvertVerticalLook ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
                new("aim", "Aim behavior", settings.Accessibility.AimBehavior.ToString(), Presentation: Phase20EntryPresentation.Dropdown),
                new("crouch", "Crouch behavior", settings.Accessibility.CrouchBehavior.ToString(), Presentation: Phase20EntryPresentation.Dropdown),
                new("sprint", "Sprint behavior", settings.Accessibility.SprintBehavior.ToString(), Presentation: Phase20EntryPresentation.Dropdown),
                new("bindings", "Input bindings", "Configure", "Capture arbitrary keyboard, mouse, and controller inputs.", Presentation: Phase20EntryPresentation.Button)
            ],
            Phase20SettingsSection.Video =>
            [
                new("fullscreen", "Fullscreen", settings.Video.Fullscreen ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
                new("resolution", "Resolution", $"{settings.Video.Width} × {settings.Video.Height}", Presentation: Phase20EntryPresentation.Dropdown),
                new("vsync", "Vertical sync", settings.Video.VSync ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
                new("fov", "Field of view", $"{settings.Gameplay.FieldOfViewDegrees:F0}°", Presentation: Phase20EntryPresentation.Slider, NormalizedValue: Normalize(settings.Gameplay.FieldOfViewDegrees, 55f, 110f))
            ],
            Phase20SettingsSection.Audio => BuildAudioEntries(settings.Audio),
            _ => []
        };
        string subtitle = section switch
        {
            Phase20SettingsSection.Gameplay => "Campaign, tutorials, and saving",
            Phase20SettingsSection.Controls => "Mouse, controller, behavior, and bindings",
            Phase20SettingsSection.Video => "Display mode, resolution, sync, and field of view",
            Phase20SettingsSection.Audio => "Independent Playroma Sound and Music mix controls",
            _ => "Settings"
        };
        return new Phase20ScreenModel(Phase20ScreenKind.Settings, "Settings", subtitle, entries, selection, [], 0,
            new Phase20Panel("Changes Apply Immediately", ["Sliders, toggles, and dropdowns are live controls.", "Settings persist across launches.", "Use Reset Section to restore defaults for the visible page."]),
            string.Empty, status, true, false, ActionPrompts:
            [
                new(UiAction.NavigateLeft, "Decrease"),
                new(UiAction.NavigateRight, "Increase"),
                new(UiAction.SettingsReset, "Reset Section", ShowBinding: false),
                new(UiAction.Cancel, "Back")
            ],
            SubTabs: SettingsTabs, SelectedSubTab: (int)section);
    }

    public static Phase20ScreenModel Accessibility(Phase20UserSettings settings, int selection, string status)
    {
        Phase20AccessibilitySettings value = settings.Accessibility;
        Phase20MenuEntry[] entries =
        [
            new("ui-scale", "Interface scale", $"{value.InterfaceScale:F2}x", Presentation: Phase20EntryPresentation.Slider, NormalizedValue: Normalize(value.InterfaceScale, 0.75f, 2f)),
            new("subtitle-scale", "Subtitle scale", $"{value.SubtitleScale:F2}x", Presentation: Phase20EntryPresentation.Slider, NormalizedValue: Normalize(value.SubtitleScale, 0.75f, 2f)),
            new("contrast", "High contrast", value.HighContrast ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
            new("color", "Color-vision preset", value.ColorVisionMode.ToString(), Presentation: Phase20EntryPresentation.Dropdown),
            new("shake", "Camera shake", value.DisableCameraShake ? "Disabled" : "Enabled", Presentation: Phase20EntryPresentation.Toggle),
            new("bob", "Head bob", value.DisableHeadBob ? "Disabled" : "Enabled", Presentation: Phase20EntryPresentation.Toggle),
            new("flashes", "Flashes", value.DisableFlashes ? "Disabled" : "Enabled", Presentation: Phase20EntryPresentation.Toggle),
            new("motion-blur", "Motion blur", value.DisableMotionBlur ? "Disabled" : "Enabled", Presentation: Phase20EntryPresentation.Toggle),
            new("motion", "Reduced motion", value.ReduceMotion ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
            new("reticle", "Persistent reticle", value.PersistentReticle ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
            new("dialogue-pause", "Pause during dialogue", value.PauseDuringDialogue ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
            new("menu-pause", "Pause in menus", value.PauseInMenus ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
            new("focus-camera", "Focus cinematic camera", value.CinematicFocusCamera ? "On" : "Off", Presentation: Phase20EntryPresentation.Toggle),
            new("focus-intensity", "Focus overlay intensity", $"{value.FocusOverlayIntensityPermille / 10.0:F0}%", Presentation: Phase20EntryPresentation.Slider, NormalizedValue: value.FocusOverlayIntensityPermille / 1000f),
            new("focus-flash", "Focus flash intensity", $"{value.FocusFlashIntensityPermille / 10.0:F0}%", Presentation: Phase20EntryPresentation.Slider, NormalizedValue: value.FocusFlashIntensityPermille / 1000f),
            new("aim", "Aim behavior", value.AimBehavior.ToString(), Presentation: Phase20EntryPresentation.Dropdown),
            new("crouch", "Crouch behavior", value.CrouchBehavior.ToString(), Presentation: Phase20EntryPresentation.Dropdown),
            new("sprint", "Sprint behavior", value.SprintBehavior.ToString(), Presentation: Phase20EntryPresentation.Dropdown)
        ];
        return new Phase20ScreenModel(Phase20ScreenKind.Accessibility, "Accessibility", "Legibility, visual comfort, motion, and control behaviors", entries, selection, [], 0,
            new Phase20Panel("Accessibility Contract", ["Every setting applies without requiring a new campaign.", "Roentgen Focus forecasts remain readable in high-contrast mode.", "All essential screens support keyboard, mouse, and gamepad navigation."]),
            string.Empty, status, true, false, ActionPrompts:
            [
                new(UiAction.NavigateLeft, "Decrease"),
                new(UiAction.NavigateRight, "Increase"),
                new(UiAction.SettingsReset, "Reset Accessibility", ShowBinding: false),
                new(UiAction.Cancel, "Back")
            ],
            SubTabs: SettingsTabs, SelectedSubTab: (int)Phase20SettingsSection.Accessibility);
    }

    private static Phase20MenuEntry[] BuildAudioEntries(Phase20AudioInterfaceSettings audio) =>
    [
        AudioSlider("audio-master", "Master volume", audio.MasterPermille),
        AudioSlider("audio-music", "Music volume", audio.MusicPermille),
        AudioSlider("audio-interface", "Interface volume", audio.InterfacePermille),
        AudioSlider("audio-weapons", "Weapons volume", audio.WeaponsPermille),
        AudioSlider("audio-creatures", "Creatures volume", audio.CreaturesPermille),
        AudioSlider("audio-foley", "Foley volume", audio.FoleyPermille),
        AudioSlider("audio-ambience", "Ambience volume", audio.AmbiencePermille),
        AudioSlider("audio-focus", "Roentgen Focus volume", audio.FocusPermille)
    ];

    private static Phase20MenuEntry AudioSlider(string id, string label, int permille) => new(id, label, $"{permille / 10.0:F0}%", Presentation: Phase20EntryPresentation.Slider, NormalizedValue: permille / 1000f);
    private static float Normalize(float value, float minimum, float maximum) => maximum <= minimum ? 0f : Math.Clamp((value - minimum) / (maximum - minimum), 0f, 1f);

    public static Phase20ScreenModel InputBindings(Phase20UserSettings settings, int selection, string status, UiAction? capturingAction = null)
    {
        Phase20MenuEntry[] entries = settings.Bindings.OrderBy(static value => value.Key).Select(value =>
        {
            InputBinding binding = value.Value;
            string keyboard = string.IsNullOrWhiteSpace(binding.KeyboardLabel) ? "-" : binding.KeyboardLabel;
            string mouse = string.IsNullOrWhiteSpace(binding.MouseLabel) ? "-" : binding.MouseLabel;
            string gamepad = string.IsNullOrWhiteSpace(binding.GamepadLabel) ? "-" : binding.GamepadLabel;
            bool listening = capturingAction == value.Key;
            return new Phase20MenuEntry(value.Key.ToString(), value.Key.ToString(), listening ? "PRESS INPUT..." : $"{keyboard} | {mouse} | {gamepad}", "Keyboard | mouse | gamepad", true, false, listening ? "LISTENING" : null);
        }).ToArray();
        IReadOnlyList<string> help = capturingAction.HasValue
            ? ["Press any keyboard key, mouse button, or controller button.", "Cancel aborts capture without changing the existing binding.", "Conflicts offer Swap, Replace, or Cancel before anything is changed."]
            : ["Select an action to enter binding capture.", "Mouse buttons and controller buttons can be assigned directly.", "All UI prompts are generated from the active binding profile."];
        return new Phase20ScreenModel(Phase20ScreenKind.InputBindings, "Input Bindings", "Direct capture with conflict resolution", entries, selection, [], 0, new Phase20Panel("Remapping", help), string.Empty, status);
    }

    public static Phase20ScreenModel BindingConflict(UiBindingConflict conflict, int selection, string status)
    {
        string existing = string.Join(", ", conflict.ConflictingActions);
        Phase20MenuEntry[] entries =
        [
            new("swap", "Swap bindings", Enabled: conflict.ConflictingActions.Count == 1, Description: "Assign the new input here and move this action's previous input to the conflicting action."),
            new("replace", "Replace conflicting binding", Dangerous: true, Description: "Assign the new input here and remove it from every conflicting action."),
            new("cancel", "Cancel", Description: "Keep all current bindings unchanged.")
        ];
        return new Phase20ScreenModel(Phase20ScreenKind.BindingConflict, "Binding Conflict", $"{conflict.Incoming} is already assigned", entries, selection, [], 0, new Phase20Panel("Conflict", [$"Target action: {conflict.TargetAction}", $"Already assigned to: {existing}", "Choose how this collision should be resolved."]), string.Empty, status, true, true);
    }

    public static Phase20ScreenModel SaveSlots(Phase20ScreenKind kind, IReadOnlyList<SaveSlotSummary> saves, int selection, string status)
    {
        var entries = new List<Phase20MenuEntry>();
        if (kind == Phase20ScreenKind.SaveGame) entries.Add(new Phase20MenuEntry("new-manual", "Create New Manual Save", "New slot"));
        entries.AddRange(saves.Select(save => new Phase20MenuEntry(
            save.Metadata.SlotId,
            save.Metadata.DisplayName,
            save.Metadata.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            $"{save.Metadata.LocationName} • Level {save.Metadata.CharacterLevel} • {TimeSpan.FromSeconds(save.Metadata.PlayTimeSeconds):hh\\:mm\\:ss}",
            save.IsValid,
            false,
            save.Metadata.Kind.ToString())));
        return new Phase20ScreenModel(kind, kind == Phase20ScreenKind.SaveGame ? "Save Game" : "Load Game", "Checksummed slots with atomic replacement and backup recovery", entries, selection, [], 0, new Phase20Panel("Save Safety", ["Primary and backup files are validated before load.", "A damaged primary file can recover from its last valid backup.", "Generator versions and content fingerprints are preserved."]), kind == Phase20ScreenKind.SaveGame ? "ENTER SAVE  •  DELETE REMOVE  •  ESC BACK" : "ENTER LOAD  •  DELETE REMOVE  •  ESC BACK", status);
    }

    public static Phase20ScreenModel Tutorials(int selection, string status)
    {
        Phase20MenuEntry[] entries = Phase20TutorialCatalog.All.Select(value => new Phase20MenuEntry(value.Id, value.Title, value.Trigger, value.Body, true, false, value.CanReplay ? "REPLAYABLE" : null)).ToArray();
        TutorialCard? selected = entries.Length == 0 ? null : Phase20TutorialCatalog.All[Math.Clamp(selection, 0, entries.Length - 1)];
        return new Phase20ScreenModel(Phase20ScreenKind.Tutorials, "Tutorials", "Contextual lessons can be replayed at any time", entries, selection, [], 0, selected is null ? null : new Phase20Panel(selected.Title, [selected.Body, $"Trigger: {selected.Trigger}"]), "ENTER REPLAY  •  ESC BACK", status);
    }

    public static Phase20ScreenModel Credits(ILocalizationService text, string status) => new(
        Phase20ScreenKind.Credits,
        "Credits and Notices",
        "Roentgen Phase 20",
        [new("close", "Return")],
        0,
        [],
        0,
        new Phase20Panel("Credits", [text.Get("credits.body"), "Complete license texts and third-party notices are included in the licenses directory.", "The GeneralUser GS SoundFont remains intentionally bundled for music quality."]),
        "ENTER OR ESC RETURN",
        status);

    public static Phase20ScreenModel Confirmation(string title, string body, int selection, string status) => new(
        Phase20ScreenKind.Confirmation,
        title,
        body,
        [new("yes", "Confirm", Dangerous: true), new("no", "Cancel")],
        selection,
        [],
        0,
        null,
        "ENTER SELECT  •  ESC CANCEL",
        status,
        true,
        true);

    private static Phase20ScreenModel BuildAttributes(Phase20CharacterCreation creation, int selection, string status)
    {
        Phase20MenuEntry[] entries = ProgressionRules.AttributeOrder.Select(id => new Phase20MenuEntry(id.ToString(), id.ToString(), creation.Attributes[id].ToString(), AttributeDescription(id),
            Presentation: Phase20EntryPresentation.Stepper, NormalizedValue: Normalize(creation.Attributes[id], ProgressionRules.MinimumAttribute, ProgressionRules.MaximumAttribute))).ToArray();
        AttributeId selected = ProgressionRules.AttributeOrder[Math.Clamp(selection, 0, ProgressionRules.AttributeOrder.Count - 1)];
        return new Phase20ScreenModel(Phase20ScreenKind.CharacterAttributes, "Attributes", $"Points remaining: {creation.AttributePointsRemaining}", entries, selection, CreationTabs(1), 1,
            new Phase20Panel(selected.ToString(), [AttributeDescription(selected), $"Range: {ProgressionRules.MinimumAttribute}–{ProgressionRules.MaximumAttribute}", "Spend all creation points before continuing."]),
            string.Empty, status, ActionPrompts:
            [
                new(UiAction.WizardRecommended, "Recommended", ShowBinding: false),
                new(UiAction.WizardReset, "Reset", ShowBinding: false),
                new(UiAction.WizardBack, "Back", ShowBinding: false),
                new(UiAction.WizardNext, "Next: Skills", creation.AttributePointsRemaining == 0, ShowBinding: false, Tooltip: creation.AttributePointsRemaining == 0 ? "Continue" : "Spend all attribute points first")
            ]);
    }

    private static Phase20ScreenModel BuildSpecializations(Phase20CharacterCreation creation, int selection, string status)
    {
        Phase20MenuEntry[] entries = ProgressionRules.SkillOrder.Select(id => new Phase20MenuEntry(id.ToString(), id.ToString(), creation.SpecializedSkills.Contains(id) ? "Specialized" : "Select", SkillDescription(id), true, false, creation.SpecializedSkills.Contains(id) ? "SELECTED" : null)).ToArray();
        SkillId selected = ProgressionRules.SkillOrder[Math.Clamp(selection, 0, ProgressionRules.SkillOrder.Count - 1)];
        return new Phase20ScreenModel(Phase20ScreenKind.CharacterSkills, "Specialized Skills", $"Selected {creation.SpecializedSkills.Count}/3", entries, selection, CreationTabs(2), 2,
            new Phase20Panel(selected.ToString(), [SkillDescription(selected), "Specialized skills receive a substantial starting bonus.", "Choose exactly three. Deselect one before choosing another when all three slots are full."]),
            string.Empty, status, ActionPrompts:
            [
                new(UiAction.WizardBack, "Back", ShowBinding: false),
                new(UiAction.WizardNext, "Next: Background", creation.SpecializedSkills.Count == 3, ShowBinding: false)
            ],
            Presentation: Phase20ScreenPresentation.CardGridTwoColumns);
    }

    private static Phase20ScreenModel BuildBackgrounds(Phase20CharacterCreation creation, int selection, string status)
    {
        Phase20MenuEntry[] entries = Phase21RpgContent.Backgrounds.Select(value => new Phase20MenuEntry(value.Id, value.Name, value.Id == creation.BackgroundId ? "Selected" : "Choose", value.Description, true, false, value.Id == creation.BackgroundId ? "ACTIVE" : null)).ToArray();
        BackgroundDefinition selected = Phase21RpgContent.Backgrounds[Math.Clamp(selection, 0, Phase21RpgContent.Backgrounds.Count - 1)];
        return new Phase20ScreenModel(Phase20ScreenKind.CharacterBackground, "Background", "Prior work shapes knowledge, dialogue, bonuses, and starting equipment", entries, selection, CreationTabs(3), 3,
            new Phase20Panel(selected.Name, [selected.Description, $"Equipment package: {selected.Equipment.PackageId}", $"Starting items: {string.Join(", ", selected.Equipment.ItemDefinitionIds.Select(PlayerFacingDefinitionName))}", $"Starting benefit: {PlayerFacingDefinitionName(selected.StartingBenefitId)}"]),
            string.Empty, status, ActionPrompts:
            [
                new(UiAction.WizardBack, "Back", ShowBinding: false),
                new(UiAction.WizardNext, "Next: Traits", ShowBinding: false)
            ],
            Presentation: Phase20ScreenPresentation.CardGridTwoColumns);
    }

    private static Phase20ScreenModel BuildTraits(Phase20CharacterCreation creation, int selection, string status)
    {
        Phase20MenuEntry[] entries = Phase21RpgContent.Traits.Select(value => new Phase20MenuEntry(value.Id, value.Name, creation.TraitIds.Contains(value.Id) ? "Selected" : "Select", value.Description, true, false, creation.TraitIds.Contains(value.Id) ? "ACTIVE" : null)).ToArray();
        TraitDefinition selected = Phase21RpgContent.Traits[Math.Clamp(selection, 0, Phase21RpgContent.Traits.Count - 1)];
        return new Phase20ScreenModel(Phase20ScreenKind.CharacterTraits, "Traits", $"Selected {creation.TraitIds.Count}/2", entries, selection, CreationTabs(4), 4,
            new Phase20Panel(selected.Name, [selected.Description, $"Incompatible: {(selected.IncompatibleTraitIds.Count == 0 ? "none" : string.Join(", ", selected.IncompatibleTraitIds.Select(PlayerFacingDefinitionName)))}", "Choose exactly two traits. The interface will never silently replace one of your choices."]),
            string.Empty, status, ActionPrompts:
            [
                new(UiAction.WizardBack, "Back", ShowBinding: false),
                new(UiAction.WizardNext, "Next: Review", creation.TraitIds.Count == 2, ShowBinding: false)
            ],
            Presentation: Phase20ScreenPresentation.CardGridTwoColumns);
    }

    private static Phase20ScreenModel BuildReview(Phase20CharacterCreation creation, CampaignSeed seed, int selection, string status)
    {
        Phase20MenuEntry[] entries =
        [
            new("identity", "Identity", creation.CharacterName, "Edit character name and campaign parameters.", Badge: "EDIT"),
            new("attributes", "Attributes", string.Join("  ", creation.Attributes.Select(value => $"{value.Key} {value.Value}")), "Edit attribute allocation.", Badge: "EDIT"),
            new("skills", "Specialized Skills", string.Join(", ", creation.SpecializedSkills), "Edit specialized skills.", Badge: "EDIT"),
            new("background", "Background", Phase21RpgContent.Backgrounds.First(value => value.Id == creation.BackgroundId).Name, "Edit background.", Badge: "EDIT"),
            new("traits", "Traits", string.Join(", ", creation.TraitIds.Select(PlayerFacingDefinitionName)), "Edit traits.", Badge: "EDIT")
        ];
        return new Phase20ScreenModel(Phase20ScreenKind.CharacterReview, "Review Character", $"Campaign seed 0x{seed.Value:X16}", entries, selection, CreationTabs(5), 5,
            new Phase20Panel(creation.CharacterName, [
                $"Background: {Phase21RpgContent.Backgrounds.First(value => value.Id == creation.BackgroundId).Name}",
                $"Specializations: {string.Join(", ", creation.SpecializedSkills)}",
                $"Traits: {string.Join(", ", creation.TraitIds.Select(PlayerFacingDefinitionName))}",
                $"Starting package: {PlayerFacingDefinitionName(creation.EquipmentPackageId)}",
                "Select any row to edit that section, or begin the campaign when satisfied."
            ]),
            string.Empty, status, ActionPrompts:
            [
                new(UiAction.WizardBack, "Back", ShowBinding: false),
                new(UiAction.WizardFinish, "Begin Campaign", !string.IsNullOrWhiteSpace(creation.CharacterName) && creation.AttributePointsRemaining == 0 && creation.SpecializedSkills.Count == 3 && creation.TraitIds.Count == 2, ShowBinding: false)
            ]);
    }

    private static string PlayerFacingDefinitionName(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        string tail = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
        return string.Join(' ', tail.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static bool JournalMatchesFilter(QuestStatus status, JournalFilter filter) => filter switch
    {
        JournalFilter.Active => status is QuestStatus.Generated or QuestStatus.Offered or QuestStatus.Active or QuestStatus.Suspended,
        JournalFilter.Completed => status == QuestStatus.Completed,
        JournalFilter.Failed => status == QuestStatus.Failed,
        JournalFilter.Campaign => false,
        _ => true
    };

    private static bool TryParseJournalId(string id, string prefix, out StableId value)
    {
        string marker = prefix + ":";
        if (!id.StartsWith(marker, StringComparison.Ordinal)) { value = StableId.Empty; return false; }
        return StableId.TryParse(id[marker.Length..], out value);
    }

    private static Phase20Panel BuildItemDetail(Phase13ItemRuntime items, StableId itemId)
    {
        ItemInspectionModel detail = items.BuildInspection(itemId);
        return new Phase20Panel(detail.Title, detail.Details);
    }

    private static Phase20ScreenModel GameplayScreen(Phase20ScreenKind kind, string title, string subtitle, IReadOnlyList<Phase20MenuEntry> entries, int selection, int tab, Phase20Panel? detail, string footer, string status) =>
        new(kind, title, subtitle, entries, selection, GameplayTabs, tab, detail, footer, status);

    private static string[] CreationTabs(int active) => ["Campaign", "Attributes", "Skills", "Background", "Traits", "Review"];

    private static string AttributeDescription(AttributeId id) => id switch
    {
        AttributeId.Physique => "Melee force, carrying, heavy weapons, forced entry, and knockdown resistance.",
        AttributeId.Awareness => "Detection, ranged accuracy, traps, Focus clarity, observation, and initiative.",
        AttributeId.Vitality => "Health, stamina, poison resistance, contamination tolerance, and recovery.",
        AttributeId.Presence => "Persuasion, deception, leadership, companion confidence, prices, and first impressions.",
        AttributeId.Intellect => "Skill points, engineering, science, medicine, security, and crafting discovery.",
        AttributeId.Reflex => "Action capacity, movement, reload speed, stealth, weapon draw, and evasion.",
        AttributeId.Fortune => "Critical events, unusual loot, accidental discoveries, and rare dialogue opportunities.",
        _ => id.ToString()
    };

    private static string SkillDescription(SkillId id) => id switch
    {
        SkillId.Ballistics => "Pistols, rifles, shotguns, and conventional firearms.",
        SkillId.DirectedEnergy => "Beam, arc, plasma, and radiographic weapons.",
        SkillId.Demolitions => "Explosives, mines, grenades, and traps.",
        SkillId.Melee => "Knives, clubs, axes, polearms, and powered weapons.",
        SkillId.Unarmed => "Strikes, grappling, and natural weapons.",
        SkillId.Athletics => "Climbing, sprinting, jumping, and physical traversal.",
        SkillId.Stealth => "Concealment, noise control, and silent attacks.",
        SkillId.Security => "Locks, alarms, traps, and physical bypasses.",
        SkillId.Engineering => "Repair, machinery, weapons, and construction.",
        SkillId.Science => "Computers, laboratories, energy systems, and analysis.",
        SkillId.Medicine => "Healing, surgery, drugs, anatomy, and diagnosis.",
        SkillId.Fieldcraft => "Hunting, scavenging, cooking, tracking, and wilderness survival.",
        SkillId.Influence => "Persuasion, deception, intimidation, and leadership.",
        SkillId.Commerce => "Barter, appraisal, negotiation, and market knowledge.",
        _ => id.ToString()
    };
}
