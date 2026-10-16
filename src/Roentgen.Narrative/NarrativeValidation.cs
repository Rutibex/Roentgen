using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Narrative;

public sealed class QuestKernelValidator : IValidator<QuestKernelDefinition>
{
    public ValidationReport Validate(QuestKernelDefinition value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var issues = new List<ValidationIssue>();
        string[] duplicateSlots = value.Slots.GroupBy(static slot => slot.Id, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key).ToArray();
        foreach (string id in duplicateSlots) issues.Add(new ValidationIssue("kernel.slot.duplicate", $"Duplicate slot '{id}'.", true));
        var nodeIds = value.Nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        foreach (QuestNodeDefinition node in value.Nodes)
            foreach (string next in node.NextNodeIds)
                if (!nodeIds.Contains(next)) issues.Add(new ValidationIssue("kernel.node.missing", $"Node '{node.Id}' points to missing node '{next}'.", true));
        if (value.Routes.Count < 3) issues.Add(new ValidationIssue("kernel.route.minimum", "Major quest kernel requires at least three solution routes.", true));
        if (!value.Routes.Any(static route => route.Kind is not QuestRouteKind.Combat)) issues.Add(new ValidationIssue("kernel.route.noncombat", "Quest has no noncombat solution route.", true));
        if (value.Dialogue.Count == 0) issues.Add(new ValidationIssue("kernel.dialogue.empty", "Quest has no dialogue graph.", true));
        foreach (QuestRouteDefinition route in value.Routes)
            foreach (string objective in route.RequiredObjectiveIds)
                if (!value.Objectives.Any(candidate => string.Equals(candidate.Id, objective, StringComparison.Ordinal))) issues.Add(new ValidationIssue("kernel.route.objective-missing", $"Route '{route.Id}' references missing objective '{objective}'.", true));
        foreach (QuestSlotDefinition slot in value.Slots.Where(static slot => slot.Required))
            if (string.IsNullOrWhiteSpace(slot.TypeId)) issues.Add(new ValidationIssue("kernel.slot.type", $"Required slot '{slot.Id}' has no type.", true));
        return new ValidationReport(issues.All(static issue => !issue.IsCritical), issues);
    }
}

public sealed class GeneratedQuestValidator
{
    private readonly QuestKernelValidator kernelValidator = new();
    public ValidationReport Validate(GeneratedQuestPlan plan, INarrativeQueryContext context)
    {
        var issues = new List<ValidationIssue>(kernelValidator.Validate(plan.Kernel).Issues);
        foreach (QuestSlotDefinition slot in plan.Kernel.Slots.Where(static slot => slot.Required))
            if (!plan.BoundSlots.TryGetValue(slot.Id, out StableId id) || id.IsEmpty) issues.Add(new ValidationIssue("quest.slot.unbound", $"Required slot '{slot.Id}' is not bound.", true));
        foreach (QuestSlotDefinition location in plan.Kernel.Slots.Where(static slot => string.Equals(slot.TypeId, "Location", StringComparison.Ordinal) && slot.Required))
            if (!context.LocationReachable(plan.ResolveSlot(location.Id))) issues.Add(new ValidationIssue("quest.location.unreachable", $"Location slot '{location.Id}' is unreachable.", true));
        int viableRoutes = plan.Kernel.Routes.Count(route => RouteViable(route, context, plan));
        if (viableRoutes < 3) issues.Add(new ValidationIssue("quest.routes.insufficient", $"Only {viableRoutes} solution routes are currently viable.", true));
        if (!plan.Kernel.Routes.Any(route => route.Kind != QuestRouteKind.Combat && RouteViable(route, context, plan))) issues.Add(new ValidationIssue("quest.route.noncombat-unavailable", "No noncombat route is currently viable.", true));
        foreach (DialogueNodeDefinition node in plan.Kernel.Dialogue.Where(static node => !string.IsNullOrWhiteSpace(node.RequiredFactSlotId)))
            if (plan.ResolveSlot(node.RequiredFactSlotId).IsEmpty) issues.Add(new ValidationIssue("dialogue.fact.unsourced", $"Dialogue node '{node.Id}' requires an unbound fact slot.", true));
        return new ValidationReport(issues.All(static issue => !issue.IsCritical), issues);
    }
    private static bool RouteViable(QuestRouteDefinition route, INarrativeQueryContext context, GeneratedQuestPlan plan)
    {
        if (!route.PrimarySkill.HasValue) return true;
        StableId actor = plan.ResolveSlot("PLAYER");
        return actor.IsEmpty || context.SkillCapability(actor, route.PrimarySkill.Value) >= Math.Max(0, route.Difficulty - 25);
    }
}

public sealed class NarrativeWorldValidator : IValidator<NarrativeWorldState>
{
    public ValidationReport Validate(NarrativeWorldState value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var issues = new List<ValidationIssue>();
        var factIds = value.Facts.Select(static fact => fact.Id).ToHashSet();
        foreach (KnowledgeClaim claim in value.KnowledgeClaims)
            if (!factIds.Contains(claim.FactId)) issues.Add(new ValidationIssue("knowledge.fact.missing", $"Knowledge claim '{claim.Id}' references missing fact '{claim.FactId}'.", true));
        foreach (QuestInstanceState quest in value.Quests)
        {
            foreach (string active in quest.ActiveNodeIds)
                if (!quest.Plan.Kernel.Nodes.Any(node => string.Equals(node.Id, active, StringComparison.Ordinal))) issues.Add(new ValidationIssue("quest.node.active-missing", $"Quest '{quest.Id}' has unknown active node '{active}'.", true));
            if (quest.Status == QuestStatus.Completed)
            {
                bool incompleteRequired = quest.Plan.Kernel.Objectives.Where(static definition => !definition.Optional)
                    .Any(definition => !quest.Objectives.TryGetValue(definition.Id, out ObjectiveRuntimeState? state) || state.Status != ObjectiveStatus.Completed);
                if (incompleteRequired) issues.Add(new ValidationIssue("quest.completed.required-objective", $"Completed quest '{quest.Id}' retains an incomplete required objective.", true));
            }
        }
        foreach (ConversationSessionState conversation in value.Conversations)
            if (!value.TryGetQuest(conversation.QuestId, out QuestInstanceState? quest) || quest is null || !quest.Plan.Kernel.Dialogue.Any(node => string.Equals(node.Id, conversation.CurrentNodeId, StringComparison.Ordinal))) issues.Add(new ValidationIssue("dialogue.session.invalid", $"Conversation '{conversation.Id}' references an invalid quest or node.", true));
        foreach (SkillCheckAttemptRecord attempt in value.SkillAttempts.GroupBy(static attempt => attempt.Id).Where(static group => group.Count() > 1).SelectMany(static group => group)) issues.Add(new ValidationIssue("skill-attempt.duplicate", $"Duplicate skill attempt '{attempt.Id}'.", true));
        return new ValidationReport(issues.All(static issue => !issue.IsCritical), issues);
    }
}

public sealed class NarrativeRepairEngine
{
    public IReadOnlyList<RecoveryRecord> Repair(NarrativeWorldState world, Func<StableId, bool> actorAlive, Func<StableId, StableId?> replacementResolver, ulong tick)
    {
        var applied = new List<RecoveryRecord>();
        foreach (QuestInstanceState quest in world.Quests.Where(static quest => quest.Status is QuestStatus.Active or QuestStatus.Offered).OrderBy(static quest => quest.Id))
        {
            foreach (QuestRecoveryDefinition recovery in quest.Plan.Kernel.Recoveries.Where(static recovery => recovery.Kind == RecoveryKind.ActorReplacement))
            {
                StableId original = quest.Plan.ResolveSlot(recovery.TriggerSlotId);
                if (original.IsEmpty || actorAlive(original)) continue;
                StableId? replacement = replacementResolver(original);
                if (!replacement.HasValue || replacement.Value.IsEmpty) continue;
                StableId id = StableIdFactory.CreateChild(quest.Id, "recovery", $"{recovery.Id}:{original}:{replacement.Value}");
                if (world.Recoveries.Any(item => item.Id == id)) continue;
                var record = new RecoveryRecord(id, quest.Id, recovery.Id, recovery.Kind, original, replacement.Value, tick);
                world.AddRecovery(record); quest.ActivateNode(recovery.ActivationNodeId); applied.Add(record);
            }
        }
        return applied;
    }
}

public sealed record AbstractQuestBuild(string Id, IReadOnlyDictionary<SkillId, int> Skills, IReadOnlySet<QuestRouteKind> AllowedRouteKinds);
public sealed record AbstractQuestSolveResult(string BuildId, StableId QuestId, bool Solved, string RouteId, IReadOnlyList<string> Steps, string FailureReason);

public sealed class AbstractQuestSolver
{
    public AbstractQuestSolveResult Solve(GeneratedQuestPlan plan, AbstractQuestBuild build)
    {
        foreach (QuestRouteDefinition route in plan.Kernel.Routes.Where(route => build.AllowedRouteKinds.Contains(route.Kind)).OrderBy(static route => route.Id, StringComparer.Ordinal))
        {
            if (route.PrimarySkill.HasValue && (!build.Skills.TryGetValue(route.PrimarySkill.Value, out int skill) || skill < route.Difficulty - 10)) continue;
            return new AbstractQuestSolveResult(build.Id, plan.Id, true, route.Id, route.RequiredObjectiveIds.ToArray(), string.Empty);
        }
        return new AbstractQuestSolveResult(build.Id, plan.Id, false, string.Empty, [], "No route matched the build's capabilities.");
    }

    public static IReadOnlyList<AbstractQuestBuild> CreateRegressionBuilds() =>
    [
        new("combat", new Dictionary<SkillId, int> { [SkillId.Ballistics] = 70 }, new HashSet<QuestRouteKind> { QuestRouteKind.Combat }),
        new("stealth-security", new Dictionary<SkillId, int> { [SkillId.Stealth] = 70, [SkillId.Security] = 65 }, new HashSet<QuestRouteKind> { QuestRouteKind.Stealth }),
        new("engineering-science", new Dictionary<SkillId, int> { [SkillId.Engineering] = 70, [SkillId.Science] = 70 }, new HashSet<QuestRouteKind> { QuestRouteKind.Technical, QuestRouteKind.Scientific }),
        new("medicine-fieldcraft", new Dictionary<SkillId, int> { [SkillId.Medicine] = 70, [SkillId.Fieldcraft] = 65 }, new HashSet<QuestRouteKind> { QuestRouteKind.Medical, QuestRouteKind.Exploration }),
        new("influence-commerce", new Dictionary<SkillId, int> { [SkillId.Influence] = 70, [SkillId.Commerce] = 70 }, new HashSet<QuestRouteKind> { QuestRouteKind.Social, QuestRouteKind.Economic }),
        new("generalist", Enum.GetValues<SkillId>().ToDictionary(static skill => skill, static _ => 50), Enum.GetValues<QuestRouteKind>().ToHashSet())
    ];
}
