using Playroma.Sound.Generators.Contracts;
using Playroma.Sound.Generators.Descriptions;
using Playroma.Sound.Generators.Generators;
using Playroma.Sound.Generators.Profiles;
using Playroma.Sound.Graph.Definitions;
using Roentgen.Audio;

namespace Roentgen.Audio.Playroma;

public static class IntegratedSoundCatalog
{
    public static IReadOnlyDictionary<string, SoundGraphDefinition> Build(ulong rootSeed, AudioQuality quality)
    {
        SoundQualityMode mode = quality == AudioQuality.RealtimeLow ? SoundQualityMode.RealtimeLow : SoundQualityMode.RealtimeHigh;
        var assets = new Dictionary<string, SoundGraphDefinition>(StringComparer.Ordinal);
        AddUi(assets, rootSeed, mode);
        AddFocus(assets, rootSeed, mode);
        AddWeapons(assets, rootSeed, mode);
        AddImpacts(assets, rootSeed, mode);
        AddFootsteps(assets, rootSeed, mode);
        AddCreatures(assets, rootSeed, mode);
        AddInteractions(assets, rootSeed, mode);
        AddMachines(assets, rootSeed, mode);
        return assets;
    }

    private static void AddUi(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        var generator = new InterfaceSoundGenerator();
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Confirm, Technology = 0.78f, Brightness = 0.62f }, C(seed, "ui.confirm", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Cancel, Technology = 0.72f, Brightness = 0.38f }, C(seed, "ui.cancel", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Error, Urgency = 0.9f, Technology = 0.8f }, C(seed, "ui.error", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Warning, Urgency = 0.78f, Importance = 0.82f }, C(seed, "ui.warning", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Hover, Technology = 0.82f, Brightness = 0.46f, Importance = 0.25f }, C(seed, "ui.navigate", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Select, Technology = 0.86f, Brightness = 0.72f, Importance = 0.45f }, C(seed, "ui.tab", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.ResourceGained, Technology = 0.68f, Brightness = 0.58f, Importance = 0.58f }, C(seed, "ui.transaction", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Success, Technology = 0.72f, Brightness = 0.54f, Importance = 0.62f }, C(seed, "ui.craft", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Message, NoteCount = 3, Technology = 0.75f }, C(seed, "ui.journal", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Achievement, NoteCount = 4, Importance = 0.85f }, C(seed, "ui.quest", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Achievement, NoteCount = 5, Importance = 0.95f, Friendliness = 0.2f }, C(seed, "ui.revelation", quality));
        Add(assets, generator, new InterfaceSoundDescription { Kind = InterfaceSoundKind.Warning, NoteCount = 2, Importance = 1f, Urgency = 0.55f }, C(seed, "ui.commitment", quality));
    }

    private static void AddFocus(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        var generator = new ScienceFictionGenerator();
        string[] ids = ["focus.enter", "focus.target", "focus.part", "focus.queue", "focus.insufficient", "focus.commit", "focus.exit"];
        for (int i = 0; i < ids.Length; i++)
        {
            var description = new ScienceFictionDescription
            {
                Intensity = 0.35f + (i * 0.07f),
                Brightness = i == 4 ? 0.28f : 0.72f,
                Duration = i is 0 or 6 ? 0.55f : 0.22f,
                Technology = 0.95f,
                Movement = 0.45f + ((i % 3) * 0.2f)
            };
            Add(assets, generator, description, C(seed, ids[i], quality, i));
        }
    }

    private static void AddWeapons(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        string[] families = ["service-sidearm", "industrial-rifle", "scrap-impact-tool"];
        for (int i = 0; i < families.Length; i++)
        {
            string family = families[i];
            float power = i == 0 ? 0.46f : i == 1 ? 0.72f : 0.58f;
            WeaponMechanism fireMechanism = i == 2 ? WeaponMechanism.MeleeStrike : WeaponMechanism.ProjectileFirearm;
            Add(assets, new WeaponGenerator(), new WeaponDescription { Mechanism = fireMechanism, Power = power, Size = 0.42f + (i * 0.17f), MechanicalComplexity = 0.55f, Technology = 0.32f, Distance = 0.02f, Tail = 0.08f }, C(seed, $"weapon.{family}.fire", quality, i));
            Add(assets, new WeaponGenerator(), new WeaponDescription { Mechanism = WeaponMechanism.DryFire, Power = 0.15f, Size = 0.4f, MechanicalComplexity = 0.72f, Technology = 0.28f, Distance = 0.02f, Tail = 0.02f }, C(seed, $"weapon.{family}.dry", quality, i));
            Add(assets, new MaterialFoleyGenerator(), new MaterialFoleyDescription { MaterialA = SoundMaterial.Steel, MaterialB = SoundMaterial.Iron, Action = FoleyActionKind.Rattle, Intensity = 0.45f, Roughness = 0.8f, Duration = 0.25f }, C(seed, $"weapon.{family}.jam", quality, i));
            Add(assets, new WeaponGenerator(), new WeaponDescription { Mechanism = WeaponMechanism.Reload, Power = 0.28f, Size = 0.45f, MechanicalComplexity = 0.82f, Technology = 0.3f, Distance = 0.02f, Tail = 0.05f }, C(seed, $"weapon.{family}.reload", quality, i));
        }
        Add(assets, new ExplosionGenerator(), new ExplosionDescription { EnergyType = ExplosionEnergyType.Chemical, Environment = AcousticEnvironment.OpenField, Size = 0.68f, Containment = 0.25f, Distance = 0.02f, Debris = 0.72f, Sharpness = 0.68f, SecondaryEvents = 3 }, C(seed, "combat.fragmentation.explosion", quality));
        Add(assets, new WeaponGenerator(), new WeaponDescription { Mechanism = WeaponMechanism.MeleeSwing, Power = 0.4f, Size = 0.5f, MechanicalComplexity = 0.15f, Distance = 0.02f, Tail = 0.04f }, C(seed, "combat.melee.swing", quality));
        Add(assets, new ImpactGenerator(), new ImpactDescription { MaterialA = SoundMaterial.Steel, MaterialB = SoundMaterial.Flesh, Mass = 0.6f, Velocity = 0.62f, Distance = 0.02f }, C(seed, "combat.melee.contact", quality));
    }

    private static void AddImpacts(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        var map = new Dictionary<string, SoundMaterial>(StringComparer.Ordinal)
        {
            ["unknown"] = SoundMaterial.Concrete,
            ["fusedglass"] = SoundMaterial.Glass,
            ["asphalt"] = SoundMaterial.Concrete,
            ["industrialmud"] = SoundMaterial.Dirt,
            ["contaminatedsediment"] = SoundMaterial.Sand,
            ["palescrub"] = SoundMaterial.Wood,
            ["rock"] = SoundMaterial.Stone,
            ["concrete"] = SoundMaterial.Concrete,
            ["metal"] = SoundMaterial.Steel,
            ["wood"] = SoundMaterial.Wood,
            ["shallowwater"] = SoundMaterial.Water,
            ["armor"] = SoundMaterial.Steel,
            ["biological"] = SoundMaterial.Flesh,
            ["mechanical"] = SoundMaterial.Iron
        };
        int index = 0;
        foreach ((string name, SoundMaterial material) in map)
        {
            Add(assets, new ImpactGenerator(), new ImpactDescription { MaterialA = material, MaterialB = SoundMaterial.Steel, Environment = AcousticEnvironment.OpenField, Mass = 0.45f, Velocity = 0.62f, ContactArea = 0.3f, Breakage = material is SoundMaterial.Glass or SoundMaterial.Wood ? 0.65f : 0.15f, Distance = 0.02f }, C(seed, "impact." + name, quality, index++));
        }
    }

    private static void AddFootsteps(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        var surfaces = new Dictionary<string, SoundMaterial>(StringComparer.Ordinal)
        {
            ["unknown"] = SoundMaterial.Concrete, ["fusedglass"] = SoundMaterial.Glass, ["asphalt"] = SoundMaterial.Concrete,
            ["industrialmud"] = SoundMaterial.Dirt, ["contaminatedsediment"] = SoundMaterial.Sand, ["palescrub"] = SoundMaterial.Wood,
            ["rock"] = SoundMaterial.Stone, ["concrete"] = SoundMaterial.Concrete, ["metal"] = SoundMaterial.Steel,
            ["wood"] = SoundMaterial.Wood, ["shallowwater"] = SoundMaterial.Water
        };
        AudioGait[] gaits = [AudioGait.Sneak, AudioGait.Walk, AudioGait.Run, AudioGait.Sprint, AudioGait.Land];
        int index = 0;
        foreach ((string name, SoundMaterial material) in surfaces)
        {
            foreach (AudioGait gait in gaits)
            {
                FootstepGait playromaGait = gait switch { AudioGait.Sneak => FootstepGait.Sneak, AudioGait.Run => FootstepGait.Run, AudioGait.Sprint => FootstepGait.Sprint, AudioGait.Land => FootstepGait.Land, _ => FootstepGait.Walk };
                Add(assets, new FootstepGenerator(), new FootstepDescription { Footwear = FootwearType.Boot, Surface = material, Environment = AcousticEnvironment.OpenField, Gait = playromaGait, Weight = 0.62f, Speed = gait is AudioGait.Run or AudioGait.Sprint ? 0.8f : 0.45f, Wetness = material == SoundMaterial.Water ? 0.9f : 0.05f, LooseDebris = material is SoundMaterial.Dirt or SoundMaterial.Sand ? 0.72f : 0.12f, LeftFoot = (index & 1) == 0 }, C(seed, $"footstep.{name}.{gait.ToString().ToLowerInvariant()}", quality, index++));
            }
        }
    }

    private static void AddCreatures(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        Add(assets, new CreatureVoiceGenerator(), new CreatureVoiceDescription { Call = CreatureCallKind.Pain, Size = 0.72f, Aggression = 0.55f, Pain = 0.8f, Duration = 0.45f }, C(seed, "creature.pain", quality));
        Add(assets, new CreatureVoiceGenerator(), new CreatureVoiceDescription { Call = CreatureCallKind.Fear, Size = 0.72f, Fear = 0.85f, Pain = 0.55f, Duration = 0.75f }, C(seed, "creature.death", quality));
    }

    private static void AddInteractions(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        string[] ids = ["interaction.door.open", "interaction.door.close", "interaction.container.open", "interaction.container.close", "interaction.pickup", "interaction.craft", "interaction.repair"];
        for (int i = 0; i < ids.Length; i++)
        {
            SoundMaterial first = i < 4 ? SoundMaterial.Steel : i == 4 ? SoundMaterial.Brass : SoundMaterial.Iron;
            Add(assets, new MaterialFoleyGenerator(), new MaterialFoleyDescription
            {
                MaterialA = first,
                MaterialB = SoundMaterial.Steel,
                Action = i is 0 or 2 ? FoleyActionKind.Open : i is 1 or 3 ? FoleyActionKind.Close : i == 4 ? FoleyActionKind.PickUp : FoleyActionKind.Clink,
                Intensity = 0.28f + (i * 0.04f),
                Roughness = 0.55f,
                Duration = i < 4 ? 0.55f : 0.32f
            }, C(seed, ids[i], quality, i));
        }

        Add(assets, new InterfaceSoundGenerator(), new InterfaceSoundDescription
        {
            Kind = InterfaceSoundKind.Message,
            Technology = 0.58f,
            Brightness = 0.34f,
            Friendliness = 0.28f,
            Importance = 0.44f,
            Duration = 0.34f,
            NoteCount = 2
        }, C(seed, "interaction.terminal", quality, 7));
    }

    private static void AddMachines(Dictionary<string, SoundGraphDefinition> assets, ulong seed, SoundQualityMode quality)
    {
        Add(assets, new MachineGenerator(), new MachineDescription { Kind = MachineKind.GeneratorStart, Intensity = 0.7f, Complexity = 0.65f, Duration = 0.85f, OrganicSynthetic = 0.35f, Speed = 0.55f, Load = 0.7f }, C(seed, "machine.generator.start", quality));
        Add(assets, new MachineGenerator(), new MachineDescription { Kind = MachineKind.EmergencyShutdown, Intensity = 0.35f, Complexity = 0.45f, Duration = 0.65f, OrganicSynthetic = 0.4f, Speed = 0.25f, Load = 0.35f }, C(seed, "machine.generator.stop", quality));
        Add(assets, new MachineGenerator(), new MachineDescription { Kind = MachineKind.CatastrophicBreakdown, Intensity = 0.88f, Complexity = 0.8f, Duration = 0.75f, OrganicSynthetic = 0.75f, Speed = 0.65f, Load = 0.95f, Malfunction = 0.95f }, C(seed, "machine.fault", quality));
    }

    private static SoundGenerationContext C(ulong root, string id, SoundQualityMode quality, int variation = 0) => new()
    {
        RootSeed = root,
        FamilySeed = StableText(id),
        VariationIndex = variation,
        QualityMode = quality,
        GraphId = id,
        DisplayName = id
    };

    private static void Add<T>(Dictionary<string, SoundGraphDefinition> assets, ISoundGenerator<T> generator, T description, SoundGenerationContext context)
    {
        SoundGraphDefinition graph = generator.Generate(description, context);
        assets.Add(graph.Id, graph);
    }

    private static ulong StableText(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in text) { hash ^= c; hash = unchecked(hash * 1099511628211UL); }
        return hash;
    }
}
