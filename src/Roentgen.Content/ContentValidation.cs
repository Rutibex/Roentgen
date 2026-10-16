namespace Roentgen.Content;

public sealed record ContentValidationIssue(string Code, string SubjectId, string Message, bool IsCritical = true);
public sealed record ContentValidationReport(bool IsValid, string Fingerprint, IReadOnlyList<ContentValidationIssue> Issues)
{
    public void ThrowIfInvalid()
    {
        if (!IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, Issues.Select(static issue => $"{issue.Code} [{issue.SubjectId}] {issue.Message}")));
    }
}

public sealed class ContentPackValidator
{
    private static readonly HashSet<string> CombatRoutes = new(StringComparer.OrdinalIgnoreCase) { "combat", "ballistics", "demolitions" };

    public ContentValidationReport Validate(LaunchContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var issues = new List<ContentValidationIssue>();
        if (pack.SchemaVersion != ModuleInfo.SchemaVersion) Add("schema.version", pack.PackageId, $"Expected schema {ModuleInfo.SchemaVersion}; found {pack.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(pack.PackageId)) Add("package.id", "package", "Package ID is required.");
        if (string.IsNullOrWhiteSpace(pack.ContentVersion)) Add("package.version", pack.PackageId, "Content version is required.");

        Exact(pack.Regions, ContentBudgets.RegionFamilies, "regions");
        Exact(pack.SettlementGrammars, ContentBudgets.SettlementGrammars, "settlement-grammars");
        Exact(pack.StructureGrammars, ContentBudgets.StructureGrammars, "structure-grammars");
        Exact(pack.RoomMotifs, ContentBudgets.RoomMotifs, "room-motifs");
        Exact(pack.DamageProfiles, ContentBudgets.DamageProfiles, "damage-profiles");
        Exact(pack.Factions, ContentBudgets.MajorFactions, "factions");
        Exact(pack.Organizations, ContentBudgets.MinorOrganizations, "organizations");
        Exact(pack.HumanoidArchetypes, ContentBudgets.HumanoidArchetypes, "humanoid-archetypes");
        Exact(pack.CreatureFamilies, ContentBudgets.CreatureFamilies, "creature-families");
        Exact(pack.RobotChassis, ContentBudgets.RobotChassis, "robot-chassis");
        Exact(pack.CompanionArchetypes, ContentBudgets.CompanionArchetypes, "companion-archetypes");
        Exact(pack.WeaponFamilies, ContentBudgets.WeaponFamilies, "weapon-families");
        Exact(pack.WeaponComponents, ContentBudgets.WeaponComponents, "weapon-components");
        Exact(pack.ArmorFamilies, ContentBudgets.ArmorFamilies, "armor-families");
        Exact(pack.ArmorComponents, ContentBudgets.ArmorComponents, "armor-components");
        Exact(pack.Consumables, ContentBudgets.Consumables, "consumables");
        Exact(pack.CraftingProcesses, ContentBudgets.CraftingProcesses, "crafting-processes");
        Exact(pack.Perks, ContentBudgets.Perks, "perks");
        Exact(pack.Backgrounds, ContentBudgets.Backgrounds, "backgrounds");
        Exact(pack.Traits, ContentBudgets.Traits, "traits");
        Exact(pack.QuestKernels, ContentBudgets.QuestKernels, "quest-kernels");
        Exact(pack.CampaignKernels, ContentBudgets.CampaignKernels, "campaign-kernels");
        Exact(pack.DialogueActs, ContentBudgets.DialogueActs, "dialogue-acts");
        Exact(pack.RumorTemplates, ContentBudgets.RumorTemplates, "rumor-templates");
        Exact(pack.Tutorials, ContentBudgets.Tutorials, "tutorials");
        Exact(pack.EpilogueRules, ContentBudgets.EpilogueRules, "epilogue-rules");
        Exact(pack.AudioProfiles, ContentBudgets.AudioProfiles, "audio-profiles");
        Exact(pack.MusicBindings, ContentBudgets.MusicBindings, "music-bindings");

        Unique(pack.Regions.Select(static value => value.Id), "region");
        Unique(pack.SettlementGrammars.Select(static value => value.Id), "settlement");
        Unique(pack.StructureGrammars.Select(static value => value.Id), "structure");
        Unique(pack.RoomMotifs.Select(static value => value.Id), "room-motif");
        Unique(pack.DamageProfiles.Select(static value => value.Id), "damage-profile");
        Unique(pack.Factions.Select(static value => value.Id), "faction");
        Unique(pack.Organizations.Select(static value => value.Id), "organization");
        Unique(pack.HumanoidArchetypes.Select(static value => value.Id), "humanoid");
        Unique(pack.CreatureFamilies.Select(static value => value.Id), "creature");
        Unique(pack.RobotChassis.Select(static value => value.Id), "robot");
        Unique(pack.CompanionArchetypes.Select(static value => value.Id), "companion");
        Unique(pack.WeaponFamilies.Select(static value => value.Id), "weapon");
        Unique(pack.WeaponComponents.Select(static value => value.Id), "weapon-component");
        Unique(pack.ArmorFamilies.Select(static value => value.Id), "armor");
        Unique(pack.ArmorComponents.Select(static value => value.Id), "armor-component");
        Unique(pack.Consumables.Select(static value => value.Id), "consumable");
        Unique(pack.CraftingProcesses.Select(static value => value.Id), "crafting");
        Unique(pack.Perks.Select(static value => value.Id), "perk");
        Unique(pack.Backgrounds.Select(static value => value.Id), "background");
        Unique(pack.Traits.Select(static value => value.Id), "trait");
        Unique(pack.QuestKernels.Select(static value => value.Id), "quest");
        Unique(pack.CampaignKernels.Select(static value => value.Id), "campaign");
        Unique(pack.DialogueActs.Select(static value => value.Id), "dialogue");
        Unique(pack.RumorTemplates.Select(static value => value.Id), "rumor");
        Unique(pack.Tutorials.Select(static value => value.Id), "tutorial");
        Unique(pack.EpilogueRules.Select(static value => value.Id), "epilogue");
        Unique(pack.AudioProfiles.Select(static value => value.Id), "audio");
        Unique(pack.MusicBindings.Select(static value => value.Id), "music");

        var regions = Ids(pack.Regions, static value => value.Id);
        var factions = Ids(pack.Factions, static value => value.Id);
        var structures = Ids(pack.StructureGrammars, static value => value.Id);
        var motifs = Ids(pack.RoomMotifs, static value => value.Id);
        var damage = Ids(pack.DamageProfiles, static value => value.Id);
        var quests = Ids(pack.QuestKernels, static value => value.Id);
        var epilogues = Ids(pack.EpilogueRules, static value => value.Id);
        var audio = Ids(pack.AudioProfiles, static value => value.Id);
        var music = Ids(pack.MusicBindings, static value => value.Id);
        var weapons = Ids(pack.WeaponFamilies, static value => value.Id);
        var armor = Ids(pack.ArmorFamilies, static value => value.Id);
        var traits = Ids(pack.Traits, static value => value.Id);

        foreach (RegionContentDefinition region in pack.Regions)
        {
            Reference(audio, region.AudioProfileId, "region.audio", region.Id);
            Reference(music, region.MusicProfileId, "region.music", region.Id);
            if (region.LandmarkFamilies.Count < 5) Add("region.landmarks", region.Id, "Each region requires at least five major landmark families.");
            if (region.Hazards.Count == 0 || region.TerrainTags.Count == 0) Add("region.identity", region.Id, "Terrain and hazard identity must be authored.");
        }

        foreach (SettlementGrammarContentDefinition settlement in pack.SettlementGrammars)
        {
            References(regions, settlement.CompatibleRegions, "settlement.region", settlement.Id);
            References(structures, settlement.StructureGrammarIds, "settlement.structure", settlement.Id);
            string[] essentials = ["water", "food", "market", "repair", "security"];
            foreach (string essential in essentials)
                if (!settlement.ServiceTags.Contains(essential, StringComparer.Ordinal)) Add("settlement.service", settlement.Id, $"Missing essential service tag '{essential}'.");
        }

        foreach (StructureGrammarContentDefinition structure in pack.StructureGrammars)
        {
            References(regions, structure.CompatibleRegions, "structure.region", structure.Id);
            References(motifs, structure.RoomMotifIds, "structure.room-motif", structure.Id);
            References(damage, structure.DamageProfileIds, "structure.damage", structure.Id);
            Reference(audio, structure.AudioProfileId, "structure.audio", structure.Id);
            if (structure.MinimumFloors < 1 || structure.MaximumFloors < structure.MinimumFloors) Add("structure.floors", structure.Id, "Floor range is invalid.");
            if (!structure.QuestReservationTags.Contains("alternate-route", StringComparer.Ordinal)) Add("structure.quest-route", structure.Id, "Structure grammar lacks alternate-route reservation support.");
        }

        foreach (OrganizationContentDefinition organization in pack.Organizations)
        {
            References(factions, organization.CompatibleFactions, "organization.faction", organization.Id);
            References(regions, organization.CompatibleRegions, "organization.region", organization.Id);
        }

        foreach (HumanoidArchetypeContentDefinition humanoid in pack.HumanoidArchetypes)
        {
            Reference(audio, humanoid.AudioProfileId, "humanoid.audio", humanoid.Id);
            References(factions, humanoid.CompatibleFactions, "humanoid.faction", humanoid.Id);
            if (humanoid.SurrenderThresholdPermille is < 0 or > 1000) Add("humanoid.surrender", humanoid.Id, "Surrender threshold is outside 0..1000.");
        }

        foreach (CreatureFamilyContentDefinition creature in pack.CreatureFamilies)
        {
            Reference(audio, creature.AudioProfileId, "creature.audio", creature.Id);
            if (creature.TargetParts.Count < 4) Add("creature.target-parts", creature.Id, "Every creature family requires at least four consequential target parts.");
            if (creature.InjuryConsequences.Count < 2) Add("creature.injuries", creature.Id, "Every creature family requires functional injury consequences.");
        }

        foreach (RobotChassisContentDefinition robot in pack.RobotChassis)
        {
            Reference(audio, robot.AudioProfileId, "robot.audio", robot.Id);
            if (robot.TargetParts.Count < 7 || robot.ComponentConsequences.Count < 7) Add("robot.components", robot.Id, "Robot chassis lacks complete targetable component consequences.");
        }

        foreach (CompanionArchetypeContentDefinition companion in pack.CompanionArchetypes)
        {
            Reference(quests, companion.PersonalQuestKernelId, "companion.quest", companion.Id);
            Reference(epilogues, companion.EndingRuleId, "companion.epilogue", companion.Id);
            Reference(audio, companion.AudioProfileId, "companion.audio", companion.Id);
            Reference(music, companion.MusicProfileId, "companion.music", companion.Id);
        }

        foreach (WeaponFamilyContentDefinition weapon in pack.WeaponFamilies)
        {
            Reference(audio, weapon.AudioProfileId, "weapon.audio", weapon.Id);
            if (weapon.ComponentSlots.Count < 5) Add("weapon.components", weapon.Id, "Weapon family has too few component slots for meaningful procedural construction.");
            if (weapon.BuildDirections.Count < 3) Add("weapon.builds", weapon.Id, "Weapon family requires three recognizable build directions.");
        }
        foreach (WeaponComponentContentDefinition component in pack.WeaponComponents)
            References(weapons, component.CompatibleWeaponFamilies, "weapon-component.family", component.Id);

        foreach (ArmorFamilyContentDefinition family in pack.ArmorFamilies)
        {
            Reference(audio, family.AudioProfileId, "armor.audio", family.Id);
            if (family.Layers.Count < 6 || family.CoverageTags.Count < 6) Add("armor.layers", family.Id, "Armor family does not cover the launch layered-equipment model.");
        }
        foreach (ArmorComponentContentDefinition component in pack.ArmorComponents)
            References(armor, component.CompatibleArmorFamilies, "armor-component.family", component.Id);

        int behaviorPercent = pack.Perks.Count == 0 ? 0 : pack.Perks.Count(static perk => perk.Behavioral) * 100 / pack.Perks.Count;
        if (behaviorPercent < 70) Add("perk.behavior-ratio", "perks", $"Behavioral/option perks are only {behaviorPercent}%; minimum is 70%.");
        foreach (PerkContentDefinition perk in pack.Perks)
        {
            if (string.IsNullOrWhiteSpace(perk.EffectType) || string.IsNullOrWhiteSpace(perk.EffectTarget)) Add("perk.effect", perk.Id, "Perk effect is incomplete.");
            if (perk.MaximumRank < 1) Add("perk.rank", perk.Id, "Maximum rank must be positive.");
        }

        foreach (TraitContentDefinition trait in pack.Traits)
            References(traits, trait.IncompatibleTraitIds, "trait.incompatible", trait.Id);

        foreach (QuestKernelContentDefinition quest in pack.QuestKernels)
        {
            References(regions, quest.CompatibleRegions, "quest.region", quest.Id);
            References(factions, quest.CompatibleFactions, "quest.faction", quest.Id);
            if (quest.SolutionRoutes.Count < 3) Add("quest.routes", quest.Id, "Major quest kernel requires at least three solution routes.");
            int computedNonCombat = quest.SolutionRoutes.Count(route => !CombatRoutes.Contains(route));
            if (quest.NonCombatRouteCount < 1 || computedNonCombat < 1) Add("quest.noncombat", quest.Id, "Quest kernel lacks a noncombat route.");
            if (string.IsNullOrWhiteSpace(quest.HiddenFact)) Add("quest.hidden-fact", quest.Id, "Quest kernel lacks a hidden fact.");
            if (quest.RecoveryKinds.Count == 0) Add("quest.recovery", quest.Id, "Quest kernel lacks failure recovery.");
            if (quest.ConsequenceTags.Count == 0 || quest.FollowUpIds.Count == 0) Add("quest.consequence", quest.Id, "Quest kernel lacks consequence or follow-up content.");
        }

        string[] campaignRoles = ["opening-crisis", "regional-crisis", "faction-introduction", "revelation", "regional-expansion", "irreversible-choice", "counteraction", "final-convergence"];
        foreach (string role in campaignRoles)
            if (!pack.CampaignKernels.Any(kernel => string.Equals(kernel.Role, role, StringComparison.Ordinal))) Add("campaign.role", role, "No campaign kernel supplies this dramatic role.");
        if (pack.CampaignKernels.Count(static kernel => kernel.Role == "revelation") < 3) Add("campaign.revelations", "campaign", "Campaign library requires at least three revelation kernels.");
        if (pack.CampaignKernels.Count(static kernel => kernel.Irreversible) < 2) Add("campaign.irreversible", "campaign", "Campaign library requires multiple irreversible choices.");

        foreach (DialogueActContentDefinition act in pack.DialogueActs)
            if (!act.KnowledgeFilter || !act.RelationshipAware || !act.FactionTerminology) Add("dialogue.context", act.Id, "Dialogue acts must account for knowledge, relationships and faction terminology.");
        foreach (TutorialContentDefinition tutorial in pack.Tutorials)
        {
            if (!tutorial.Replayable || !tutorial.Skippable) Add("tutorial.accessibility", tutorial.Id, "Tutorial must be replayable and skippable.");
            if (string.IsNullOrWhiteSpace(tutorial.Title) || string.IsNullOrWhiteSpace(tutorial.Body)) Add("tutorial.presentation", tutorial.Id, "Tutorial title and body are required.");
        }

        if (pack.EpilogueRules.Count < 50) Add("epilogue.coverage", "epilogues", "Epilogue library is below the systemic coverage floor.");
        if (!pack.AudioProfiles.Any(profile => profile.Id == "audio.interface.complete")) Add("audio.interface", "audio", "Complete interface semantic audio profile is missing.");
        if (pack.MusicBindings.Count(binding => binding.Category == "region") != ContentBudgets.RegionFamilies) Add("music.regions", "music", "Every region requires one music binding.");
        if (pack.MusicBindings.Count(binding => binding.Category == "faction") != ContentBudgets.MajorFactions) Add("music.factions", "music", "Every major faction requires one music binding.");
        if (pack.MusicBindings.Count(binding => binding.Category == "companion") != ContentBudgets.CompanionArchetypes) Add("music.companions", "music", "Every companion requires one music binding.");

        string fingerprint = ContentPackLoader.ComputeFingerprint(pack);
        return new ContentValidationReport(issues.All(static issue => !issue.IsCritical), fingerprint, issues);

        void Add(string code, string subject, string message) => issues.Add(new ContentValidationIssue(code, subject, message));
        void Exact<T>(IReadOnlyCollection<T> values, int expected, string category)
        {
            if (values.Count != expected) Add("budget." + category, category, $"Expected exactly {expected}; found {values.Count}.");
        }
        void Unique(IEnumerable<string> values, string category)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in values)
            {
                if (string.IsNullOrWhiteSpace(id)) Add("id.empty", category, "Content ID is empty.");
                else if (!seen.Add(id)) Add("id.duplicate", id, $"Duplicate {category} ID.");
            }
        }
        void Reference(IReadOnlySet<string> available, string id, string code, string subject)
        {
            if (string.IsNullOrWhiteSpace(id) || !available.Contains(id)) Add(code, subject, $"Reference '{id}' is missing.");
        }
        void References(IReadOnlySet<string> available, IEnumerable<string> ids, string code, string subject)
        {
            foreach (string id in ids) Reference(available, id, code, subject);
        }
    }

    private static IReadOnlySet<string> Ids<T>(IEnumerable<T> values, Func<T, string> selector) => new HashSet<string>(values.Select(selector), StringComparer.Ordinal);
}
