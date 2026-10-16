using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Items;

Phase13ContentBundle content = Phase13Content.Create();
string command = args.FirstOrDefault() ?? "help";
switch (command)
{
    case "generate": Generate(args.Skip(1).FirstOrDefault() ?? "weapon.revolver", ParseCount(args, 2, 1)); break;
    case "inspect": Inspect(args.Skip(1).FirstOrDefault() ?? "weapon.revolver"); break;
    case "assemble": Assemble(args.Skip(1).FirstOrDefault() ?? "weapon.hunting-rifle"); break;
    case "loot": Loot(args.Skip(1).FirstOrDefault() ?? "clinic-storage", ParseCount(args, 2, 100)); break;
    case "inventory-soak": InventorySoak(ParseCount(args, 1, 10_000)); break;
    case "economy": Economy(ParseCount(args, 1, 3650)); break;
    case "arbitrage-audit": ArbitrageAudit(); break;
    case "catalog": Catalog(); break;
    default:
        Console.WriteLine("Roentgen ItemLab — Phase 13");
        Console.WriteLine("Commands: catalog, generate [family] [count], inspect [family], assemble [family], loot [profile] [count], inventory-soak [count], economy [days], arbitrage-audit");
        break;
}

void Catalog()
{
    Console.WriteLine($"Definitions: {content.Items.Definitions.Count}");
    Console.WriteLine($"Weapon families: {content.WeaponFamilies.Count}");
    Console.WriteLine($"Weapon components: {content.WeaponComponents.Count}");
    Console.WriteLine($"Ammunition: {content.Ammunition.Count}");
    Console.WriteLine($"Armor: {content.Armor.Count}");
    Console.WriteLine($"Consumables: {content.Consumables.Count}");
    Console.WriteLine($"Recipes: {content.Recipes.Count}");
    Console.WriteLine($"Loot profiles: {content.LootProfiles.Count}");
    Console.WriteLine($"Merchants: {content.Merchants.Count}");
}

void Generate(string familyId, int count)
{
    WeaponFamilyDefinition family = content.WeaponFamilies[familyId]; var generator = new ItemBlueprintGenerator(); var writer = new CanonicalWriter();
    for (int index = 0; index < count; index++) { GeneratedItemBlueprint blueprint = generator.GenerateWeapon(new CampaignSeed((ulong)index + 1), family, content.WeaponComponents.Values.ToArray()); blueprint.WriteCanonical(writer); if (count <= 20) Console.WriteLine($"{index,4} {blueprint.DisplayIdentity,-30} {blueprint.Fingerprint}"); }
    Console.WriteLine($"Generated {count} blueprints; aggregate {writer.ComputeChecksum()}");
}

void Inspect(string familyId)
{
    var world = new ItemWorldState(); var factory = new ItemFactory(content); StableId owner = StableIdFactory.Create("itemlab", "owner"); world.EnsureInventory(owner); StableId weaponId = StableIdFactory.Create("itemlab", familyId); ItemInstanceState weapon = factory.CreateGeneratedWeapon(world, weaponId, new CampaignSeed(42), familyId, ItemLocation.InInventory(owner)); WeaponCapabilitySnapshot capability = new WeaponAssemblyCompiler().Compile(weapon, content.WeaponFamilies[familyId], content.WeaponComponents, world);
    Console.WriteLine($"{weapon.Blueprint?.DisplayIdentity}"); Console.WriteLine($"ID {weapon.Id}"); Console.WriteLine($"damage {string.Join(", ", capability.Damage.Select(x => $"{x.Type}:{x.Amount}"))}"); Console.WriteLine($"capacity {capability.MagazineCapacity}; range {capability.MaximumRangeMillimeters}; spread {capability.SpreadPermille}; recoil {capability.RecoilPermille}; reliability {capability.ReliabilityPermille}");
    foreach ((WeaponComponentSlot slot, StableId componentId) in weapon.InstalledComponents) Console.WriteLine($"{slot,-12} {world.GetItem(componentId).DefinitionId}");
}

void Assemble(string familyId) => Inspect(familyId);

void Loot(string profileId, int count)
{
    LootProfile profile = content.LootProfiles[profileId]; var generator = new LootGenerator(); long total = 0; var writer = new CanonicalWriter();
    for (int index = 0; index < count; index++) { StableId container = StableIdFactory.Create("loot-container", index.ToString(System.Globalization.CultureInfo.InvariantCulture)); GeneratedLoot loot = generator.Generate(new CampaignSeed((ulong)index + 1), container, profile, content.Items); total += loot.TotalValueMinorUnits; writer.WriteChecksum(loot.Fingerprint); }
    Console.WriteLine($"Generated {count} containers; average value {(double)total / count:F2}; checksum {writer.ComputeChecksum()}");
}

void InventorySoak(int count)
{
    var world = new ItemWorldState(); StableId owner = StableIdFactory.Create("itemlab", "inventory"); world.EnsureInventory(owner, int.MaxValue, int.MaxValue); var factory = new ItemFactory(content);
    for (int index = 0; index < count; index++) { string definition = index % 5 == 0 ? "ammo.light-cartridge" : "material.scrap-steel"; StableId id = StableIdFactory.CreateChild(owner, "soak-item", index); ItemInstanceState item = factory.CreateSimple(id, definition, ItemLocation.InInventory(owner), 1); world.AddItem(item); world.GetInventory(owner).Add(id); }
    ItemValidationReport report = new ItemWorldValidator().Validate(world, content.Items); Console.WriteLine($"Items {count}; valid {report.IsValid}; checksum {world.ComputeChecksum()}"); foreach (ItemValidationIssue issue in report.Issues.Take(20)) Console.WriteLine($"{issue.Code}: {issue.Message}");
}

void Economy(int days)
{
    IReadOnlyList<SettlementEconomyState> economies = Phase13Content.CreateEconomyFixtures(); for (int day = 0; day < days; day++) foreach (SettlementEconomyState economy in economies) economy.AdvanceDay(); var writer = new CanonicalWriter(); foreach (SettlementEconomyState economy in economies) economy.WriteCanonical(writer); Console.WriteLine($"Advanced {economies.Count} economies for {days} days; checksum {writer.ComputeChecksum()}"); foreach (SettlementEconomyState economy in economies) Console.WriteLine($"{economy.SettlementId} day {economy.Day} stocks {string.Join(", ", economy.Stocks.Select(x => $"{x.Key}:{x.Value.QuantityMilliunits}"))}");
}

void ArbitrageAudit()
{
    SettlementEconomyState economy = Phase13Content.CreateEconomyFixtures()[0]; var item = new ItemInstanceState(StableIdFactory.Create("audit", "item"), "consumable.trauma-dressing", null, ItemLocation.InInventory(StableIdFactory.Create("audit", "owner")), 1); ItemDefinition definition = content.Items.Get(item.DefinitionId); var calculator = new EconomyPriceCalculator(); PriceBreakdown buy = calculator.Calculate(definition, item, economy, EconomyCommodity.Medicine, true, 1000, 1000); PriceBreakdown sell = calculator.Calculate(definition, item, economy, EconomyCommodity.Medicine, false, 1000, 1000); Console.WriteLine($"buy {buy.FinalPriceMinorUnits}; sell {sell.FinalPriceMinorUnits}; arbitrage {(sell.FinalPriceMinorUnits >= buy.FinalPriceMinorUnits ? "FAIL" : "PASS")}");
}

static int ParseCount(string[] values, int index, int fallback) => values.Length > index && int.TryParse(values[index], out int count) ? Math.Clamp(count, 1, 1_000_000) : fallback;
