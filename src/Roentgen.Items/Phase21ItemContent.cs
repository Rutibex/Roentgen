using Roentgen.Content;
using Roentgen.Contracts;

namespace Roentgen.Items;

public sealed record Phase21ItemContentSummary(
    IReadOnlyList<WeaponFamilyContentDefinition> WeaponFamilies,
    IReadOnlyList<WeaponComponentContentDefinition> WeaponComponents,
    IReadOnlyList<ArmorFamilyContentDefinition> ArmorFamilies,
    IReadOnlyList<ArmorComponentContentDefinition> ArmorComponents,
    IReadOnlyList<ConsumableContentDefinition> Consumables,
    IReadOnlyList<CraftingProcessContentDefinition> CraftingProcesses,
    string Fingerprint);

public static class Phase21ItemContent
{
    private static readonly Lazy<Phase13ContentBundle> RuntimeBundle = new(BuildRuntimeBundle, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<Phase21ItemContentSummary> SummaryValue = new(BuildSummary, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Phase13ContentBundle Create() => RuntimeBundle.Value;
    public static Phase21ItemContentSummary Launch => SummaryValue.Value;

    private static Phase21ItemContentSummary BuildSummary()
    {
        Phase21ContentRuntime runtime = Phase21ContentRuntime.CreateDefault();
        return new(
            runtime.Pack.WeaponFamilies,
            runtime.Pack.WeaponComponents,
            runtime.Pack.ArmorFamilies,
            runtime.Pack.ArmorComponents,
            runtime.Pack.Consumables,
            runtime.Pack.CraftingProcesses,
            runtime.Fingerprint);
    }

    private static Phase13ContentBundle BuildRuntimeBundle()
    {
        Phase13ContentBundle baseline = Phase13Content.Create();
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();

        var items = new ItemDefinitionCatalog();
        foreach (ItemDefinition definition in baseline.Items.Definitions) items.Add(definition);

        var families = baseline.WeaponFamilies.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        foreach (WeaponFamilyContentDefinition source in content.Pack.WeaponFamilies)
        {
            if (!families.ContainsKey(source.Id)) families.Add(source.Id, BuildWeaponFamily(source));
            AddItemIfMissing(items, new ItemDefinition(
                source.Id,
                source.Name,
                ItemKind.Weapon,
                new HashSet<string>(["weapon", source.Skill, source.AmmunitionTag], StringComparer.Ordinal),
                WeaponMass(source.Id),
                1800,
                300,
                StackPolicy.Never,
                1,
                source.AssetRecipeId,
                "material.generated",
                $"description.{source.Id}"));
        }

        var components = baseline.WeaponComponents.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        foreach (WeaponComponentContentDefinition source in content.Pack.WeaponComponents)
        {
            if (!components.ContainsKey(source.Id)) components.Add(source.Id, BuildWeaponComponent(source));
            AddItemIfMissing(items, new ItemDefinition(
                source.Id,
                source.Name,
                ItemKind.Component,
                new HashSet<string>(["component", source.Slot, .. source.Effects], StringComparer.Ordinal),
                180,
                100,
                30,
                StackPolicy.Never,
                1,
                $"fragment/{source.AssetVariant}",
                "material.generated"));
        }

        var ammunition = baseline.Ammunition.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        EnsureLaunchAmmunition(ammunition, items);

        var armor = baseline.Armor.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        foreach (ArmorFamilyContentDefinition family in content.Pack.ArmorFamilies)
        {
            string id = $"armor.{Slug(family.Id)}.torso";
            if (!armor.ContainsKey(id)) armor.Add(id, BuildArmor(family, id));
            AddItemIfMissing(items, new ItemDefinition(
                id,
                $"{family.Name} Torso",
                ItemKind.Armor,
                new HashSet<string>(["armor", family.Id, family.Specialization, .. family.SocialTags], StringComparer.Ordinal),
                3600,
                2500,
                280,
                StackPolicy.Never,
                1,
                family.AssetRecipeId,
                "material.generated"));
        }
        foreach (ArmorComponentContentDefinition component in content.Pack.ArmorComponents)
        {
            AddItemIfMissing(items, new ItemDefinition(
                component.Id,
                component.Name,
                ItemKind.Component,
                new HashSet<string>(["armor-component", component.Slot, .. component.Effects], StringComparer.Ordinal),
                220,
                100,
                32,
                StackPolicy.Never,
                1,
                $"armor-component/{component.Slot}",
                "material.generated"));
        }

        var consumables = baseline.Consumables.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        foreach (ConsumableContentDefinition source in content.Pack.Consumables)
        {
            if (!consumables.ContainsKey(source.Id))
            {
                consumables.Add(source.Id, new ConsumableDefinition(
                    source.Id,
                    ParseUseKind(source.UseKind),
                    45,
                    1,
                    [new ItemEffectDefinition(source.EffectTags.FirstOrDefault() ?? "status.modify", source.Magnitude, source.DurationTicks)],
                    new HashSet<string>(source.RequirementTags, StringComparer.Ordinal)));
            }
            AddItemIfMissing(items, new ItemDefinition(
                source.Id,
                source.Name,
                ItemKind.Consumable,
                new HashSet<string>(["consumable", .. source.EffectTags], StringComparer.Ordinal),
                150,
                80,
                18 + source.Magnitude,
                StackPolicy.FungibleBatch,
                20,
                $"item/{source.Id}",
                "material.generated"));
        }

        var recipes = baseline.Recipes.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        foreach (CraftingProcessContentDefinition source in content.Pack.CraftingProcesses)
        {
            string recipeId = $"phase21.{source.Id}";
            if (!recipes.ContainsKey(recipeId)) recipes.Add(recipeId, BuildRecipe(source, items));
        }

        var loot = baseline.LootProfiles.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        AddLaunchLootProfiles(loot);
        var merchants = baseline.Merchants.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
        AddLaunchMerchants(merchants);

        if (families.Count != ContentBudgets.WeaponFamilies)
            throw new InvalidOperationException($"Launch runtime requires {ContentBudgets.WeaponFamilies} weapon families; found {families.Count}.");
        if (components.Count != ContentBudgets.WeaponComponents + baseline.WeaponComponents.Count)
            throw new InvalidOperationException("Launch runtime weapon component merge is incomplete.");
        if (merchants.Count != 20) throw new InvalidOperationException($"Launch runtime requires 20 merchant profiles; found {merchants.Count}.");

        return new Phase13ContentBundle(items, families, components, ammunition, armor, consumables, recipes, loot, merchants);
    }

    private static WeaponFamilyDefinition BuildWeaponFamily(WeaponFamilyContentDefinition source)
    {
        string attack = source.Skill is "melee" or "unarmed" ? "Melee" : "Projectile";
        ItemDamageKind damage = source.Id.Contains("beam", StringComparison.Ordinal) ? ItemDamageKind.Thermal
            : source.Id.Contains("arc", StringComparison.Ordinal) ? ItemDamageKind.Electrical
            : source.Id.Contains("radiographic", StringComparison.Ordinal) ? ItemDamageKind.Radiological
            : source.Id.Contains("chemical", StringComparison.Ordinal) ? ItemDamageKind.Chemical
            : source.Id.Contains("melee", StringComparison.Ordinal) ? ItemDamageKind.Blunt
            : ItemDamageKind.Kinetic;
        int index = StableIndex(source.Id, 9);
        return new WeaponFamilyDefinition(
            source.Id,
            source.Name,
            source.Skill,
            attack,
            [new ItemDamageComponent(damage, 16 + index * 3)],
            attack == "Melee" ? 0 : 6 + index,
            attack == "Melee" ? 2400 : 18_000 + index * 9_000,
            70 + index * 10,
            80 + index * 15,
            760 + index * 20,
            24 + index * 2,
            70 + index * 4,
            WeaponMass(source.Id),
            Slots(source.ComponentSlots),
            new HashSet<string>([source.AmmunitionTag], StringComparer.Ordinal));
    }

    private static WeaponComponentDefinition BuildWeaponComponent(WeaponComponentContentDefinition source)
    {
        int index = StableIndex(source.Id, 7);
        var modifiers = new WeaponStatModifiers(
            index % 3,
            1000 + index * 5,
            source.Slot == "feed" ? index : 0,
            1000 + (index - 3) * 15,
            (index - 3) * 4,
            (index - 3) * 5,
            index * 8,
            1000,
            1000,
            20 + index * 25);
        return new WeaponComponentDefinition(
            source.Id,
            source.Name,
            ParseSlot(source.Slot),
            new HashSet<string>(source.CompatibleWeaponFamilies, StringComparer.Ordinal),
            new HashSet<string>([source.Slot, .. source.Effects], StringComparer.Ordinal),
            modifiers,
            "material.generated",
            $"fragment/{source.AssetVariant}",
            $"acoustic.{source.Slot}.{index}",
            25 + index * 6);
    }

    private static ArmorItemDefinition BuildArmor(ArmorFamilyContentDefinition family, string id)
    {
        int tier = 2 + StableIndex(family.Id, 8);
        return new ArmorItemDefinition(
            id,
            family.Id.Contains("powered", StringComparison.Ordinal) ? ItemArmorLayer.PoweredFrame : ItemArmorLayer.PrimaryProtection,
            new HashSet<string>(family.CoverageTags, StringComparer.Ordinal),
            [new ArmorProtection(ItemDamageKind.Kinetic, tier, 100 + tier * 20), new ArmorProtection(ItemDamageKind.Radiological, tier / 2, 70 + tier * 15)],
            tier * 12,
            tier * 18,
            100 + tier * 70,
            new HashSet<string>(family.SocialTags, StringComparer.Ordinal),
            new HashSet<string>(["armor.mod.standard", family.Id], StringComparer.Ordinal));
    }

    private static CraftingRecipeDefinition BuildRecipe(CraftingProcessContentDefinition source, ItemDefinitionCatalog items)
    {
        string output = source.Station.Contains("medical", StringComparison.Ordinal) ? "consumable.trauma-dressing"
            : source.Station.Contains("energy", StringComparison.Ordinal) ? "ammo.energy-cell"
            : "material.scrap-steel";
        if (!items.TryGet(output, out _)) output = items.Definitions.First().Id;
        return new CraftingRecipeDefinition(
            $"phase21.{source.Id}",
            CraftingOperationKind.Assemble,
            ParseStation(source.Station),
            [new IngredientRequirement(null, "material", 1)],
            [new ItemOutputDefinition(output, 1, false)],
            [],
            ParseSkill(source.PrimarySkill),
            source.Difficulty,
            120 + source.Difficulty * 2,
            source.FailurePolicy,
            "phase21-content");
    }

    private static void EnsureLaunchAmmunition(IDictionary<string, AmmunitionDefinition> ammunition, ItemDefinitionCatalog items)
    {
        (string Id, AmmunitionKind Kind, string Tag, ItemDamageKind Damage)[] values =
        [
            ("ammo.cartridge", AmmunitionKind.Cartridge, "ammo.cartridge", ItemDamageKind.Kinetic),
            ("ammo.energy", AmmunitionKind.EnergyCell, "ammo.energy", ItemDamageKind.Electrical),
            ("ammo.radiographic", AmmunitionKind.RadiographicCharge, "ammo.radiographic", ItemDamageKind.Radiological),
            ("ammo.chemical", AmmunitionKind.ChemicalCanister, "ammo.chemical", ItemDamageKind.Chemical),
            ("ammo.explosive", AmmunitionKind.ExplosiveCharge, "ammo.explosive", ItemDamageKind.Blunt)
        ];
        foreach ((string id, AmmunitionKind kind, string tag, ItemDamageKind damage) in values)
        {
            if (!ammunition.ContainsKey(id)) ammunition.Add(id, new AmmunitionDefinition(id, id.Replace('.', ' '), kind, new HashSet<string>([tag], StringComparer.Ordinal), 25, [new ItemDamageComponent(damage, 3)], 1000, 1000, 1000, $"projectile/{id}", $"effect/{id}"));
            AddItemIfMissing(items, new ItemDefinition(id, id.Replace('.', ' '), ItemKind.Ammunition, new HashSet<string>(["ammunition", tag], StringComparer.Ordinal), 20, 3, 4, StackPolicy.IdenticalDefinition, 200, $"item/{id}", "material.generated"));
        }
    }

    private static void AddLaunchLootProfiles(IDictionary<string, LootProfile> loot)
    {
        string[] ids = ["ossuary-cache", "meridian-freight", "quiet-range-locker", "quarry-machine-bay", "flood-platform-stores", "archive-repository", "mutation-nest", "robot-service-bay"];
        foreach ((string id, int index) in ids.Select((value, index) => (value, index)))
        {
            string key = $"phase21.{id}";
            if (!loot.ContainsKey(key)) loot.Add(key, new LootProfile(key, [new LootSlotDefinition("materials", "material", null, 1, 4, 900, 300, 1000), new LootSlotDefinition("special", index % 2 == 0 ? "component" : "consumable", null, 1, 2, 550, 600, 1100)], new HashSet<string>([id], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), 260 + index * 40));
        }
    }

    private static void AddLaunchMerchants(IDictionary<string, MerchantProfile> merchants)
    {
        string[] tags = ["food", "water", "medicine", "ammunition", "weapon", "armor", "component", "material", "tool", "diagnostic", "chemical", "energy", "creature", "robot", "document", "faction"];
        for (int index = 0; merchants.Count < 20; index++)
        {
            string id = $"merchant.phase21.{index + 1:00}";
            if (merchants.ContainsKey(id)) continue;
            merchants.Add(id, new MerchantProfile(id, (MerchantArchetype)(index % Enum.GetValues<MerchantArchetype>().Length), new HashSet<string>([tags[index % tags.Length], "phase21"], StringComparer.Ordinal), 2 + index % 5, 3200 + index * 350, 1600 + index * 210));
        }
    }

    private static IReadOnlySet<WeaponComponentSlot> Slots(IEnumerable<string> values) => new HashSet<WeaponComponentSlot>(values.Select(ParseSlot));
    private static WeaponComponentSlot ParseSlot(string value) => value switch { "receiver" => WeaponComponentSlot.Receiver, "barrel" => WeaponComponentSlot.Barrel, "grip" => WeaponComponentSlot.Grip, "stock" => WeaponComponentSlot.Stock, "feed" => WeaponComponentSlot.FeedSystem, "sight" => WeaponComponentSlot.Sight, "power" => WeaponComponentSlot.PowerSystem, _ => WeaponComponentSlot.Modification };
    private static ConsumableUseKind ParseUseKind(string value) => value switch { "other" => ConsumableUseKind.OtherActor, "item" => ConsumableUseKind.Item, "area" => ConsumableUseKind.Area, _ => ConsumableUseKind.Self };
    private static CraftingStationKind ParseStation(string value) => value switch { "chemistry-station" => CraftingStationKind.ChemistryStation, "medical-station" => CraftingStationKind.MedicalStation, "energy-bench" => CraftingStationKind.EnergyBench, "cooking-fire" => CraftingStationKind.CookingFire, "armor-frame" => CraftingStationKind.ArmorFrame, "diagnostic-table" => CraftingStationKind.DiagnosticTable, _ => CraftingStationKind.Workbench };
    private static SkillId ParseSkill(string value) => Enum.TryParse(value.Replace("-", string.Empty), true, out SkillId skill) ? skill : SkillId.Engineering;
    private static int WeaponMass(string id) => id.Contains("heavy", StringComparison.Ordinal) ? 8200 : id.Contains("melee", StringComparison.Ordinal) ? 2800 : id.Contains("rifle", StringComparison.Ordinal) || id.Contains("shotgun", StringComparison.Ordinal) ? 4100 : 1400;
    private static int StableIndex(string value, int modulus)
    {
        uint hash = 2166136261;
        foreach (char character in value) hash = (hash ^ character) * 16777619;
        return (int)(hash % (uint)modulus);
    }
    private static string Slug(string id) => id.Replace("armor-family.", string.Empty, StringComparison.Ordinal).Replace('.', '-');
    private static void AddItemIfMissing(ItemDefinitionCatalog catalog, ItemDefinition definition) { if (!catalog.TryGet(definition.Id, out _)) catalog.Add(definition); }
}
