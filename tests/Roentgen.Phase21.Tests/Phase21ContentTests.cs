using Xunit;
using Roentgen.Campaign;
using Roentgen.Music;
using Roentgen.Combat;
using Roentgen.Audio;
using Roentgen.Content;
using Roentgen.Factions;
using Roentgen.Items;
using Roentgen.Narrative;
using Roentgen.Rpg;
using Roentgen.World;

namespace Roentgen.Phase21.Tests;

public sealed class Phase21ContentTests
{
    [Fact]
    public void CanonicalPackMeetsEveryLaunchBudget()
    {
        Phase21ContentRuntime runtime = Phase21ContentRuntime.CreateDefault();
        Assert.True(runtime.Validation.IsValid, string.Join(Environment.NewLine, runtime.Validation.Issues.Select(static issue => issue.Message)));
        Assert.Equal(ContentBudgets.RegionFamilies, runtime.Pack.Regions.Count);
        Assert.Equal(ContentBudgets.SettlementGrammars, runtime.Pack.SettlementGrammars.Count);
        Assert.Equal(ContentBudgets.StructureGrammars, runtime.Pack.StructureGrammars.Count);
        Assert.Equal(ContentBudgets.RoomMotifs, runtime.Pack.RoomMotifs.Count);
        Assert.Equal(ContentBudgets.DamageProfiles, runtime.Pack.DamageProfiles.Count);
        Assert.Equal(ContentBudgets.MajorFactions, runtime.Pack.Factions.Count);
        Assert.Equal(ContentBudgets.MinorOrganizations, runtime.Pack.Organizations.Count);
        Assert.Equal(ContentBudgets.HumanoidArchetypes, runtime.Pack.HumanoidArchetypes.Count);
        Assert.Equal(ContentBudgets.CreatureFamilies, runtime.Pack.CreatureFamilies.Count);
        Assert.Equal(ContentBudgets.RobotChassis, runtime.Pack.RobotChassis.Count);
        Assert.Equal(ContentBudgets.CompanionArchetypes, runtime.Pack.CompanionArchetypes.Count);
        Assert.Equal(ContentBudgets.WeaponFamilies, runtime.Pack.WeaponFamilies.Count);
        Assert.Equal(ContentBudgets.WeaponComponents, runtime.Pack.WeaponComponents.Count);
        Assert.Equal(ContentBudgets.ArmorFamilies, runtime.Pack.ArmorFamilies.Count);
        Assert.Equal(ContentBudgets.ArmorComponents, runtime.Pack.ArmorComponents.Count);
        Assert.Equal(ContentBudgets.Consumables, runtime.Pack.Consumables.Count);
        Assert.Equal(ContentBudgets.CraftingProcesses, runtime.Pack.CraftingProcesses.Count);
        Assert.Equal(ContentBudgets.Perks, runtime.Pack.Perks.Count);
        Assert.Equal(ContentBudgets.Backgrounds, runtime.Pack.Backgrounds.Count);
        Assert.Equal(ContentBudgets.Traits, runtime.Pack.Traits.Count);
        Assert.Equal(ContentBudgets.QuestKernels, runtime.Pack.QuestKernels.Count);
        Assert.Equal(ContentBudgets.CampaignKernels, runtime.Pack.CampaignKernels.Count);
        Assert.Equal(ContentBudgets.DialogueActs, runtime.Pack.DialogueActs.Count);
        Assert.Equal(ContentBudgets.RumorTemplates, runtime.Pack.RumorTemplates.Count);
        Assert.Equal(ContentBudgets.Tutorials, runtime.Pack.Tutorials.Count);
        Assert.Equal(ContentBudgets.EpilogueRules, runtime.Pack.EpilogueRules.Count);
        Assert.Equal(ContentBudgets.AudioProfiles, runtime.Pack.AudioProfiles.Count);
        Assert.Equal(ContentBudgets.MusicBindings, runtime.Pack.MusicBindings.Count);
    }

    [Fact]
    public void ContentPackCompilerRoundTripsCanonicalPayload()
    {
        string directory = Path.Combine(Path.GetTempPath(), "roentgen-phase21-" + Guid.NewGuid().ToString("N"));
        try
        {
            string source = ContentPackLoader.ResolveDefaultPath();
            var compiler = new ContentPackCompiler();
            CompiledContentPackResult result = compiler.Compile(source, directory);
            LaunchContentPack restored = compiler.ReadBinary(result.BinaryPath);
            Assert.Equal(result.Fingerprint, ContentPackLoader.ComputeFingerprint(restored));
            Assert.Equal(ContentBudgets.QuestKernels, restored.QuestKernels.Count);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void GameplayAdaptersExposeLaunchCatalogs()
    {
        Assert.Equal(18, Phase21RpgContent.Backgrounds.Count);
        Assert.Equal(30, Phase21RpgContent.Traits.Count);
        Assert.Equal(110, Phase21RpgContent.Perks.Count);
        Assert.Equal(120, Phase21NarrativeContent.CreateLaunchKernels().Count);
        Assert.Equal(8, Phase21SocietyContent.Launch.FactionKernels.Count);
        Assert.Equal(20, Phase21SocietyContent.Launch.Organizations.Count);
        Assert.Equal(12, Phase21SocietyContent.Launch.SettlementProfiles.Count);
        Assert.Equal(30, Phase21CampaignContent.Launch.CampaignKernels.Count);
        Assert.Equal(14, Phase21ItemContent.Launch.WeaponFamilies.Count);
        Assert.Equal(12, Phase21ItemContent.Launch.ArmorFamilies.Count);
    }

    [Fact]
    public void LaunchWorldProfileIsEightKilometresAndRequiresAllRegions()
    {
        WorldGenerationProfile profile = WorldGenerationProfile.LaunchCampaign;
        profile.Validate();
        Assert.Equal(8192, profile.WorldWidthMeters);
        Assert.Equal(8192, profile.WorldDepthMeters);
        Assert.True(profile.RequiresAllRegionFamilies);
        Assert.Equal(6, Phase21WorldContent.Launch.Regions.Count);
        Assert.Equal(50, Phase21WorldContent.Launch.StructureGrammars.Count);
    }

    [Fact]
    public void LaunchRpgCatalogRetainsBehavioralPerkMajority()
    {
        int behavioral = Phase21RpgContent.Perks.Count(static perk => perk.BalanceClass is PerkBalanceClass.Behavior or PerkBalanceClass.OptionUnlock);
        Assert.True(behavioral * 100 >= Phase21RpgContent.Perks.Count * 70);
    }
    [Fact]
    public void AudioMusicAndTargetProfilesCoverEveryLaunchFamily()
    {
        Assert.Equal(ContentBudgets.AudioProfiles, Phase21AudioContent.Launch.Profiles.Count);
        Assert.NotEmpty(Phase21AudioContent.ForSemanticEvent("confirm"));
        Assert.Equal(ContentBudgets.MusicBindings, Phase21MusicContent.Launch.Bindings.Count);
        Assert.Equal(ContentBudgets.RegionFamilies, Phase21MusicContent.Launch.BindingsByCategory["region"].Count);
        Assert.Equal(ContentBudgets.CreatureFamilies, Phase21TargetProfileCatalog.CreatureProfileCount);
        Assert.Equal(ContentBudgets.RobotChassis, Phase21TargetProfileCatalog.RobotProfileCount);
        Phase21TargetProfile creature = Phase21TargetProfileCatalog.All.First(static value => value.Kind == "creature");
        Assert.Equal(creature.TargetPartKeys.Count, Phase21TargetProfileCatalog.Materialize(creature.Id, new Roentgen.Contracts.StableId(1, 2)).Count);
    }

    [Fact]
    public void TutorialCatalogIsBackedByTheCompletePhase21Pack()
    {
        Assert.Equal(ContentBudgets.Tutorials, Roentgen.UI.Phase20TutorialCatalog.All.Count);
        Assert.All(Roentgen.UI.Phase20TutorialCatalog.All, static tutorial => Assert.False(string.IsNullOrWhiteSpace(tutorial.Body)));
    }

}
