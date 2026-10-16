using System.Globalization;
using Roentgen.Campaign;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Narrative;

const ulong DefaultSeed = 0x524F454E5447454EUL;
string command = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "help";
ulong seedValue = ReadSeed(args.Skip(1).FirstOrDefault());
var seed = new CampaignSeed(seedValue);
StableId playerId = StableIdFactory.Create("player", "campaign-lab");
SocietyWorldState CreateSociety() => Phase15Content.CreateRegressionWorld(seed, playerId);
GeneratedCampaignPlan Generate()
{
    SocietyWorldState society = CreateSociety();
    return new CampaignGenerator().Generate(new CampaignGenerationRequest(
        seed,
        playerId,
        society,
        Phase16Content.CreateRegressionBindingContext(),
        Phase16Content.CreateInitialKernels()));
}

try
{
    switch (command)
    {
        case "generate":
        {
            GeneratedCampaignPlan plan = Generate();
            PrintSummary(plan);
            break;
        }
        case "inspect-history":
        {
            GeneratedCampaignPlan plan = Generate();
            foreach (RegionalHistoryRecord region in plan.History.Regions.OrderBy(static value => value.Region))
            {
                HistoricalEventRecord[] regionEvents = plan.History.Events
                    .Where(value => region.EventIds.Contains(value.Id))
                    .OrderBy(static value => value.Day)
                    .ThenBy(static value => value.Id)
                    .ToArray();
                StableId founding = regionEvents.FirstOrDefault(static value => value.Kind == HistoricalEventKind.FactionFounded)?.Id ?? StableId.Empty;
                StableId crisis = regionEvents.LastOrDefault()?.Id ?? StableId.Empty;
                Console.WriteLine($"[{region.Region}] founding={founding} crisis={crisis} location={region.RepresentativeLocationId}");
                foreach (HistoricalEventRecord historicalEvent in regionEvents)
                    Console.WriteLine($"  day {historicalEvent.Day,6}: {historicalEvent.Kind,-18} visibility={historicalEvent.PublicVisibilityPermille,3} {historicalEvent.Summary}");
            }
            break;
        }
        case "inspect-premise":
        {
            CampaignPremiseSelection premise = Generate().Premise;
            Console.WriteLine($"{premise.Kernel.DisplayName} ({premise.Kernel.Kind})");
            Console.WriteLine($"region={premise.PrimaryRegion} factions={premise.PrimaryFactionId}/{premise.SecondaryFactionId}");
            Console.WriteLine($"hidden-cause={premise.HiddenCauseEventId} score={premise.Score}");
            foreach (string reason in premise.Reasons) Console.WriteLine($"  {reason}");
            break;
        }
        case "print-graph":
        {
            GeneratedCampaignPlan plan = Generate();
            foreach (GeneratedMilestonePlan milestone in plan.Milestones.OrderBy(static value => value.Sequence))
                Console.WriteLine($"{milestone.Sequence:D2} {milestone.Kernel.Role,-22} {milestone.Kernel.DisplayName} -> [{string.Join(",", milestone.NextIds)}] quest={milestone.QuestPlan.Kernel.Id}");
            break;
        }
        case "validate":
        {
            GeneratedCampaignPlan plan = Generate();
            CampaignValidationReport report = new CampaignPlanValidator().Validate(plan);
            PrintSummary(plan);
            foreach (CampaignValidationIssue issue in report.Issues) Console.WriteLine($"{(issue.IsCritical ? "ERROR" : "WARN ")} {issue.Code}: {issue.Message}");
            Environment.ExitCode = report.IsValid ? 0 : 2;
            break;
        }
        case "solve":
        {
            GeneratedCampaignPlan plan = Generate();
            AbstractQuestBuild build = AbstractQuestSolver.CreateRegressionBuilds().FirstOrDefault(value => args.Contains(value.Id, StringComparer.OrdinalIgnoreCase))
                ?? AbstractQuestSolver.CreateRegressionBuilds()[0];
            CampaignAutoCompletionResult result = new CampaignAutoAgent().Complete(plan, CreateSociety(), build, playerId);
            PrintSolve(result);
            Environment.ExitCode = result.Completed ? 0 : 3;
            break;
        }
        case "solve-all-builds":
        {
            GeneratedCampaignPlan plan = Generate();
            IReadOnlyList<CampaignAutoCompletionResult> results = new CampaignAutoAgent().CompleteRegressionBuilds(plan, CreateSociety, playerId);
            foreach (CampaignAutoCompletionResult result in results) PrintSolve(result);
            Environment.ExitCode = results.All(static value => value.Completed) ? 0 : 3;
            break;
        }
        case "kill-critical-role":
        {
            GeneratedCampaignPlan plan = Generate();
            var world = new CampaignWorldState(plan);
            CriticalRoleRuntimeState target = world.CriticalRoles.OrderBy(static value => value.RoleId, StringComparer.Ordinal).First();
            IReadOnlyList<CriticalRoleReplacementRecord> records = new CampaignDirector().RepairCriticalRoles(world, new CriticalRoleRepairContext(id => id != target.CurrentEntityId), 100);
            Console.WriteLine($"removed={target.CurrentEntityId} role={target.RoleId}");
            foreach (CriticalRoleReplacementRecord record in records) Console.WriteLine($"replacement={record.ReplacementEntityId} kind={record.FallbackKind}");
            Environment.ExitCode = records.Count == 1 ? 0 : 4;
            break;
        }
        case "analyze-endings":
        {
            GeneratedCampaignPlan plan = Generate();
            foreach (EndingFamilyDefinition ending in plan.EndingFamilies.OrderByDescending(static value => value.Priority))
                Console.WriteLine($"{ending.Priority,4} {ending.Tone,-20} {ending.DisplayName,-28} requirements={ending.Requirements.Count}");
            break;
        }
        case "export-epilogue":
        {
            GeneratedCampaignPlan plan = Generate();
            CampaignAutoCompletionResult result = new CampaignAutoAgent().Complete(plan, CreateSociety(), AbstractQuestSolver.CreateRegressionBuilds()[0], playerId);
            if (!result.Completed) throw new InvalidOperationException(string.Join("; ", result.Errors));
            Console.WriteLine($"ending={result.EndingId}");
            foreach (string line in result.Trace.Where(static value => value.StartsWith("convergence:", StringComparison.Ordinal) || value.StartsWith("choice:", StringComparison.Ordinal))) Console.WriteLine(line);
            break;
        }
        default:
            PrintHelp();
            break;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}

static ulong ReadSeed(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return DefaultSeed;
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return ulong.Parse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    return ulong.Parse(value, CultureInfo.InvariantCulture);
}

static void PrintSummary(GeneratedCampaignPlan plan)
{
    Console.WriteLine($"campaign={plan.Id}");
    Console.WriteLine($"seed=0x{plan.CampaignSeed.Value:X16}");
    Console.WriteLine($"fingerprint={plan.Fingerprint}");
    Console.WriteLine($"premise={plan.Premise.Kernel.Id}");
    Console.WriteLine($"history: regions={plan.History.Regions.Count} events={plan.History.Events.Count}");
    Console.WriteLine($"milestones={plan.Milestones.Count} revelations={plan.Revelations.Count} choices={plan.Choices.Count} endings={plan.EndingFamilies.Count}");
}

static void PrintSolve(CampaignAutoCompletionResult result)
{
    Console.WriteLine($"{result.BuildId,-22} completed={result.Completed,-5} ending={result.EndingId} checksum={result.FinalChecksum}");
    foreach (string error in result.Errors) Console.WriteLine($"  error: {error}");
}

static void PrintHelp()
{
    Console.WriteLine("Roentgen CampaignLab Phase 17");
    Console.WriteLine("  generate [seed]");
    Console.WriteLine("  inspect-history [seed]");
    Console.WriteLine("  inspect-premise [seed]");
    Console.WriteLine("  print-graph [seed]");
    Console.WriteLine("  validate [seed]");
    Console.WriteLine("  solve [seed] [build-id]");
    Console.WriteLine("  solve-all-builds [seed]");
    Console.WriteLine("  kill-critical-role [seed]");
    Console.WriteLine("  analyze-endings [seed]");
    Console.WriteLine("  export-epilogue [seed]");
}
