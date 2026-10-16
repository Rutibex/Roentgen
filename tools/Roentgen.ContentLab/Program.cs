using System.Text.Json;
using Roentgen.Content;

return Run(args);

static int Run(string[] args)
{
    try
    {
        string command = args.Length == 0 ? "audit" : args[0].ToLowerInvariant();
        string source = Option(args, "--source") ?? ContentPackLoader.ResolveDefaultPath();
        LaunchContentPack pack = ContentPackLoader.Load(source, ResolveSidecar(source));
        ContentValidationReport report = new ContentPackValidator().Validate(pack);

        return command switch
        {
            "audit" or "validate" => Audit(report),
            "coverage" => Coverage(Phase21ContentRuntime.Create(pack)),
            "list" => List(pack, Option(args, "--category") ?? "all"),
            "compile" => Compile(source, Option(args, "--output") ?? Path.Combine("artifacts", "content-v21")),
            "distribution" => Distribution(pack),
            "repetition" => Repetition(pack),
            "fingerprint" => Fingerprint(report),
            "help" or "--help" or "-h" => Help(),
            _ => Unknown(command)
        };
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"ContentLab failed: {exception.Message}");
        return 1;
    }
}

static int Audit(ContentValidationReport report)
{
    Console.WriteLine($"Phase 21 content fingerprint: {report.Fingerprint}");
    Console.WriteLine($"Valid: {report.IsValid}");
    foreach (ContentValidationIssue issue in report.Issues.OrderBy(static value => value.Code, StringComparer.Ordinal).ThenBy(static value => value.SubjectId, StringComparer.Ordinal))
        Console.WriteLine($"{(issue.IsCritical ? "ERROR" : "WARN ")} {issue.Code} [{issue.SubjectId}] {issue.Message}");
    return report.IsValid ? 0 : 2;
}

static int Coverage(Phase21ContentRuntime runtime)
{
    Console.WriteLine($"Package: {runtime.Pack.PackageId} {runtime.Pack.ContentVersion}");
    Console.WriteLine($"Fingerprint: {runtime.Fingerprint}");
    foreach ((string category, int count) in runtime.Counts)
        Console.WriteLine($"{category,-24} {count,5}");
    return 0;
}

static int List(LaunchContentPack pack, string category)
{
    IEnumerable<(string Category, string Id, string Name)> rows = Entries(pack);
    if (!string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        rows = rows.Where(value => string.Equals(value.Category, category, StringComparison.OrdinalIgnoreCase));
    foreach ((string group, string id, string name) in rows.OrderBy(static value => value.Category, StringComparer.Ordinal).ThenBy(static value => value.Id, StringComparer.Ordinal))
        Console.WriteLine($"{group,-22} {id,-48} {name}");
    return 0;
}

static int Compile(string source, string output)
{
    CompiledContentPackResult result = new ContentPackCompiler().Compile(source, output);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

static int Distribution(LaunchContentPack pack)
{
    PrintGroups("structure groups", pack.StructureGrammars.GroupBy(static value => value.Group, StringComparer.Ordinal));
    PrintGroups("creature body plans", pack.CreatureFamilies.GroupBy(static value => value.BodyPlan, StringComparer.Ordinal));
    PrintGroups("quest categories", pack.QuestKernels.GroupBy(static value => value.Category, StringComparer.Ordinal));
    PrintGroups("campaign roles", pack.CampaignKernels.GroupBy(static value => value.Role, StringComparer.Ordinal));
    PrintGroups("perk families", pack.Perks.GroupBy(static value => value.Family, StringComparer.Ordinal));
    return 0;
}

static int Repetition(LaunchContentPack pack)
{
    int duplicateNames = Entries(pack).GroupBy(static value => value.Name, StringComparer.OrdinalIgnoreCase).Count(static group => group.Count() > 1);
    int duplicateQuestPatterns = pack.QuestKernels.GroupBy(static value => string.Join('|', value.SolutionRoutes.Order(StringComparer.Ordinal)), StringComparer.Ordinal).Count(static group => group.Count() > 8);
    int dialogueTemplateOveruse = pack.DialogueActs.GroupBy(static value => value.Intent, StringComparer.Ordinal).Where(static group => group.Count() > 20).Count();
    Console.WriteLine($"duplicate display-name groups: {duplicateNames}");
    Console.WriteLine($"overused quest-route signatures: {duplicateQuestPatterns}");
    Console.WriteLine($"overused dialogue acts: {dialogueTemplateOveruse}");
    bool pass = duplicateQuestPatterns == 0 && dialogueTemplateOveruse == 0;
    Console.WriteLine($"repetition gate: {(pass ? "PASS" : "FAIL")}");
    return pass ? 0 : 3;
}

static int Fingerprint(ContentValidationReport report) { Console.WriteLine(report.Fingerprint); return report.IsValid ? 0 : 2; }
static int Help()
{
    Console.WriteLine("Roentgen.ContentLab commands: audit, coverage, list, compile, distribution, repetition, fingerprint");
    Console.WriteLine("Options: --source <phase21-launch-content.json> --output <directory> --category <name>");
    return 0;
}
static int Unknown(string command) { Console.Error.WriteLine($"Unknown ContentLab command '{command}'. Use help."); return 64; }
static string? Option(string[] args, string name) { int index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
static string? ResolveSidecar(string source) { string value = Path.ChangeExtension(source, ".sha256"); return File.Exists(value) ? value : null; }
static void PrintGroups<T>(string title, IEnumerable<IGrouping<string, T>> groups) { Console.WriteLine($"[{title}]"); foreach (IGrouping<string, T> group in groups.OrderBy(static value => value.Key, StringComparer.Ordinal)) Console.WriteLine($"{group.Key,-36} {group.Count(),4}"); }

static IEnumerable<(string Category, string Id, string Name)> Entries(LaunchContentPack pack)
{
    foreach (RegionContentDefinition value in pack.Regions) yield return ("regions", value.Id, value.Name);
    foreach (SettlementGrammarContentDefinition value in pack.SettlementGrammars) yield return ("settlements", value.Id, value.Name);
    foreach (StructureGrammarContentDefinition value in pack.StructureGrammars) yield return ("structures", value.Id, value.Name);
    foreach (RoomMotifContentDefinition value in pack.RoomMotifs) yield return ("room-motifs", value.Id, value.Name);
    foreach (DamageProfileContentDefinition value in pack.DamageProfiles) yield return ("damage", value.Id, value.Name);
    foreach (FactionContentDefinition value in pack.Factions) yield return ("factions", value.Id, value.Name);
    foreach (OrganizationContentDefinition value in pack.Organizations) yield return ("organizations", value.Id, value.Name);
    foreach (HumanoidArchetypeContentDefinition value in pack.HumanoidArchetypes) yield return ("humanoids", value.Id, value.Name);
    foreach (CreatureFamilyContentDefinition value in pack.CreatureFamilies) yield return ("creatures", value.Id, value.Name);
    foreach (RobotChassisContentDefinition value in pack.RobotChassis) yield return ("robots", value.Id, value.Name);
    foreach (CompanionArchetypeContentDefinition value in pack.CompanionArchetypes) yield return ("companions", value.Id, value.Name);
    foreach (WeaponFamilyContentDefinition value in pack.WeaponFamilies) yield return ("weapons", value.Id, value.Name);
    foreach (WeaponComponentContentDefinition value in pack.WeaponComponents) yield return ("weapon-components", value.Id, value.Name);
    foreach (ArmorFamilyContentDefinition value in pack.ArmorFamilies) yield return ("armor", value.Id, value.Name);
    foreach (ArmorComponentContentDefinition value in pack.ArmorComponents) yield return ("armor-components", value.Id, value.Name);
    foreach (ConsumableContentDefinition value in pack.Consumables) yield return ("consumables", value.Id, value.Name);
    foreach (CraftingProcessContentDefinition value in pack.CraftingProcesses) yield return ("crafting", value.Id, value.Name);
    foreach (PerkContentDefinition value in pack.Perks) yield return ("perks", value.Id, value.Name);
    foreach (BackgroundContentDefinition value in pack.Backgrounds) yield return ("backgrounds", value.Id, value.Name);
    foreach (TraitContentDefinition value in pack.Traits) yield return ("traits", value.Id, value.Name);
    foreach (QuestKernelContentDefinition value in pack.QuestKernels) yield return ("quests", value.Id, value.Name);
    foreach (CampaignKernelContentDefinition value in pack.CampaignKernels) yield return ("campaign", value.Id, value.Name);
    foreach (DialogueActContentDefinition value in pack.DialogueActs) yield return ("dialogue", value.Id, $"{value.Intent} / {value.Tone}");
    foreach (RumorTemplateContentDefinition value in pack.RumorTemplates) yield return ("rumors", value.Id, $"{value.BaseFactTag} / {value.Distortion}");
    foreach (TutorialContentDefinition value in pack.Tutorials) yield return ("tutorials", value.Id, value.Title);
    foreach (EpilogueRuleContentDefinition value in pack.EpilogueRules) yield return ("epilogues", value.Id, $"{value.SubjectKind} / {value.ConditionTag}");
    foreach (AudioProfileContentDefinition value in pack.AudioProfiles) yield return ("audio", value.Id, value.Category);
    foreach (MusicBindingContentDefinition value in pack.MusicBindings) yield return ("music", value.Id, $"{value.Category} / {value.SubjectId}");
}
