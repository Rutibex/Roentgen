using Roentgen.Contracts;

namespace Roentgen.Rpg;

public sealed record BuildDefinition(string Id, string Name, IReadOnlyDictionary<SkillId, int> Skills, IReadOnlySet<string> BehaviorTags);
public sealed record ScenarioRoute(string Id, string Name, SkillId PrimarySkill, int Difficulty, IReadOnlySet<string> RequiredBehaviors, string ResolutionFamily);
public sealed record BuildRouteResult(string BuildId, string RouteId, bool Completed, int Margin, IReadOnlyList<string> MissingBehaviors);
public sealed record BuildViabilityReport(bool Passed, IReadOnlyList<BuildRouteResult> Results);

public sealed class BuildViabilitySimulator
{
    public BuildRouteResult Evaluate(BuildDefinition build, ScenarioRoute route)
    {
        int skill = build.Skills.GetValueOrDefault(route.PrimarySkill);
        string[] missing = route.RequiredBehaviors.Where(x => !build.BehaviorTags.Contains(x)).Order(StringComparer.Ordinal).ToArray();
        int margin = skill - route.Difficulty;
        return new(build.Id, route.Id, margin >= 0 && missing.Length == 0, margin, missing);
    }

    public BuildViabilityReport EvaluateSixBuildGate(IEnumerable<BuildDefinition> builds, IEnumerable<ScenarioRoute> routes)
    {
        ScenarioRoute[] routeArray = routes.ToArray();
        BuildRouteResult[] results = builds.OrderBy(x => x.Id, StringComparer.Ordinal)
            .Select(build => routeArray.Select(route => Evaluate(build, route)).OrderByDescending(x => x.Completed).ThenByDescending(x => x.Margin).First())
            .ToArray();
        bool passed = results.Length >= 6 && results.All(x => x.Completed) && results.Select(x => x.RouteId).Distinct(StringComparer.Ordinal).Count() >= 6;
        return new(passed, results);
    }
}

public static class Phase12BuildFixtures
{
    public static IReadOnlyList<ScenarioRoute> ProspectClinicRoutes { get; } =
    [
        new("combat-ballistics", "Suppress the hostile force", SkillId.Ballistics, 55, new HashSet<string>(StringComparer.Ordinal), "combat"),
        new("combat-melee", "Close and force surrender", SkillId.Melee, 55, new HashSet<string>(["combat.action.unlock:guard-break"], StringComparer.Ordinal), "melee"),
        new("stealth-security", "Enter unseen and ghost the alarm", SkillId.Security, 55, new HashSet<string>(["interaction.solution.unlock:alarm-ghost"], StringComparer.Ordinal), "stealth"),
        new("engineering-science", "Reroute clinic power", SkillId.Engineering, 55, new HashSet<string>(["interaction.solution.unlock:power-reroute"], StringComparer.Ordinal), "technical"),
        new("medicine-fieldcraft", "Stabilize the exposed patients", SkillId.Medicine, 55, new HashSet<string>(["interaction.solution.unlock:field-stabilize"], StringComparer.Ordinal), "medical"),
        new("influence-commerce", "Broker access and supplies", SkillId.Influence, 55, new HashSet<string>(["dialogue.option.unlock:leverage-ledger"], StringComparer.Ordinal), "social")
    ];

    public static IReadOnlyList<BuildDefinition> SixBuilds { get; } =
    [
        Build("ballistics-specialist", SkillId.Ballistics, 70),
        Build("melee-specialist", SkillId.Melee, 70, "combat.action.unlock:guard-break"),
        Build("stealth-security-specialist", SkillId.Security, 70, "interaction.solution.unlock:alarm-ghost"),
        Build("engineering-science-specialist", SkillId.Engineering, 70, "interaction.solution.unlock:power-reroute"),
        Build("medicine-fieldcraft-specialist", SkillId.Medicine, 70, "interaction.solution.unlock:field-stabilize"),
        Build("influence-commerce-specialist", SkillId.Influence, 70, "dialogue.option.unlock:leverage-ledger")
    ];

    private static BuildDefinition Build(string id, SkillId skill, int value, params string[] tags) =>
        new(id, id.Replace('-', ' '), new Dictionary<SkillId, int> { [skill] = value }, new HashSet<string>(tags, StringComparer.Ordinal));
}
