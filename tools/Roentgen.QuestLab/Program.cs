using System.Text.Json;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Narrative;
using Roentgen.Persistence;

namespace Roentgen.QuestLab;

internal static class Program
{
    private const ulong DefaultSeed = 0x524F454E5447454EUL;

    public static int Main(string[] args)
    {
        string command = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
        ulong seed = ReadSeed(args);
        CampaignSeed campaignSeed = new(seed);
        try
        {
            return command switch
            {
                "list-kernels" => ListKernels(),
                "generate" => Generate(campaignSeed, args),
                "bind" => Generate(campaignSeed, args),
                "inspect-slots" => InspectSlots(campaignSeed),
                "inspect-facts" => InspectFacts(campaignSeed),
                "inspect-knowledge" => InspectKnowledge(campaignSeed),
                "simulate-route" => SimulateRoute(campaignSeed, args),
                "simulate-all-routes" => SimulateAllRoutes(campaignSeed),
                "preview-dialogue" => PreviewDialogue(campaignSeed),
                "kill-role-actor" => KillRoleActor(campaignSeed),
                "destroy-required-item" => DestroyRequiredItem(campaignSeed),
                "block-location" => BlockLocation(campaignSeed),
                "expire-deadline" => ExpireDeadline(campaignSeed),
                "validate" => Validate(campaignSeed),
                "repair" => Repair(campaignSeed),
                "solve" => Solve(campaignSeed),
                "export" => Export(campaignSeed, args),
                "replay" => Replay(campaignSeed),
                "compare-seeds" => CompareSeeds(seed),
                "analyze-repetition" => AnalyzeRepetition(),
                _ => Help()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int Help()
    {
        Console.WriteLine("Roentgen QuestLab — Phase 16");
        Console.WriteLine("Commands: list-kernels, generate, bind, inspect-slots, inspect-facts, inspect-knowledge, simulate-route, simulate-all-routes, preview-dialogue, kill-role-actor, destroy-required-item, block-location, expire-deadline, validate, repair, solve, export, replay, compare-seeds, analyze-repetition");
        Console.WriteLine("Options: --seed <ulong>, --route <id>, --output <path>");
        return 0;
    }

    private static NarrativeWorldState Create(CampaignSeed seed) => Phase16Content.CreateRegressionWorld(seed, StableIdFactory.Create("player", "questlab"));
    private static QuestInstanceState Quest(NarrativeWorldState world) => world.Quests.OrderBy(static value => value.Id).First();
    private static BasicNarrativeQueryContext Query(NarrativeWorldState world, ulong tick = 0, Func<StableId, bool>? alive = null, Func<StableId, bool>? reachable = null) => new(world, tick, actorAlive: alive, locationReachable: reachable, skill: (_, _) => 70);

    private static int ListKernels() { foreach (QuestKernelDefinition kernel in Phase16Content.CreateInitialKernels()) Console.WriteLine($"{kernel.Id,-30} {kernel.DisplayName,-32} routes={kernel.Routes.Count}"); return 0; }
    private static int Generate(CampaignSeed seed, string[] args) { NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); Console.WriteLine($"{quest.Id} {quest.Plan.Kernel.DisplayName} checksum={world.ComputeChecksum()}"); foreach ((string slot, StableId id) in quest.Plan.BoundSlots) Console.WriteLine($"  {slot,-24} {id}  {quest.Plan.BindingReasons[slot]}"); return 0; }
    private static int InspectSlots(CampaignSeed seed) => Generate(seed, []);
    private static int InspectFacts(CampaignSeed seed) { NarrativeWorldState world = Create(seed); foreach (FactRecord fact in world.Facts.OrderBy(static value => value.Id)) Console.WriteLine($"{fact.Id} {fact.PredicateId} subject={fact.SubjectId} value={fact.Object} truth={fact.Truth} source={fact.ProvenanceId}"); return 0; }
    private static int InspectKnowledge(CampaignSeed seed) { NarrativeWorldState world = Create(seed); foreach (KnowledgeClaim claim in world.KnowledgeClaims.OrderBy(static value => value.Id)) Console.WriteLine($"{claim.KnowerId} -> {claim.FactId} {claim.BelievedTruth} confidence={claim.ConfidencePermille} source={claim.Source.Kind}:{claim.Source.Description}"); return 0; }
    private static int SimulateRoute(CampaignSeed seed, string[] args) { string routeId = ReadOption(args, "--route") ?? "technical-route"; NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); QuestRouteDefinition route = quest.Plan.Kernel.Routes.FirstOrDefault(value => value.Id == routeId) ?? throw new ArgumentException($"Unknown route '{routeId}'."); Console.WriteLine($"Route {route.Id} [{route.Kind}] skill={route.PrimarySkill} difficulty={route.Difficulty}"); foreach (string objective in route.RequiredObjectiveIds) Console.WriteLine("  -> " + objective); return 0; }
    private static int SimulateAllRoutes(CampaignSeed seed) { NarrativeWorldState world = Create(seed); foreach (QuestRouteDefinition route in Quest(world).Plan.Kernel.Routes.OrderBy(static value => value.Id, StringComparer.Ordinal)) Console.WriteLine($"{route.Id}: {string.Join(" -> ", route.RequiredObjectiveIds)}"); return 0; }
    private static int PreviewDialogue(CampaignSeed seed) { NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); StableId player = StableIdFactory.Create("player", "questlab"); var runtime = new Roentgen.Simulation.Phase16NarrativeRuntime(world, player); var profiles = new Dictionary<StableId, DialogueProfile> { [Phase16Content.ProspectCaretakerId] = new(Phase16Content.ProspectCaretakerId, "Caretaker Vey", "clinical", 750, 650, 800, new Dictionary<string, string>(), new HashSet<string>()) }; var names = quest.Plan.BoundSlots.Values.Distinct().ToDictionary(static id => id, static id => id.ToString()[..8]); names[Phase16Content.ProspectCaretakerId] = "Caretaker Vey"; DialogueRenderedNode node = runtime.StartConversation(quest.Id, [player, Phase16Content.ProspectCaretakerId], Query(world), new BasicDialoguePresentationContext(profiles, names)); Console.WriteLine($"{node.SpeakerName}: {node.Text}"); foreach (DialogueRenderedChoice choice in node.Choices) Console.WriteLine($"  [{choice.Id}] {choice.Text}"); return 0; }
    private static int KillRoleActor(CampaignSeed seed) { NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); StableId dead = quest.Plan.ResolveSlot("REQUESTING_ACTOR"); var repair = new NarrativeRepairEngine(); IReadOnlyList<RecoveryRecord> records = repair.Repair(world, actor => actor != dead, _ => quest.Plan.ResolveSlot("REPLACEMENT_ACTOR"), 10); Console.WriteLine($"Applied {records.Count} recovery record(s)."); return 0; }
    private static int DestroyRequiredItem(CampaignSeed seed) { NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); Console.WriteLine($"Evidence substitute route available: {quest.Plan.Kernel.Recoveries.Any(static value => value.Kind == RecoveryKind.EvidenceSubstitute)}"); return 0; }
    private static int BlockLocation(CampaignSeed seed) { NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); StableId blocked = quest.Plan.ResolveSlot("PRIMARY_LOCATION"); ValidationReport report = new GeneratedQuestValidator().Validate(quest.Plan, Query(world, reachable: id => id != blocked)); Print(report); return report.IsValid ? 0 : 2; }
    private static int ExpireDeadline(CampaignSeed seed) { NarrativeWorldState world = Create(seed); world.SetTime(60UL * 60UL * 24UL * 10UL, 10); Console.WriteLine($"Advanced to day {world.Day}; quest remains {Quest(world).Status} with aftermath recovery available."); return 0; }
    private static int Validate(CampaignSeed seed) { NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); ValidationReport kernel = new QuestKernelValidator().Validate(quest.Plan.Kernel); ValidationReport plan = new GeneratedQuestValidator().Validate(quest.Plan, Query(world)); ValidationReport state = new NarrativeWorldValidator().Validate(world); Print(kernel); Print(plan); Print(state); return kernel.IsValid && plan.IsValid && state.IsValid ? 0 : 2; }
    private static int Repair(CampaignSeed seed) => KillRoleActor(seed);
    private static int Solve(CampaignSeed seed) { GeneratedQuestPlan plan = Quest(Create(seed)).Plan; var solver = new AbstractQuestSolver(); bool all = true; foreach (AbstractQuestBuild build in AbstractQuestSolver.CreateRegressionBuilds()) { AbstractQuestSolveResult result = solver.Solve(plan, build); Console.WriteLine($"{result.BuildId,-24} solved={result.Solved,-5} route={result.RouteId} {result.FailureReason}"); all &= result.Solved; } return all ? 0 : 2; }
    private static int Export(CampaignSeed seed, string[] args) { string output = ReadOption(args, "--output") ?? "questlab-export.json"; NarrativeWorldState world = Create(seed); QuestInstanceState quest = Quest(world); var payload = new { questId = quest.Id.ToString(), kernel = quest.Plan.Kernel.Id, slots = quest.Plan.BoundSlots.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToString()), checksum = world.ComputeChecksum().ToString(), validation = new NarrativeWorldValidator().Validate(world) }; File.WriteAllText(output, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })); Console.WriteLine(Path.GetFullPath(output)); return 0; }
    private static int Replay(CampaignSeed seed) { NarrativeWorldState first = Create(seed); byte[] bytes = NarrativeWorldSnapshotSerializer.WriteToBytes(first); NarrativeWorldState second = NarrativeWorldSnapshotSerializer.ReadFromBytes(bytes); Console.WriteLine($"before={first.ComputeChecksum()} after={second.ComputeChecksum()} equal={first.ComputeChecksum() == second.ComputeChecksum()}"); return first.ComputeChecksum() == second.ComputeChecksum() ? 0 : 2; }
    private static int CompareSeeds(ulong seed) { NarrativeWorldState a = Create(new CampaignSeed(seed)); NarrativeWorldState b = Create(new CampaignSeed(seed + 1)); Console.WriteLine($"{seed}: {a.ComputeChecksum()}\n{seed + 1}: {b.ComputeChecksum()}"); return a.ComputeChecksum() != b.ComputeChecksum() ? 0 : 2; }
    private static int AnalyzeRepetition() { IReadOnlyList<QuestKernelDefinition> kernels = Phase16Content.CreateInitialKernels(); int duplicateNames = kernels.GroupBy(static value => value.DisplayName, StringComparer.OrdinalIgnoreCase).Count(static group => group.Count() > 1); int duplicateIds = kernels.GroupBy(static value => value.Id, StringComparer.Ordinal).Count(static group => group.Count() > 1); Console.WriteLine($"kernels={kernels.Count} duplicate-names={duplicateNames} duplicate-ids={duplicateIds} dialogue-templates={kernels.SelectMany(static value => value.Dialogue).Select(static value => value.TextTemplate).Distinct(StringComparer.Ordinal).Count()}"); return duplicateNames == 0 && duplicateIds == 0 ? 0 : 2; }
    private static void Print(ValidationReport report) { Console.WriteLine(report.IsValid ? "VALID" : "INVALID"); foreach (ValidationIssue issue in report.Issues) Console.WriteLine($"  {issue.Code}: {issue.Message}"); }
    private static ulong ReadSeed(string[] args) { string? value = ReadOption(args, "--seed"); return value is null ? DefaultSeed : ulong.Parse(value, System.Globalization.CultureInfo.InvariantCulture); }
    private static string? ReadOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
}
