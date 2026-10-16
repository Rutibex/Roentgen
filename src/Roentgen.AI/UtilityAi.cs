using Roentgen.Contracts;

namespace Roentgen.AI;

public enum UtilityActionKind
{
    Idle = 0,
    Patrol = 1,
    Investigate = 2,
    RaiseAlarm = 3,
    TakeCover = 4,
    Advance = 5,
    Attack = 6,
    Reload = 7,
    Heal = 8,
    ProtectAlly = 9,
    Retreat = 10,
    Surrender = 11,
    Flee = 12,
    Work = 13,
    Eat = 14,
    Sleep = 15,
    Trade = 16,
    Travel = 17,
    Talk = 18,
    Repair = 19,
    Follow = 20,
    HoldPosition = 21,
    Interact = 22
}

public sealed record UtilityContext(
    ulong Tick,
    StableId AgentId,
    AwarenessLevel Awareness,
    int HealthPermille,
    int MoralePermille,
    int FearPermille,
    int HungerPermille,
    int FatiguePermille,
    int DutyPermille,
    int DistanceToThreatMillimeters,
    int CoverQualityPermille,
    bool HasAmmunition,
    bool WeaponReady,
    bool CanEscape,
    bool Outnumbered,
    bool OrderedToHold,
    bool HasHealingItem,
    bool IsCompanion,
    bool HasCommand);

public sealed record UtilityActionDefinition(
    string Id,
    UtilityActionKind Kind,
    int BaseScore,
    int MinimumCommitTicks,
    int CooldownTicks,
    int SwitchingPenalty,
    Func<UtilityContext, int> Score);

public sealed record UtilityDecision(string ActionId, UtilityActionKind Kind, int Score, IReadOnlyList<KeyValuePair<string, int>> Candidates);

public sealed class UtilityAiEvaluator
{
    private readonly IReadOnlyList<UtilityActionDefinition> actions;
    private readonly Dictionary<(StableId Agent, string Action), ulong> cooldowns = [];

    public UtilityAiEvaluator(IEnumerable<UtilityActionDefinition>? definitions = null) => actions = (definitions ?? CreateDefaults()).OrderBy(static value => value.Id, StringComparer.Ordinal).ToArray();

    public UtilityDecision Select(AgentBrainState brain, UtilityContext context)
    {
        var candidates = new List<KeyValuePair<string, int>>();
        foreach (UtilityActionDefinition action in actions)
        {
            int score = Math.Clamp(action.BaseScore + action.Score(context), -100_000, 100_000);
            if (cooldowns.TryGetValue((brain.AgentId, action.Id), out ulong available) && context.Tick < available) score = int.MinValue / 2;
            if (!string.Equals(brain.CurrentActionId, action.Id, StringComparison.Ordinal)) score -= action.SwitchingPenalty;
            if (context.Tick < brain.ActionCommittedUntilTick && !IsEmergency(action.Kind, context)) score -= 50_000;
            candidates.Add(new KeyValuePair<string, int>(action.Id, score));
        }
        KeyValuePair<string, int> selected = candidates.OrderByDescending(static value => value.Value).ThenBy(static value => value.Key, StringComparer.Ordinal).First();
        UtilityActionDefinition definition = actions.Single(value => string.Equals(value.Id, selected.Key, StringComparison.Ordinal));
        if (!string.Equals(brain.CurrentActionId, definition.Id, StringComparison.Ordinal))
        {
            UtilityActionDefinition? prior = actions.FirstOrDefault(value => string.Equals(value.Id, brain.CurrentActionId, StringComparison.Ordinal));
            if (prior is not null && prior.CooldownTicks > 0) cooldowns[(brain.AgentId, prior.Id)] = checked(context.Tick + (ulong)prior.CooldownTicks);
            brain.CommitAction(definition.Id, context.Tick, definition.MinimumCommitTicks);
        }
        return new UtilityDecision(definition.Id, definition.Kind, selected.Value, candidates.OrderByDescending(static value => value.Value).ThenBy(static value => value.Key, StringComparer.Ordinal).ToArray());
    }

    private static bool IsEmergency(UtilityActionKind kind, UtilityContext context) =>
        kind is UtilityActionKind.Retreat or UtilityActionKind.Surrender or UtilityActionKind.Flee
        || (kind == UtilityActionKind.Heal && context.HealthPermille < 250);

    public static IReadOnlyList<UtilityActionDefinition> CreateDefaults() =>
    [
        new("attack", UtilityActionKind.Attack, 100, 12, 0, 80, c => c.Awareness >= AwarenessLevel.Alerted && c.WeaponReady && c.HasAmmunition ? 900 + c.DutyPermille / 3 - c.FearPermille / 2 : -5000),
        new("reload", UtilityActionKind.Reload, 120, 30, 20, 50, c => c.Awareness >= AwarenessLevel.Alerted && !c.HasAmmunition ? 1300 : -4000),
        new("advance", UtilityActionKind.Advance, 80, 30, 10, 60, c => c.Awareness >= AwarenessLevel.Investigating && c.DistanceToThreatMillimeters > 10_000 ? 650 + c.DutyPermille / 4 : -2000),
        new("take-cover", UtilityActionKind.TakeCover, 70, 45, 20, 80, c => c.Awareness >= AwarenessLevel.Alerted && c.CoverQualityPermille < 500 ? 650 + c.FearPermille / 2 : -1000),
        new("investigate", UtilityActionKind.Investigate, 60, 60, 30, 40, c => c.Awareness is AwarenessLevel.Suspicious or AwarenessLevel.Investigating ? 800 + c.DutyPermille / 4 : -2000),
        new("raise-alarm", UtilityActionKind.RaiseAlarm, 90, 20, 300, 100, c => c.Awareness >= AwarenessLevel.Alerted ? 780 + c.DutyPermille / 3 : -3000),
        new("heal", UtilityActionKind.Heal, 110, 90, 180, 100, c => c.HasHealingItem && c.HealthPermille < 550 ? 1200 - c.HealthPermille : -3000),
        new("retreat", UtilityActionKind.Retreat, 100, 90, 60, 60, c => c.CanEscape && (c.HealthPermille < 350 || c.MoralePermille < 300) ? 1500 + c.FearPermille : -2500),
        new("surrender", UtilityActionKind.Surrender, 100, 300, 0, 0, c => !c.OrderedToHold && c.Outnumbered && c.MoralePermille < 220 && !c.CanEscape ? 2500 : -6000),
        new("flee", UtilityActionKind.Flee, 80, 120, 60, 20, c => c.CanEscape && c.FearPermille > 800 ? 1700 : -3000),
        new("follow", UtilityActionKind.Follow, 80, 30, 0, 40, c => c.IsCompanion && c.HasCommand ? 1000 : -3000),
        new("hold", UtilityActionKind.HoldPosition, 75, 30, 0, 40, c => c.OrderedToHold ? 1200 : -3000),
        new("idle", UtilityActionKind.Idle, 0, 15, 0, 20, c => c.Awareness == AwarenessLevel.Unaware ? 250 - c.HungerPermille / 10 - c.FatiguePermille / 10 : 0)
    ];
}
