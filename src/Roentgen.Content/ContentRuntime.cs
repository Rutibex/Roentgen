namespace Roentgen.Content;

public sealed class Phase21ContentRuntime
{
    private readonly IReadOnlyDictionary<string, object> index;

    private Phase21ContentRuntime(LaunchContentPack pack, ContentValidationReport validation)
    {
        Pack = pack;
        Validation = validation;
        index = BuildIndex(pack);
    }

    public LaunchContentPack Pack { get; }
    public ContentValidationReport Validation { get; }
    public string Fingerprint => Validation.Fingerprint;

    public static Phase21ContentRuntime CreateDefault()
    {
        LaunchContentPack pack = ContentPackLoader.LoadDefault();
        ContentValidationReport validation = new ContentPackValidator().Validate(pack);
        validation.ThrowIfInvalid();
        return new Phase21ContentRuntime(pack, validation);
    }

    public static Phase21ContentRuntime Create(LaunchContentPack pack)
    {
        ContentValidationReport validation = new ContentPackValidator().Validate(pack);
        validation.ThrowIfInvalid();
        return new Phase21ContentRuntime(pack, validation);
    }

    public bool TryGet<T>(string id, out T? value) where T : class
    {
        if (index.TryGetValue(id, out object? candidate) && candidate is T typed)
        {
            value = typed;
            return true;
        }
        value = null;
        return false;
    }

    public T Get<T>(string id) where T : class => TryGet(id, out T? value) ? value : throw new KeyNotFoundException($"Phase 21 content ID '{id}' is not a {typeof(T).Name}.");

    public IReadOnlyDictionary<string, int> Counts => new SortedDictionary<string, int>(StringComparer.Ordinal)
    {
        ["regions"] = Pack.Regions.Count,
        ["settlement-grammars"] = Pack.SettlementGrammars.Count,
        ["structure-grammars"] = Pack.StructureGrammars.Count,
        ["room-motifs"] = Pack.RoomMotifs.Count,
        ["damage-profiles"] = Pack.DamageProfiles.Count,
        ["factions"] = Pack.Factions.Count,
        ["organizations"] = Pack.Organizations.Count,
        ["humanoid-archetypes"] = Pack.HumanoidArchetypes.Count,
        ["creature-families"] = Pack.CreatureFamilies.Count,
        ["robot-chassis"] = Pack.RobotChassis.Count,
        ["companion-archetypes"] = Pack.CompanionArchetypes.Count,
        ["weapon-families"] = Pack.WeaponFamilies.Count,
        ["weapon-components"] = Pack.WeaponComponents.Count,
        ["armor-families"] = Pack.ArmorFamilies.Count,
        ["armor-components"] = Pack.ArmorComponents.Count,
        ["consumables"] = Pack.Consumables.Count,
        ["crafting-processes"] = Pack.CraftingProcesses.Count,
        ["perks"] = Pack.Perks.Count,
        ["backgrounds"] = Pack.Backgrounds.Count,
        ["traits"] = Pack.Traits.Count,
        ["quest-kernels"] = Pack.QuestKernels.Count,
        ["campaign-kernels"] = Pack.CampaignKernels.Count,
        ["dialogue-acts"] = Pack.DialogueActs.Count,
        ["rumor-templates"] = Pack.RumorTemplates.Count,
        ["tutorials"] = Pack.Tutorials.Count,
        ["epilogue-rules"] = Pack.EpilogueRules.Count,
        ["audio-profiles"] = Pack.AudioProfiles.Count,
        ["music-bindings"] = Pack.MusicBindings.Count
    };

    private static IReadOnlyDictionary<string, object> BuildIndex(LaunchContentPack pack)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        Add(pack.Regions); Add(pack.SettlementGrammars); Add(pack.StructureGrammars); Add(pack.RoomMotifs); Add(pack.DamageProfiles);
        Add(pack.Factions); Add(pack.Organizations); Add(pack.HumanoidArchetypes); Add(pack.CreatureFamilies); Add(pack.RobotChassis); Add(pack.CompanionArchetypes);
        Add(pack.WeaponFamilies); Add(pack.WeaponComponents); Add(pack.ArmorFamilies); Add(pack.ArmorComponents); Add(pack.Consumables); Add(pack.CraftingProcesses);
        Add(pack.Perks); Add(pack.Backgrounds); Add(pack.Traits); Add(pack.QuestKernels); Add(pack.CampaignKernels); Add(pack.DialogueActs); Add(pack.RumorTemplates);
        Add(pack.Tutorials); Add(pack.EpilogueRules); Add(pack.AudioProfiles); Add(pack.MusicBindings);
        return result;

        void Add<T>(IEnumerable<T> values) where T : class
        {
            foreach (T value in values)
            {
                string id = (string)(typeof(T).GetProperty("Id")?.GetValue(value) ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property."));
                if (!result.TryAdd(id, value)) throw new InvalidDataException($"Duplicate global content ID '{id}'.");
            }
        }
    }
}
