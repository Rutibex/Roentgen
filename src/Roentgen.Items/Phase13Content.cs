using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record Phase13ContentBundle(
    ItemDefinitionCatalog Items,
    IReadOnlyDictionary<string, WeaponFamilyDefinition> WeaponFamilies,
    IReadOnlyDictionary<string, WeaponComponentDefinition> WeaponComponents,
    IReadOnlyDictionary<string, AmmunitionDefinition> Ammunition,
    IReadOnlyDictionary<string, ArmorItemDefinition> Armor,
    IReadOnlyDictionary<string, ConsumableDefinition> Consumables,
    IReadOnlyDictionary<string, CraftingRecipeDefinition> Recipes,
    IReadOnlyDictionary<string, LootProfile> LootProfiles,
    IReadOnlyDictionary<string, MerchantProfile> Merchants);

public static class Phase13Content
{
    public static Phase13ContentBundle Create()
    {
        ItemDefinitionCatalog items = CreateItemDefinitions();
        Dictionary<string, WeaponFamilyDefinition> families = CreateWeaponFamilies().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        Dictionary<string, WeaponComponentDefinition> components = CreateWeaponComponents().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        Dictionary<string, AmmunitionDefinition> ammunition = CreateAmmunition().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        Dictionary<string, ArmorItemDefinition> armor = CreateArmor().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        Dictionary<string, ConsumableDefinition> consumables = CreateConsumables().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        Dictionary<string, CraftingRecipeDefinition> recipes = CreateRecipes().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        Dictionary<string, LootProfile> loot = CreateLootProfiles().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        Dictionary<string, MerchantProfile> merchants = CreateMerchants().ToDictionary(static value => value.Id, StringComparer.Ordinal);
        return new(items, families, components, ammunition, armor, consumables, recipes, loot, merchants);
    }

    public static IReadOnlyList<SettlementEconomyState> CreateEconomyFixtures()
    {
        var clinic = NewEconomy("prospect-clinic", 120, 40, 80);
        clinic.AddStock(new(EconomyCommodity.Medicine, 80000, 100000, 25)); clinic.AddStock(new(EconomyCommodity.Water, 120000, 100000, 4)); clinic.AddStock(new(EconomyCommodity.Ammunition, 40000, 90000, 8)); clinic.AddProduction(new(EconomyCommodity.Medicine, 5000)); clinic.AddConsumption(new(EconomyCommodity.Water, 3500)); clinic.AddPreferredGood("medicine"); clinic.AddProhibitedGood("biological-hazard");
        var quarry = NewEconomy("red-quarry-outpost", 80, 30, 160);
        quarry.AddStock(new(EconomyCommodity.Metals, 240000, 150000, 3)); quarry.AddStock(new(EconomyCommodity.Fuel, 50000, 100000, 12)); quarry.AddStock(new(EconomyCommodity.Weapons, 30000, 80000, 60)); quarry.AddProduction(new(EconomyCommodity.Metals, 12000)); quarry.AddConsumption(new(EconomyCommodity.Fuel, 7000)); quarry.AddPreferredGood("tool");
        var blackwater = NewEconomy("blackwater-exchange", 150, 55, 250);
        blackwater.AddStock(new(EconomyCommodity.Chemicals, 180000, 120000, 10)); blackwater.AddStock(new(EconomyCommodity.Food, 30000, 100000, 6)); blackwater.AddStock(new(EconomyCommodity.DiagnosticMaterials, 25000, 70000, 45)); blackwater.AddProduction(new(EconomyCommodity.Chemicals, 9000)); blackwater.AddConsumption(new(EconomyCommodity.Food, 6000)); blackwater.AddProhibitedGood("radiological-contraband");
        return [clinic, quarry, blackwater];
    }

    private static SettlementEconomyState NewEconomy(string id, int buyTax, int sellTax, int risk)
    {
        return new SettlementEconomyState(StableIdFactory.Create("settlement", id), new CurrencyPolicy(CurrencyPolicyKind.Mixed, "currency.diagnostic-scrip", 100), new TaxPolicy(buyTax, sellTax, 150), risk);
    }

    private static ItemDefinitionCatalog CreateItemDefinitions()
    {
        var catalog = new ItemDefinitionCatalog();
        void Add(string id, string name, ItemKind kind, int mass, int bulk, int value, StackPolicy stack, int max, params string[] tags) => catalog.Add(new ItemDefinition(id, name, kind, new HashSet<string>(tags, StringComparer.Ordinal), mass, bulk, value, stack, max, $"item/{id}", "material.generated"));
        Add("weapon.revolver", "Generated Revolver", ItemKind.Weapon, 1050, 900, 180, StackPolicy.Never, 1, "weapon", "weapon.revolver", "ballistic");
        Add("weapon.self-loading-pistol", "Generated Self-loading Pistol", ItemKind.Weapon, 980, 850, 210, StackPolicy.Never, 1, "weapon", "weapon.pistol", "ballistic");
        Add("weapon.scrap-pistol", "Generated Scrap Pistol", ItemKind.Weapon, 1250, 1000, 90, StackPolicy.Never, 1, "weapon", "weapon.scrap-pistol", "ballistic");
        Add("weapon.shotgun", "Generated Shotgun", ItemKind.Weapon, 3600, 3200, 340, StackPolicy.Never, 1, "weapon", "weapon.shotgun", "ballistic");
        Add("weapon.hunting-rifle", "Generated Hunting Rifle", ItemKind.Weapon, 4100, 3600, 390, StackPolicy.Never, 1, "weapon", "weapon.hunting-rifle", "ballistic");
        Add("weapon.improvised-melee", "Generated Improvised Melee Weapon", ItemKind.Weapon, 1900, 1800, 70, StackPolicy.Never, 1, "weapon", "weapon.melee", "melee");
        string[] componentIds = { "receiver.revolver", "receiver.pistol", "receiver.scrap", "receiver.shotgun", "receiver.rifle", "receiver.melee", "barrel.short", "barrel.standard", "barrel.long", "barrel.scatter", "grip.wood", "grip.rubber", "grip.wrapped", "stock.none", "stock.wood", "stock.folding", "feed.cylinder", "feed.box", "feed.tube", "feed.single", "sight.iron", "sight.aperture", "power.none", "mod.counterweight" };
        foreach (string id in componentIds) Add($"component.{id}", id.Replace('.', ' '), ItemKind.Component, 150, 100, 25, StackPolicy.Never, 1, "component", id.Split('.')[0]);
        Add("ammo.light-cartridge", "Light Cartridge", ItemKind.Ammunition, 12, 2, 2, StackPolicy.IdenticalDefinition, 250, "ammunition", "ammo.light");
        Add("ammo.heavy-cartridge", "Heavy Cartridge", ItemKind.Ammunition, 22, 3, 4, StackPolicy.IdenticalDefinition, 200, "ammunition", "ammo.heavy");
        Add("ammo.shot-shell", "Shot Shell", ItemKind.Ammunition, 38, 5, 5, StackPolicy.IdenticalDefinition, 100, "ammunition", "ammo.shell");
        Add("ammo.rifle-cartridge", "Rifle Cartridge", ItemKind.Ammunition, 28, 4, 5, StackPolicy.IdenticalDefinition, 150, "ammunition", "ammo.rifle");
        Add("ammo.energy-cell", "Energy Cell", ItemKind.Ammunition, 80, 10, 12, StackPolicy.IdenticalDefinition, 80, "ammunition", "ammo.energy");
        Add("ammo.radiographic-charge", "Radiographic Charge", ItemKind.Ammunition, 110, 12, 20, StackPolicy.IdenticalDefinition, 60, "ammunition", "ammo.radiographic");
        Add("ammo.chemical-canister", "Chemical Canister", ItemKind.Ammunition, 260, 30, 18, StackPolicy.IdenticalDefinition, 40, "ammunition", "ammo.chemical");
        Add("ammo.explosive-charge", "Explosive Charge", ItemKind.Ammunition, 450, 50, 30, StackPolicy.IdenticalDefinition, 20, "ammunition", "ammo.explosive");
        Add("armor.field-underlayer", "Field Underlayer", ItemKind.Armor, 900, 700, 80, StackPolicy.Never, 1, "armor", "armor.underlayer");
        Add("armor.field-clothing", "Reinforced Field Clothing", ItemKind.Armor, 1600, 1300, 130, StackPolicy.Never, 1, "armor", "armor.clothing");
        Add("armor.field-torso", "Field Torso Plate", ItemKind.Armor, 4200, 2800, 320, StackPolicy.Never, 1, "armor", "armor.torso");
        Add("armor.field-arm", "Field Arm Guard", ItemKind.Armor, 900, 650, 90, StackPolicy.Never, 1, "armor", "armor.arm");
        Add("armor.field-leg", "Field Leg Guard", ItemKind.Armor, 1200, 850, 110, StackPolicy.Never, 1, "armor", "armor.leg");
        Add("armor.field-head", "Field Diagnostic Helmet", ItemKind.Armor, 1400, 950, 180, StackPolicy.Never, 1, "armor", "armor.head");
        Add("armor.field-respirator", "Field Respirator", ItemKind.Armor, 650, 500, 140, StackPolicy.Never, 1, "armor", "armor.respirator");
        string[] consumables = { "consumable.trauma-dressing", "consumable.stimulant", "consumable.antitoxin", "consumable.burn-gel", "consumable.contamination-binder", "consumable.clean-water", "consumable.field-ration", "consumable.pain-suppressant", "consumable.coagulant", "consumable.electrolyte", "consumable.diagnostic-dye", "consumable.sedative" };
        foreach (string id in consumables) Add(id, id.Replace("consumable.", "").Replace('-', ' '), ItemKind.Consumable, 150, 80, 18, StackPolicy.FungibleBatch, 20, "consumable", id.Contains("water", StringComparison.Ordinal) || id.Contains("ration", StringComparison.Ordinal) ? "food" : "medicine");
        string[] materials = { "scrap-steel", "alloy-plate", "copper-wire", "ceramic-fragment", "polymer-sheet", "clean-cloth", "medical-alcohol", "active-reagent", "sealant", "lubricant", "electronic-parts", "diagnostic-film", "fuel-gel", "organic-fiber", "chemical-salt" };
        foreach (string material in materials) Add($"material.{material}", material.Replace('-', ' '), ItemKind.Material, 250, 80, 8, StackPolicy.IdenticalDefinition, 100, "material", material);
        Add("tool.field-kit", "Field Tool Kit", ItemKind.Tool, 1800, 1100, 150, StackPolicy.Never, 1, "tool", "engineering-tool");
        Add("tool.medical-kit", "Medical Tool Kit", ItemKind.Tool, 1100, 800, 170, StackPolicy.Never, 1, "tool", "medicine-tool");
        Add("container.field-pack", "Field Pack", ItemKind.Container, 1400, 900, 90, StackPolicy.Never, 1, "container", "backpack");
        Add("currency.diagnostic-scrip", "Diagnostic Scrip", ItemKind.Currency, 1, 0, 1, StackPolicy.IdenticalDefinition, 1000000, "currency");
        Add("document.clinic-repair-manual", "Clinic Repair Manual", ItemKind.Document, 300, 100, 45, StackPolicy.Never, 1, "document", "recipe-source");
        return catalog;
    }

    private static IReadOnlyList<WeaponFamilyDefinition> CreateWeaponFamilies()
    {
        HashSet<WeaponComponentSlot> pistolSlots = [WeaponComponentSlot.Receiver, WeaponComponentSlot.Barrel, WeaponComponentSlot.Grip, WeaponComponentSlot.FeedSystem, WeaponComponentSlot.Sight];
        HashSet<WeaponComponentSlot> longSlots = [WeaponComponentSlot.Receiver, WeaponComponentSlot.Barrel, WeaponComponentSlot.Grip, WeaponComponentSlot.Stock, WeaponComponentSlot.FeedSystem, WeaponComponentSlot.Sight];
        return [
            Family("weapon.revolver", "Revolver", "Revolver", "Hitscan", 24, 6, 30000, 85, 120, 900, 16, 70, 1050, pistolSlots, "ammo.light"),
            Family("weapon.self-loading-pistol", "Self-loading Pistol", "SelfLoadingPistol", "Hitscan", 20, 10, 28000, 95, 100, 860, 10, 80, 980, pistolSlots, "ammo.light"),
            Family("weapon.scrap-pistol", "Scrap Pistol", "ScrapPistol", "Hitscan", 28, 1, 22000, 145, 170, 650, 24, 85, 1250, pistolSlots, "ammo.heavy"),
            Family("weapon.shotgun", "Shotgun", "Shotgun", "Hitscan", 12, 6, 16000, 240, 220, 820, 28, 105, 3600, longSlots, "ammo.shell"),
            Family("weapon.hunting-rifle", "Hunting Rifle", "HuntingRifle", "Hitscan", 42, 5, 65000, 55, 180, 900, 32, 115, 4100, longSlots, "ammo.rifle"),
            Family("weapon.improvised-melee", "Improvised Melee", "ImprovisedMelee", "Melee", 35, 0, 1800, 0, 80, 950, 30, 45, 1900, new HashSet<WeaponComponentSlot> { WeaponComponentSlot.Receiver, WeaponComponentSlot.Grip }, "ammo.none")
        ];
    }
    private static WeaponFamilyDefinition Family(string id, string name, string combat, string attack, int damage, int cap, int range, int spread, int recoil, int reliability, int attackTicks, int reloadTicks, int mass, IReadOnlySet<WeaponComponentSlot> slots, string ammo) => new(id, name, combat, attack, [new ItemDamageComponent(attack == "Melee" ? ItemDamageKind.Blunt : ItemDamageKind.Kinetic, damage)], cap, range, spread, recoil, reliability, attackTicks, reloadTicks, mass, slots, new HashSet<string>(StringComparer.Ordinal) { ammo });

    private static IReadOnlyList<WeaponComponentDefinition> CreateWeaponComponents()
    {
        var allPistols = new HashSet<string>(new[] { "weapon.revolver", "weapon.self-loading-pistol", "weapon.scrap-pistol" }, StringComparer.Ordinal);
        var allLong = new HashSet<string>(new[] { "weapon.shotgun", "weapon.hunting-rifle" }, StringComparer.Ordinal);
        var allFirearms = new HashSet<string>(allPistols.Concat(allLong), StringComparer.Ordinal);
        var result = new List<WeaponComponentDefinition>();
        void Add(string id, WeaponComponentSlot slot, IReadOnlySet<string> families, WeaponStatModifiers m, string material, params string[] tags) => result.Add(new WeaponComponentDefinition($"component.{id}", id.Replace('.', ' '), slot, families, new HashSet<string>(tags, StringComparer.Ordinal), m, material, $"fragment/{id}", id, 25));
        Add("receiver.revolver", WeaponComponentSlot.Receiver, new HashSet<string>(StringComparer.Ordinal){"weapon.revolver"}, new(2,1000,0,1000,0,0,30,1000,1000,100), "steel", "receiver");
        Add("receiver.pistol", WeaponComponentSlot.Receiver, new HashSet<string>(StringComparer.Ordinal){"weapon.self-loading-pistol"}, new(0,1000,0,1000,-10,0,0,900,1000,40), "alloy", "receiver");
        Add("receiver.scrap", WeaponComponentSlot.Receiver, new HashSet<string>(StringComparer.Ordinal){"weapon.scrap-pistol"}, new(4,1050,0,950,30,40,-120,1150,1100,180), "scrap", "receiver");
        Add("receiver.shotgun", WeaponComponentSlot.Receiver, new HashSet<string>(StringComparer.Ordinal){"weapon.shotgun"}, new(0,1000,0,1000,0,20,0,1000,1000,250), "steel", "receiver");
        Add("receiver.rifle", WeaponComponentSlot.Receiver, new HashSet<string>(StringComparer.Ordinal){"weapon.hunting-rifle"}, new(3,1050,0,1050,-15,20,40,1050,1000,300), "steel", "receiver");
        Add("receiver.melee", WeaponComponentSlot.Receiver, new HashSet<string>(StringComparer.Ordinal){"weapon.improvised-melee"}, new(8,1100,0,1000,0,0,50,1000,1000,400), "scrap", "receiver");
        Add("barrel.short", WeaponComponentSlot.Barrel, allPistols, new(0,950,0,800,35,-30,0,900,1000,-120), "steel", "barrel");
        Add("barrel.standard", WeaponComponentSlot.Barrel, allFirearms, WeaponStatModifiers.Neutral, "steel", "barrel");
        Add("barrel.long", WeaponComponentSlot.Barrel, allFirearms, new(2,1050,0,1250,-30,25,0,1100,1000,250), "steel", "barrel");
        Add("barrel.scatter", WeaponComponentSlot.Barrel, new HashSet<string>(StringComparer.Ordinal){"weapon.shotgun"}, new(-2,900,0,850,100,-20,0,900,1000,-150), "steel", "barrel");
        Add("grip.wood", WeaponComponentSlot.Grip, new HashSet<string>(StringComparer.Ordinal), new(0,1000,0,1000,0,-20,10,1000,1000,60), "wood", "grip");
        Add("grip.rubber", WeaponComponentSlot.Grip, new HashSet<string>(StringComparer.Ordinal), new(0,1000,0,1000,-8,-35,0,1000,1000,80), "polymer", "grip");
        Add("grip.wrapped", WeaponComponentSlot.Grip, new HashSet<string>(StringComparer.Ordinal), new(0,1000,0,1000,8,-10,-20,1000,1000,30), "cloth", "grip");
        Add("stock.none", WeaponComponentSlot.Stock, allPistols, new(0,1000,0,1000,15,-10,0,900,1000,-200), "none", "stock");
        Add("stock.wood", WeaponComponentSlot.Stock, allLong, new(0,1000,0,1050,-20,-60,20,1050,1000,450), "wood", "stock");
        Add("stock.folding", WeaponComponentSlot.Stock, allLong, new(0,1000,0,1000,0,-30,-10,950,1000,180), "alloy", "stock");
        Add("feed.cylinder", WeaponComponentSlot.FeedSystem, new HashSet<string>(StringComparer.Ordinal){"weapon.revolver"}, new(0,1000,0,1000,0,0,60,1000,1100,180), "steel", "feed");
        Add("feed.box", WeaponComponentSlot.FeedSystem, new HashSet<string>(allFirearms.Where(static x => x != "weapon.revolver" && x != "weapon.shotgun"), StringComparer.Ordinal), new(0,1000,4,1000,0,0,-20,1000,850,220), "alloy", "feed");
        Add("feed.tube", WeaponComponentSlot.FeedSystem, new HashSet<string>(StringComparer.Ordinal){"weapon.shotgun"}, new(0,1000,2,1000,0,0,30,1000,1200,300), "steel", "feed");
        Add("feed.single", WeaponComponentSlot.FeedSystem, new HashSet<string>(StringComparer.Ordinal){"weapon.scrap-pistol","weapon.hunting-rifle"}, new(3,1080,-4,1000,0,0,100,1100,700,-100), "steel", "feed");
        Add("sight.iron", WeaponComponentSlot.Sight, allFirearms, WeaponStatModifiers.Neutral, "steel", "sight");
        Add("sight.aperture", WeaponComponentSlot.Sight, allFirearms, new(0,1000,0,1080,-25,0,0,1050,1000,70), "alloy", "sight");
        Add("power.none", WeaponComponentSlot.PowerSystem, new HashSet<string>(StringComparer.Ordinal), WeaponStatModifiers.Neutral, "none", "power");
        Add("mod.counterweight", WeaponComponentSlot.Modification, new HashSet<string>(StringComparer.Ordinal), new(0,1000,0,1000,0,-45,0,1100,1000,320), "steel", "modification");
        return result;
    }

    private static IReadOnlyList<AmmunitionDefinition> CreateAmmunition() => [
        Ammo("ammo.light-cartridge", AmmunitionKind.Cartridge, "ammo.light", ItemDamageKind.Kinetic, 0, 900, 1000, 1000), Ammo("ammo.heavy-cartridge", AmmunitionKind.Cartridge, "ammo.heavy", ItemDamageKind.Kinetic, 4, 1100, 950, 940),
        Ammo("ammo.shot-shell", AmmunitionKind.Shell, "ammo.shell", ItemDamageKind.Kinetic, 0, 800, 850, 1000), Ammo("ammo.rifle-cartridge", AmmunitionKind.Cartridge, "ammo.rifle", ItemDamageKind.Kinetic, 6, 1250, 1100, 980),
        Ammo("ammo.energy-cell", AmmunitionKind.EnergyCell, "ammo.energy", ItemDamageKind.Electrical, 8, 1000, 1000, 1000), Ammo("ammo.radiographic-charge", AmmunitionKind.RadiographicCharge, "ammo.radiographic", ItemDamageKind.Radiological, 10, 1150, 1000, 950),
        Ammo("ammo.chemical-canister", AmmunitionKind.ChemicalCanister, "ammo.chemical", ItemDamageKind.Chemical, 7, 700, 700, 900), Ammo("ammo.explosive-charge", AmmunitionKind.ExplosiveCharge, "ammo.explosive", ItemDamageKind.Blunt, 15, 900, 800, 850)
    ];
    private static AmmunitionDefinition Ammo(string id, AmmunitionKind kind, string tag, ItemDamageKind damage, int amount, int penetration, int velocity, int reliability) => new(id, id.Replace("ammo.", "").Replace('-', ' '), kind, new HashSet<string>(StringComparer.Ordinal){tag}, 20, [new ItemDamageComponent(damage, amount)], penetration, velocity, reliability, $"projectile/{id}", $"effect/{id}");

    private static IReadOnlyList<ArmorItemDefinition> CreateArmor() => [
        Armor("armor.field-underlayer", ItemArmorLayer.Underlayer, new[]{"torso","left-arm","right-arm","left-leg","right-leg"}, 1, 50, 0, 50, "plain"),
        Armor("armor.field-clothing", ItemArmorLayer.Clothing, new[]{"torso","left-arm","right-arm","left-leg","right-leg"}, 3, 90, 20, 90, "workwear"),
        Armor("armor.field-torso", ItemArmorLayer.PrimaryProtection, new[]{"torso"}, 10, 260, 80, 180, "armored"), Armor("armor.field-arm", ItemArmorLayer.PrimaryProtection, new[]{"left-arm","right-arm"}, 7, 210, 35, 140, "armored"),
        Armor("armor.field-leg", ItemArmorLayer.PrimaryProtection, new[]{"left-leg","right-leg"}, 8, 230, 45, 150, "armored"), Armor("armor.field-head", ItemArmorLayer.OuterPlate, new[]{"head"}, 9, 240, 30, 120, "diagnostic-helmet"),
        Armor("armor.field-respirator", ItemArmorLayer.OuterPlate, new[]{"head","respiratory"}, 2, 80, 10, 850, "sealed")
    ];
    private static ArmorItemDefinition Armor(string id, ItemArmorLayer layer, IEnumerable<string> regions, int threshold, int resistance, int mobility, int environment, string appearance) => new(id, layer, new HashSet<string>(regions, StringComparer.Ordinal), [new ArmorProtection(ItemDamageKind.Kinetic, threshold, resistance), new ArmorProtection(ItemDamageKind.Chemical, threshold / 2, resistance / 2)], mobility, mobility + 30, environment, new HashSet<string>(StringComparer.Ordinal){appearance}, new HashSet<string>(StringComparer.Ordinal){"armor.mod.standard"});

    private static IReadOnlyList<ConsumableDefinition> CreateConsumables() => [
        Consumable("consumable.trauma-dressing", "health.restore", 25), Consumable("consumable.stimulant", "stamina.restore", 35), Consumable("consumable.antitoxin", "status.poison.reduce", 50), Consumable("consumable.burn-gel", "status.burn.reduce", 50),
        Consumable("consumable.contamination-binder", "contamination.reduce", 35), Consumable("consumable.clean-water", "hydration.restore", 40), Consumable("consumable.field-ration", "nutrition.restore", 40), Consumable("consumable.pain-suppressant", "pain.reduce", 45),
        Consumable("consumable.coagulant", "status.bleeding.stabilize", 60), Consumable("consumable.electrolyte", "stamina.regeneration", 25), Consumable("consumable.diagnostic-dye", "diagnostic.knowledge.modify", 20), Consumable("consumable.sedative", "morale.stabilize", 35)
    ];
    private static ConsumableDefinition Consumable(string id, string effect, int magnitude) => new(id, ConsumableUseKind.Self, 45, 1, [new ItemEffectDefinition(effect, magnitude, effect.Contains("regeneration", StringComparison.Ordinal) ? 600 : 0)], new HashSet<string>(StringComparer.Ordinal));

    private static IReadOnlyList<CraftingRecipeDefinition> CreateRecipes()
    {
        var recipes = new List<CraftingRecipeDefinition>();
        recipes.Add(Recipe("repair-sidearm", CraftingOperationKind.Repair, CraftingStationKind.Workbench, SkillId.Engineering, 35, [Tag("material",2)], [Out("material.scrap-steel",1,false)]));
        recipes.Add(Recipe("assemble-light-cartridges", CraftingOperationKind.Assemble, CraftingStationKind.Workbench, SkillId.Engineering, 40, [Exact("material.scrap-steel",1),Exact("material.chemical-salt",1)], [Out("ammo.light-cartridge",12,false)]));
        recipes.Add(Recipe("assemble-rifle-cartridges", CraftingOperationKind.Assemble, CraftingStationKind.Workbench, SkillId.Engineering, 50, [Exact("material.scrap-steel",2),Exact("material.chemical-salt",1)], [Out("ammo.rifle-cartridge",10,false)]));
        recipes.Add(Recipe("synthesize-trauma-dressing", CraftingOperationKind.Synthesize, CraftingStationKind.MedicalStation, SkillId.Medicine, 30, [Exact("material.clean-cloth",1),Exact("material.medical-alcohol",1)], [Out("consumable.trauma-dressing",2,false)]));
        recipes.Add(Recipe("synthesize-antitoxin", CraftingOperationKind.Synthesize, CraftingStationKind.MedicalStation, SkillId.Medicine, 50, [Exact("material.active-reagent",2),Exact("material.medical-alcohol",1)], [Out("consumable.antitoxin",1,false)]));
        recipes.Add(Recipe("cook-field-ration", CraftingOperationKind.Cook, CraftingStationKind.CookingFire, SkillId.Fieldcraft, 25, [Exact("material.organic-fiber",2),Exact("material.chemical-salt",1)], [Out("consumable.field-ration",2,false)]));
        recipes.Add(Recipe("assemble-torso-plate", CraftingOperationKind.Assemble, CraftingStationKind.ArmorFrame, SkillId.Engineering, 55, [Exact("material.alloy-plate",3),Exact("material.sealant",1)], [Out("armor.field-torso",1,false)]));
        recipes.Add(Recipe("analyze-diagnostic-film", CraftingOperationKind.Analyze, CraftingStationKind.DiagnosticTable, SkillId.Science, 45, [Exact("material.diagnostic-film",1),Exact("material.active-reagent",1)], [Out("material.electronic-parts",1,false)]));
        recipes.Add(Recipe("refine-energy-cell", CraftingOperationKind.Synthesize, CraftingStationKind.EnergyBench, SkillId.Science, 50, [Exact("material.copper-wire",1),Exact("material.electronic-parts",1),Exact("material.chemical-salt",1)], [Out("ammo.energy-cell",2,false)]));
        recipes.Add(Recipe("mix-burn-gel", CraftingOperationKind.Synthesize, CraftingStationKind.ChemistryStation, SkillId.Medicine, 35, [Exact("material.active-reagent",1),Exact("material.sealant",1)], [Out("consumable.burn-gel",2,false)]));
        string[] extras = { "clean-water","coagulant","electrolyte","pain-suppressant","contamination-binder","diagnostic-dye","sedative","field-underlayer","field-arm","field-leg","field-head","field-respirator","shot-shells","heavy-cartridges","chemical-canister","explosive-charge","rubber-grip","wood-stock","aperture-sight","counterweight","scrap-pistol","hunting-rifle","disassemble-sidearm" };
        int index = 0; foreach (string extra in extras) { CraftingStationKind station = (index % 7) switch { 0=>CraftingStationKind.Workbench,1=>CraftingStationKind.ChemistryStation,2=>CraftingStationKind.MedicalStation,3=>CraftingStationKind.EnergyBench,4=>CraftingStationKind.CookingFire,5=>CraftingStationKind.ArmorFrame,_=>CraftingStationKind.DiagnosticTable }; SkillId skill = station switch { CraftingStationKind.MedicalStation=>SkillId.Medicine,CraftingStationKind.ChemistryStation=>SkillId.Science,CraftingStationKind.CookingFire=>SkillId.Fieldcraft,_=>SkillId.Engineering }; recipes.Add(Recipe($"phase13-{extra}", CraftingOperationKind.Assemble, station, skill, 30 + index % 5 * 5, [Tag("material",1)], [Out(index % 2 == 0 ? "material.scrap-steel" : "material.clean-cloth",1,false)])); index++; }
        return recipes.Take(30).ToArray();
    }
    private static CraftingRecipeDefinition Recipe(string id, CraftingOperationKind op, CraftingStationKind station, SkillId skill, int difficulty, IReadOnlyList<IngredientRequirement> inputs, IReadOnlyList<ItemOutputDefinition> outputs) => new(id, op, station, inputs, outputs, [], skill, difficulty, 180, "bounded-loss", "phase13");
    private static IngredientRequirement Exact(string id, int quantity) => new(id, null, quantity); private static IngredientRequirement Tag(string tag, int quantity) => new(null, tag, quantity); private static ItemOutputDefinition Out(string id, int quantity, bool blueprint) => new(id, quantity, blueprint);

    private static IReadOnlyList<LootProfile> CreateLootProfiles()
    {
        string[] ids = { "clinic-storage","industrial-ruin","checkpoint-locker","quarry-cache","blackwater-cabinet","field-camp","merchant-backroom","diagnostic-vault","raider-stash","maintenance-closet","roadside-wreck","medical-cart" };
        return ids.Select((id,index) => new LootProfile(id, [new LootSlotDefinition("materials", "material", null, 1, 4, 900, 300, 950), new LootSlotDefinition("medicine", "medicine", null, 1, 2, index % 3 == 0 ? 800 : 350, 500, 1000), new LootSlotDefinition("ammunition", "ammunition", null, 3, 18, index % 2 == 0 ? 700 : 400, 600, 1000)], new HashSet<string>(StringComparer.Ordinal){id}, new HashSet<string>(StringComparer.Ordinal), 120 + index * 25)).ToArray();
    }
    private static IReadOnlyList<MerchantProfile> CreateMerchants() => [
        new("merchant.general", MerchantArchetype.General, new HashSet<string>(StringComparer.Ordinal){"consumable","material","tool"}, 3, 2500, 1200),
        new("merchant.arms", MerchantArchetype.ArmsDealer, new HashSet<string>(StringComparer.Ordinal){"weapon","ammunition","armor","component"}, 5, 6000, 3000),
        new("merchant.medic", MerchantArchetype.Medic, new HashSet<string>(StringComparer.Ordinal){"medicine","consumable"}, 2, 2800, 1800),
        new("merchant.salvager", MerchantArchetype.Salvager, new HashSet<string>(StringComparer.Ordinal){"material","component","tool"}, 4, 3500, 2200)
    ];
}
