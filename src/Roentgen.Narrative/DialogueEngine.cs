using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Narrative;

public sealed record DialogueProfile(
    StableId ActorId,
    string DisplayName,
    string VocabularyRegister,
    int FormalityPermille,
    int DirectnessPermille,
    int EmotionalRestraintPermille,
    IReadOnlyDictionary<string, string> Terms,
    IReadOnlySet<string> AvoidedWords);

public interface IDialoguePresentationContext
{
    DialogueProfile GetProfile(StableId actorId);
    string ResolveDisplayName(StableId id);
    string ResolveTerm(string token, StableId speakerId);
}

public sealed class DialogueTextRealizer
{
    public string Render(string template, DialogueNodeDefinition node, GeneratedQuestPlan plan, DialogueProfile speaker, IDialoguePresentationContext context, ulong visitCount)
    {
        string result = template;
        foreach ((string slot, StableId id) in plan.BoundSlots.OrderBy(static pair => pair.Key, StringComparer.Ordinal)) result = result.Replace("{" + slot + "}", context.ResolveDisplayName(id), StringComparison.Ordinal);
        result = result.Replace("{SPEAKER}", speaker.DisplayName, StringComparison.Ordinal);
        foreach ((string key, string value) in speaker.Terms.OrderBy(static pair => pair.Key, StringComparer.Ordinal)) result = result.Replace("{" + key + "}", value, StringComparison.Ordinal);
        result = ApplyRegister(result, speaker, node.Intent, visitCount);
        foreach (string avoided in speaker.AvoidedWords.Order(StringComparer.Ordinal)) result = result.Replace(avoided, context.ResolveTerm(avoided, speaker.ActorId), StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static string ApplyRegister(string text, DialogueProfile speaker, DialogueIntent intent, ulong visitCount)
    {
        if (visitCount > 0 && intent == DialogueIntent.Greet) return speaker.FormalityPermille >= 600 ? $"We meet again. {text}" : $"Back again. {text}";
        if (speaker.DirectnessPermille >= 700 && text.Length > 160) return text[..157] + "...";
        if (speaker.EmotionalRestraintPermille < 300 && intent is DialogueIntent.Accuse or DialogueIntent.Warn) return text + " Do not make me say it twice.";
        return text;
    }
}

public sealed class DialogueEngine
{
    private readonly NarrativeConditionEvaluator conditionEvaluator = new();
    private readonly NarrativeEffectExecutor effectExecutor = new();
    private readonly DialogueTextRealizer realizer = new();
    private readonly QuestRuntimeEngine questRuntime = new();

    public ConversationSessionState StartConversation(NarrativeWorldState world, QuestInstanceState quest, IReadOnlyList<StableId> participants, ulong visitCount)
    {
        DialogueNodeDefinition first = quest.Plan.Kernel.Dialogue.OrderBy(static node => node.Id, StringComparer.Ordinal).FirstOrDefault() ?? throw new InvalidOperationException($"Quest kernel '{quest.Plan.Kernel.Id}' has no dialogue nodes.");
        StableId id = StableIdFactory.CreateChild(quest.Id, "conversation", visitCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var session = new ConversationSessionState(id, quest.Id, participants, first.Id, visitCount);
        world.AddConversation(session);
        return session;
    }

    public DialogueRenderedNode Render(ConversationSessionState session, QuestInstanceState quest, INarrativeQueryContext query, IDialoguePresentationContext presentation)
    {
        DialogueNodeDefinition node = FindNode(quest, session.CurrentNodeId);
        StableId speakerId = quest.Plan.ResolveSlot(node.SpeakerSlotId);
        DialogueProfile profile = presentation.GetProfile(speakerId);
        string text = realizer.Render(node.TextTemplate, node, quest.Plan, profile, presentation, session.VisitCount);
        var choices = new List<DialogueRenderedChoice>();
        foreach (DialogueChoiceDefinition choice in node.Choices.OrderBy(static choice => choice.Id, StringComparer.Ordinal))
        {
            bool enabled = conditionEvaluator.Evaluate(choice.Availability, quest.Plan, query);
            string rendered = realizer.Render(choice.PlayerTextTemplate, node, quest.Plan, presentation.GetProfile(StableId.Empty), presentation, session.VisitCount);
            choices.Add(new DialogueRenderedChoice(choice.Id, rendered, enabled, enabled ? string.Empty : "Requirements are not met.", choice.Skill.HasValue, choice.Difficulty, choice.Skill?.ToString()));
            if (enabled) session.RevealChoice(choice.Id);
        }
        return new DialogueRenderedNode(session.Id, profile.DisplayName, text, choices, node.EndsConversation);
    }

    public SkillCheckAttemptRecord? Choose(NarrativeWorldState world, ConversationSessionState session, QuestInstanceState quest, string choiceId, StableId playerId, INarrativeQueryContext query, INarrativeExternalEffectRouter router, IDialoguePresentationContext presentation)
    {
        DialogueNodeDefinition node = FindNode(quest, session.CurrentNodeId);
        DialogueChoiceDefinition choice = node.Choices.FirstOrDefault(value => string.Equals(value.Id, choiceId, StringComparison.Ordinal)) ?? throw new KeyNotFoundException($"Unknown dialogue choice '{choiceId}'.");
        if (!conditionEvaluator.Evaluate(choice.Availability, quest.Plan, query)) throw new InvalidOperationException("Dialogue choice conditions are not met.");
        session.SelectChoice(choice.Id);
        SkillCheckAttemptRecord? attempt = null;
        if (choice.Skill.HasValue)
        {
            int capability = query.SkillCapability(playerId, choice.Skill.Value);
            attempt = questRuntime.ResolveSkillCheck(world, quest, playerId, $"dialogue:{node.Id}:{choice.Id}", choice.Skill.Value, choice.Difficulty, choice.RetryPolicy, capability, true, query.Tick);
            if (attempt.Outcome == SkillCheckOutcome.Failure) return attempt;
        }
        NarrativeEffectBatchResult applied = effectExecutor.Apply(world, quest, playerId, choice.Effects, router, query.Tick);
        if (!applied.Success) throw new InvalidOperationException(string.Join("; ", applied.Errors));
        if (string.IsNullOrWhiteSpace(choice.NextNodeId)) session.End(ConversationEndReason.PlayerEnded); else session.MoveTo(choice.NextNodeId);
        DialogueNodeDefinition next = string.IsNullOrWhiteSpace(choice.NextNodeId) ? node : FindNode(quest, choice.NextNodeId);
        if (next.EndsConversation) session.End(ConversationEndReason.SpeakerEnded);
        return attempt;
    }

    private static DialogueNodeDefinition FindNode(QuestInstanceState quest, string id) => quest.Plan.Kernel.Dialogue.FirstOrDefault(node => string.Equals(node.Id, id, StringComparison.Ordinal)) ?? throw new KeyNotFoundException($"Unknown dialogue node '{id}'.");
}

public sealed class BasicDialoguePresentationContext : IDialoguePresentationContext
{
    private readonly IReadOnlyDictionary<StableId, DialogueProfile> profiles;
    private readonly IReadOnlyDictionary<StableId, string> names;
    public BasicDialoguePresentationContext(IReadOnlyDictionary<StableId, DialogueProfile> profiles, IReadOnlyDictionary<StableId, string> names) { this.profiles = profiles; this.names = names; }
    public DialogueProfile GetProfile(StableId actorId) => profiles.TryGetValue(actorId, out DialogueProfile? profile) ? profile : new DialogueProfile(actorId, ResolveDisplayName(actorId), "plain", 500, 500, 500, new Dictionary<string, string>(), new HashSet<string>());
    public string ResolveDisplayName(StableId id) => names.TryGetValue(id, out string? name) ? name : id.IsEmpty ? "the player" : id.ToString()[..8];
    public string ResolveTerm(string token, StableId speakerId) => token;
}
