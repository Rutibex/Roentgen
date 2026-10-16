using System.Text.Json;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

string command = args.FirstOrDefault() ?? "help";
switch (command)
{
    case "attributes":
        PrintAttributes();
        break;
    case "skills":
        PrintSkills();
        break;
    case "level-curve":
        PrintLevelCurve();
        break;
    case "perk-graph":
    case "perk-reachability":
        PrintPerks();
        break;
    case "compare-builds":
    case "scenario":
        PrintBuildGate();
        break;
    case "progression-soak":
        RunSoak(ParseSeeds(args));
        break;
    case "export-report":
        ExportReport(args.Skip(1).FirstOrDefault() ?? "phase12-balance-report.json");
        break;
    default:
        Console.WriteLine("Roentgen BalanceLab — Phase 12");
        Console.WriteLine("Commands: attributes, skills, level-curve, perk-graph, perk-reachability, compare-builds, scenario, progression-soak [count], export-report [path]");
        break;
}

static void PrintAttributes()
{
    var state = CreateFixture();
    DerivedStatSnapshot stats = new DerivedStatCalculator().Calculate(state);
    foreach ((AttributeId id, int value) in state.Attributes.Values) Console.WriteLine($"{id,-12} {value}");
    foreach ((DerivedStatId id, int value) in stats.Values) Console.WriteLine($"{id,-40} {value}");
}

static void PrintSkills()
{
    var state = CreateFixture();
    foreach ((SkillId id, int value) in state.Skills.Values) Console.WriteLine($"{id,-20} {value,3} next-cost {SkillSet.GetPointCost(value)}");
}

static void PrintLevelCurve()
{
    long cumulative = 0;
    for (int level = 1; level < ProgressionRules.MaximumLevel; level++)
    {
        long needed = ExperienceSystem.ExperienceToNextLevel(level);
        cumulative += needed;
        Console.WriteLine($"{level,2} -> {level + 1,2}: {needed,6} cumulative {cumulative,8}");
    }
}

static void PrintPerks()
{
    PerkCatalog catalog = Phase12Content.CreatePerkCatalog();
    PerkValidationReport report = catalog.Validate();
    Console.WriteLine($"Perks: {catalog.All.Count}; behavior/option: {report.BehaviorOrOptionPercent}%; valid: {report.IsValid}");
    foreach (PerkDefinition perk in catalog.All) Console.WriteLine($"{perk.Family,-22} {perk.Id}");
    foreach (PerkValidationIssue issue in report.Issues) Console.WriteLine($"ERROR {issue.PerkId}: {issue.Message}");
}

static void PrintBuildGate()
{
    BuildViabilityReport report = new BuildViabilitySimulator().EvaluateSixBuildGate(Phase12BuildFixtures.SixBuilds, Phase12BuildFixtures.ProspectClinicRoutes);
    Console.WriteLine($"Six-build gate: {(report.Passed ? "PASS" : "FAIL")}");
    foreach (BuildRouteResult result in report.Results) Console.WriteLine($"{result.BuildId,-36} -> {result.RouteId,-24} margin {result.Margin,3} {(result.Completed ? "PASS" : "FAIL")}");
}

static void RunSoak(int count)
{
    var generator = new NpcProgressionGenerator();
    var checksumWriter = new CanonicalWriter();
    for (int index = 0; index < count; index++)
    {
        StableId id = StableIdFactory.Create("balance-soak", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        CharacterProgressionState state = generator.Generate(id, (NpcProgressionArchetype)(index % Enum.GetValues<NpcProgressionArchetype>().Length), 1 + index % 50, (ulong)index + 1);
        state.WriteCanonical(checksumWriter);
    }
    Console.WriteLine($"Generated {count} deterministic progression states; checksum {checksumWriter.ComputeChecksum()}");
}

static void ExportReport(string path)
{
    PerkCatalog catalog = Phase12Content.CreatePerkCatalog();
    PerkValidationReport perks = catalog.Validate();
    BuildViabilityReport builds = new BuildViabilitySimulator().EvaluateSixBuildGate(Phase12BuildFixtures.SixBuilds, Phase12BuildFixtures.ProspectClinicRoutes);
    var report = new { phase = 12, perks = new { count = catalog.All.Count, valid = perks.IsValid, behaviorPercent = perks.BehaviorOrOptionPercent }, sixBuildGate = builds.Passed, results = builds.Results };
    File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(Path.GetFullPath(path));
}

static int ParseSeeds(string[] arguments) => arguments.Length > 1 && int.TryParse(arguments[1], out int count) ? Math.Clamp(count, 1, 1_000_000) : 10_000;
static CharacterProgressionState CreateFixture()
{
    var attributes = new AttributeSet();
    attributes.TryAdjust(AttributeId.Intellect, 2); attributes.TryAdjust(AttributeId.Awareness, 2); attributes.TryAdjust(AttributeId.Reflex, 2);
    SkillSet skills = Phase12SkillDefinitions.CreateStartingSkills(attributes, [SkillId.Engineering, SkillId.Science, SkillId.Security]);
    var state = new CharacterProgressionState(new(12, 12), attributes, skills);
    state.ConfigureCreation([SkillId.Engineering, SkillId.Science, SkillId.Security], "relay-technician", ["methodical", "adaptable"], []);
    return state;
}
