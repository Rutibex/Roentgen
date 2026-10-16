using Roentgen.Contracts;
using Roentgen.Narrative;

namespace Roentgen.UI;

public enum JournalFilter { Active, Completed, Failed, Campaign }

public sealed record QuestObjectiveRow(string Id, string Text, ObjectiveStatus Status, bool Revealed, bool Optional);
public sealed record QuestReferenceRow(string SlotId, StableId TargetId, string Label, string DisplayName);
public sealed record QuestJournalEntry(
    StableId QuestId,
    string Title,
    QuestStatus Status,
    IReadOnlyList<QuestObjectiveRow> Objectives,
    IReadOnlyList<string> CompletedRoutes,
    IReadOnlyList<string> Themes,
    IReadOnlyList<QuestReferenceRow> References,
    IReadOnlyList<string> RouteHints,
    IReadOnlyList<string> ConsequenceHints,
    string CurrentObjective,
    int CompletedObjectiveCount,
    int RequiredObjectiveCount);
public sealed record KnowledgeRow(StableId FactId, string Predicate, string Value, int ConfidencePermille, KnowledgeSecrecy Secrecy, string Source);
public sealed record RumorRow(StableId RumorId, string Text, int ConfidencePermille, int DistortionPermille, RumorStatus Status);
public sealed record ConversationScreenModel(StableId SessionId, string Speaker, string Text, IReadOnlyList<DialogueRenderedChoice> Choices, bool EndsConversation);
public sealed record NarrativeDebugModel(IReadOnlyList<string> Facts, IReadOnlyList<string> Knowledge, IReadOnlyList<string> Consequences, IReadOnlyList<string> Recoveries);

public sealed class Phase16NarrativeScreenBuilder
{
    public IReadOnlyList<QuestJournalEntry> BuildJournal(NarrativeWorldState world, Func<StableId, string>? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        displayName ??= static id => id.ToString()[..8];
        return world.Quests.OrderBy(static quest => quest.Id).Select(quest =>
        {
            QuestObjectiveRow[] objectives = quest.Plan.Kernel.Objectives.OrderBy(static objective => objective.Id, StringComparer.Ordinal).Select(objective =>
            {
                ObjectiveRuntimeState state = quest.Objectives[objective.Id];
                return new QuestObjectiveRow(objective.Id, RenderTemplate(objective.DisplayTemplate, quest.Plan, displayName), state.Status, state.Revealed, objective.Optional);
            }).Where(static objective => objective.Revealed).ToArray();

            QuestReferenceRow[] references = quest.Plan.BoundSlots.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new QuestReferenceRow(pair.Key, pair.Value, Humanize(pair.Key), displayName(pair.Value)))
                .Where(static row => !string.IsNullOrWhiteSpace(row.DisplayName))
                .ToArray();

            string[] routes = quest.Plan.Kernel.Routes.OrderBy(static route => route.Id, StringComparer.Ordinal).Select(route =>
            {
                string skill = route.PrimarySkill.HasValue ? $" • {Humanize(route.PrimarySkill.Value.ToString())} {route.Difficulty}" : string.Empty;
                return $"{Humanize(route.Kind.ToString())}{skill}";
            }).Distinct(StringComparer.Ordinal).ToArray();

            string[] consequences = quest.Plan.Kernel.Consequences.OrderBy(static effect => effect.Id, StringComparer.Ordinal)
                .Select(RenderConsequence).Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();

            string current = objectives.FirstOrDefault(static value => value.Status == ObjectiveStatus.Active)?.Text
                ?? objectives.FirstOrDefault(static value => value.Status == ObjectiveStatus.Dormant)?.Text
                ?? objectives.FirstOrDefault(static value => value.Status == ObjectiveStatus.Failed)?.Text
                ?? (quest.Status == QuestStatus.Completed ? "Quest complete." : "No currently revealed objective.");
            int completed = objectives.Count(static value => value.Status == ObjectiveStatus.Completed && !value.Optional);
            int required = quest.Plan.Kernel.Objectives.Count(static value => !value.Optional);
            return new QuestJournalEntry(
                quest.Id,
                quest.Plan.Kernel.DisplayName,
                quest.Status,
                objectives,
                quest.CompletedRouteIds.Order(StringComparer.Ordinal).ToArray(),
                quest.Plan.Kernel.Themes.Select(Humanize).ToArray(),
                references,
                routes,
                consequences,
                current,
                completed,
                required);
        }).ToArray();
    }

    public IReadOnlyList<KnowledgeRow> BuildKnowledge(NarrativeWorldState world, StableId knowerId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.GetKnowledge(knowerId).OrderByDescending(static claim => claim.ConfidencePermille).ThenBy(static claim => claim.Id).Select(claim =>
        {
            FactRecord fact = world.FactDatabase.Get(claim.FactId);
            return new KnowledgeRow(fact.Id, fact.PredicateId, fact.Object.ToString(), claim.ConfidencePermille, claim.Secrecy, claim.Source.Description);
        }).ToArray();
    }

    public IReadOnlyList<RumorRow> BuildRumors(NarrativeWorldState world, Func<StableId, string> displayName)
    {
        var rumorSystem = new RumorSystem();
        return world.Rumors.OrderByDescending(static rumor => rumor.CreatedDay).ThenBy(static rumor => rumor.Id).Select(rumor =>
        {
            FactRecord fact = world.FactDatabase.Get(rumor.FactId);
            return new RumorRow(rumor.Id, rumorSystem.Render(rumor, fact, displayName), rumor.ConfidencePermille, rumor.DistortionPermille, rumor.Status);
        }).ToArray();
    }

    public ConversationScreenModel BuildConversation(DialogueRenderedNode node) => new(node.SessionId, node.SpeakerName, node.Text, node.Choices, node.EndsConversation);

    public NarrativeDebugModel BuildDebug(NarrativeWorldState world) => new(
        world.Facts.OrderBy(static fact => fact.Id).Select(fact => $"{fact.PredicateId}({fact.SubjectId})={fact.Object} [{fact.Truth}]").ToArray(),
        world.KnowledgeClaims.OrderBy(static claim => claim.Id).Select(claim => $"{claim.KnowerId} believes {claim.FactId} as {claim.BelievedTruth} at {claim.ConfidencePermille}/1000 via {claim.Source.Kind}").ToArray(),
        world.Consequences.OrderBy(static consequence => consequence.Id).Select(consequence => $"{consequence.EffectId} from quest {consequence.QuestId} at tick {consequence.AppliedTick}").ToArray(),
        world.Recoveries.OrderBy(static recovery => recovery.Id).Select(recovery => $"{recovery.Kind}: {recovery.ReplacedEntityId} -> {recovery.ReplacementEntityId}").ToArray());

    private static string RenderTemplate(string template, GeneratedQuestPlan plan, Func<StableId, string> displayName)
    {
        string result = template;
        foreach ((string slot, StableId id) in plan.BoundSlots.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            result = result.Replace("{" + slot + "}", displayName(id), StringComparison.Ordinal);
        return result;
    }

    private static string RenderConsequence(NarrativeEffectDefinition effect)
    {
        string action = Humanize(effect.Kind.ToString());
        if (!string.IsNullOrWhiteSpace(effect.Argument)) return $"{action}: {Humanize(effect.Argument)}";
        if (effect.Amount != 0) return $"{action}: {(effect.Amount > 0 ? "+" : string.Empty)}{effect.Amount}";
        return action;
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string cleaned = value.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ');
        var chars = new List<char>(cleaned.Length + 8);
        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(cleaned[i - 1])) chars.Add(' ');
            chars.Add(c);
        }
        string text = new(chars.ToArray());
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
