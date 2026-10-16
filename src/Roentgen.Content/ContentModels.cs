namespace Roentgen.Content;

public sealed class LaunchContentPack
{
    public int SchemaVersion { get; init; }
    public string PackageId { get; init; } = string.Empty;
    public string ContentVersion { get; init; } = string.Empty;
    public List<RegionContentDefinition> Regions { get; init; } = [];
    public List<SettlementGrammarContentDefinition> SettlementGrammars { get; init; } = [];
    public List<StructureGrammarContentDefinition> StructureGrammars { get; init; } = [];
    public List<RoomMotifContentDefinition> RoomMotifs { get; init; } = [];
    public List<DamageProfileContentDefinition> DamageProfiles { get; init; } = [];
    public List<FactionContentDefinition> Factions { get; init; } = [];
    public List<OrganizationContentDefinition> Organizations { get; init; } = [];
    public List<HumanoidArchetypeContentDefinition> HumanoidArchetypes { get; init; } = [];
    public List<CreatureFamilyContentDefinition> CreatureFamilies { get; init; } = [];
    public List<RobotChassisContentDefinition> RobotChassis { get; init; } = [];
    public List<CompanionArchetypeContentDefinition> CompanionArchetypes { get; init; } = [];
    public List<WeaponFamilyContentDefinition> WeaponFamilies { get; init; } = [];
    public List<WeaponComponentContentDefinition> WeaponComponents { get; init; } = [];
    public List<ArmorFamilyContentDefinition> ArmorFamilies { get; init; } = [];
    public List<ArmorComponentContentDefinition> ArmorComponents { get; init; } = [];
    public List<ConsumableContentDefinition> Consumables { get; init; } = [];
    public List<CraftingProcessContentDefinition> CraftingProcesses { get; init; } = [];
    public List<PerkContentDefinition> Perks { get; init; } = [];
    public List<BackgroundContentDefinition> Backgrounds { get; init; } = [];
    public List<TraitContentDefinition> Traits { get; init; } = [];
    public List<QuestKernelContentDefinition> QuestKernels { get; init; } = [];
    public List<CampaignKernelContentDefinition> CampaignKernels { get; init; } = [];
    public List<DialogueActContentDefinition> DialogueActs { get; init; } = [];
    public List<RumorTemplateContentDefinition> RumorTemplates { get; init; } = [];
    public List<TutorialContentDefinition> Tutorials { get; init; } = [];
    public List<EpilogueRuleContentDefinition> EpilogueRules { get; init; } = [];
    public List<AudioProfileContentDefinition> AudioProfiles { get; init; } = [];
    public List<MusicBindingContentDefinition> MusicBindings { get; init; } = [];
}

public sealed class RegionContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Identity { get; init; } = string.Empty;
    public List<string> TerrainTags { get; init; } = [];
    public List<string> Hazards { get; init; } = [];
    public string StrategicResource { get; init; } = string.Empty;
    public List<string> LandmarkFamilies { get; init; } = [];
    public string AudioProfileId { get; init; } = string.Empty;
    public string MusicProfileId { get; init; } = string.Empty;
}

public sealed class SettlementGrammarContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FoundingPurpose { get; init; } = string.Empty;
    public string EconomicRole { get; init; } = string.Empty;
    public string DefenseDoctrine { get; init; } = string.Empty;
    public List<string> DistrictTags { get; init; } = [];
    public List<string> ServiceTags { get; init; } = [];
    public int PopulationCapacity { get; init; }
    public List<string> CompatibleRegions { get; init; } = [];
    public List<string> StructureGrammarIds { get; init; } = [];
}

public sealed class StructureGrammarContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public List<string> PurposeTags { get; init; } = [];
    public List<string> CompatibleRegions { get; init; } = [];
    public List<string> RoomMotifIds { get; init; } = [];
    public List<string> DamageProfileIds { get; init; } = [];
    public int MinimumFloors { get; init; }
    public int MaximumFloors { get; init; }
    public List<string> QuestReservationTags { get; init; } = [];
    public string AudioProfileId { get; init; } = string.Empty;
}

public sealed class RoomMotifContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Circulation { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public int MinimumRooms { get; init; }
    public int MaximumRooms { get; init; }
    public bool RequiresAlternateRoute { get; init; }
    public List<string> Tags { get; init; } = [];
}

public sealed class DamageProfileContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SeverityPermille { get; init; }
    public List<string> RepairStages { get; init; } = [];
    public List<string> Tags { get; init; } = [];
}

public sealed class FactionContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FoundingEvent { get; init; } = string.Empty;
    public string Ideology { get; init; } = string.Empty;
    public List<string> MaterialNeeds { get; init; } = [];
    public List<string> Taboos { get; init; } = [];
    public string Leadership { get; init; } = string.Empty;
    public List<string> InternalDivisions { get; init; } = [];
    public List<string> EquipmentTags { get; init; } = [];
    public List<string> SettlementTags { get; init; } = [];
    public List<string> SpeechTags { get; init; } = [];
    public List<string> Objectives { get; init; } = [];
    public string MusicProfileId { get; init; } = string.Empty;
}

public sealed class OrganizationContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AuthorityKind { get; init; } = string.Empty;
    public List<string> Services { get; init; } = [];
    public List<string> CompatibleFactions { get; init; } = [];
    public List<string> CompatibleRegions { get; init; } = [];
}

public sealed class HumanoidArchetypeContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CombatRole { get; init; } = string.Empty;
    public string PreferredRange { get; init; } = string.Empty;
    public string MoraleProfile { get; init; } = string.Empty;
    public int SurrenderThresholdPermille { get; init; }
    public List<string> Skills { get; init; } = [];
    public List<string> EquipmentTags { get; init; } = [];
    public string TargetPartProfileId { get; init; } = string.Empty;
    public string AudioProfileId { get; init; } = string.Empty;
    public List<string> CompatibleFactions { get; init; } = [];
}

public sealed class CreatureFamilyContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string BodyPlan { get; init; } = string.Empty;
    public string CombatRole { get; init; } = string.Empty;
    public List<string> HabitatTags { get; init; } = [];
    public List<string> SenseTags { get; init; } = [];
    public List<string> TargetParts { get; init; } = [];
    public List<string> InjuryConsequences { get; init; } = [];
    public List<string> LootTags { get; init; } = [];
    public string AudioProfileId { get; init; } = string.Empty;
    public string MusicThreatTag { get; init; } = string.Empty;
}

public sealed class RobotChassisContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Mobility { get; init; } = string.Empty;
    public string CombatRole { get; init; } = string.Empty;
    public List<string> TargetParts { get; init; } = [];
    public List<string> ComponentConsequences { get; init; } = [];
    public string AudioProfileId { get; init; } = string.Empty;
}

public sealed class CompanionArchetypeContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PrimaryCapability { get; init; } = string.Empty;
    public string CombatPackage { get; init; } = string.Empty;
    public List<string> IdeologicalThresholds { get; init; } = [];
    public string PersonalQuestKernelId { get; init; } = string.Empty;
    public string LoyaltyCrisis { get; init; } = string.Empty;
    public string DepartureCondition { get; init; } = string.Empty;
    public string EndingRuleId { get; init; } = string.Empty;
    public string AudioProfileId { get; init; } = string.Empty;
    public string MusicProfileId { get; init; } = string.Empty;
}

public sealed class WeaponFamilyContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Skill { get; init; } = string.Empty;
    public string AmmunitionTag { get; init; } = string.Empty;
    public List<string> ComponentSlots { get; init; } = [];
    public List<string> BuildDirections { get; init; } = [];
    public List<string> FocusActions { get; init; } = [];
    public string AudioProfileId { get; init; } = string.Empty;
    public string AssetRecipeId { get; init; } = string.Empty;
}

public sealed class WeaponComponentContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slot { get; init; } = string.Empty;
    public List<string> CompatibleWeaponFamilies { get; init; } = [];
    public List<string> Effects { get; init; } = [];
    public string AssetVariant { get; init; } = string.Empty;
}

public sealed class ArmorFamilyContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Specialization { get; init; } = string.Empty;
    public List<string> Layers { get; init; } = [];
    public List<string> CoverageTags { get; init; } = [];
    public List<string> SocialTags { get; init; } = [];
    public string AudioProfileId { get; init; } = string.Empty;
    public string AssetRecipeId { get; init; } = string.Empty;
}

public sealed class ArmorComponentContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slot { get; init; } = string.Empty;
    public List<string> CompatibleArmorFamilies { get; init; } = [];
    public List<string> Effects { get; init; } = [];
}

public sealed class ConsumableContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string UseKind { get; init; } = string.Empty;
    public int Magnitude { get; init; }
    public int DurationTicks { get; init; }
    public List<string> EffectTags { get; init; } = [];
    public List<string> RequirementTags { get; init; } = [];
}

public sealed class CraftingProcessContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Station { get; init; } = string.Empty;
    public string PrimarySkill { get; init; } = string.Empty;
    public int Difficulty { get; init; }
    public List<string> InputTags { get; init; } = [];
    public List<string> OutputTags { get; init; } = [];
    public string FailurePolicy { get; init; } = string.Empty;
}

public sealed class PerkContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Family { get; init; } = string.Empty;
    public int MaximumRank { get; init; }
    public bool Behavioral { get; init; }
    public List<string> Requirements { get; init; } = [];
    public string EffectType { get; init; } = string.Empty;
    public string EffectTarget { get; init; } = string.Empty;
    public int EffectValue { get; init; }
    public List<string> Tags { get; init; } = [];
}

public sealed class BackgroundContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Attribute { get; init; } = string.Empty;
    public string PrimarySkill { get; init; } = string.Empty;
    public string SecondarySkill { get; init; } = string.Empty;
    public string EquipmentPackageId { get; init; } = string.Empty;
    public List<string> KnowledgeTags { get; init; } = [];
    public List<string> DialogueTags { get; init; } = [];
    public string StartingBenefitId { get; init; } = string.Empty;
}

public sealed class TraitContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string BenefitType { get; init; } = string.Empty;
    public string BenefitTarget { get; init; } = string.Empty;
    public int BenefitValue { get; init; }
    public string DrawbackType { get; init; } = string.Empty;
    public string DrawbackTarget { get; init; } = string.Empty;
    public int DrawbackValue { get; init; }
    public List<string> IncompatibleTraitIds { get; init; } = [];
    public List<string> Tags { get; init; } = [];
}

public sealed class QuestKernelContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Form { get; init; } = string.Empty;
    public List<string> SolutionRoutes { get; init; } = [];
    public int NonCombatRouteCount { get; init; }
    public string HiddenFact { get; init; } = string.Empty;
    public List<string> RecoveryKinds { get; init; } = [];
    public List<string> ConsequenceTags { get; init; } = [];
    public List<string> FollowUpIds { get; init; } = [];
    public List<string> CompatibleRegions { get; init; } = [];
    public List<string> CompatibleFactions { get; init; } = [];
    public List<string> DialogueActTags { get; init; } = [];
}

public sealed class CampaignKernelContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public List<string> RequiredQuestCategories { get; init; } = [];
    public int CompatibleFactionCount { get; init; }
    public int RevelationCount { get; init; }
    public bool Irreversible { get; init; }
    public List<string> EndingTags { get; init; } = [];
    public string ReplacementPolicy { get; init; } = string.Empty;
}

public sealed class DialogueActContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Intent { get; init; } = string.Empty;
    public string Tone { get; init; } = string.Empty;
    public string Template { get; init; } = string.Empty;
    public List<string> RequiredFacts { get; init; } = [];
    public bool KnowledgeFilter { get; init; }
    public bool RelationshipAware { get; init; }
    public bool FactionTerminology { get; init; }
}

public sealed class RumorTemplateContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string BaseFactTag { get; init; } = string.Empty;
    public string Distortion { get; init; } = string.Empty;
    public string Template { get; init; } = string.Empty;
    public int MaximumTransmissionDepth { get; init; }
    public int ReliabilityLossPermille { get; init; }
}

public sealed class TutorialContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string TriggerFact { get; init; } = string.Empty;
    public string CompletionFact { get; init; } = string.Empty;
    public bool Replayable { get; init; }
    public bool Skippable { get; init; }
    public int Priority { get; init; }
}

public sealed class EpilogueRuleContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string SubjectKind { get; init; } = string.Empty;
    public string ConditionTag { get; init; } = string.Empty;
    public string Template { get; init; } = string.Empty;
}

public sealed class AudioProfileContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public List<string> SemanticEvents { get; init; } = [];
    public int VariationCount { get; init; }
    public bool Spatial { get; init; }
    public string QualityBudget { get; init; } = string.Empty;
}

public sealed class MusicBindingContentDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string SubjectId { get; init; } = string.Empty;
    public string StyleId { get; init; } = string.Empty;
    public List<string> States { get; init; } = [];
}
