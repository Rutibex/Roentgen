using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Narrative;

public sealed record QuestSlotDefinition(string Id, string TypeId, bool Required, bool Unique, IReadOnlyList<string> Tags) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteString(TypeId); writer.WriteBoolean(Required); writer.WriteBoolean(Unique); writer.WriteCount(Tags.Count); foreach (string tag in Tags.Order(StringComparer.Ordinal)) writer.WriteString(tag); }
    public static QuestSlotDefinition ReadCanonical(CanonicalReader reader) { string id = reader.ReadRequiredString(); string type = reader.ReadRequiredString(); bool required = reader.ReadBoolean(); bool unique = reader.ReadBoolean(); int count = reader.ReadCount(); var tags = new string[count]; for (int i = 0; i < count; i++) tags[i] = reader.ReadRequiredString(); return new(id, type, required, unique, tags); }
}

public sealed record NarrativeConditionDefinition(
    NarrativeConditionKind Kind,
    string Id,
    string PredicateId,
    string SlotId,
    NarrativeValue ExpectedValue,
    int Minimum,
    int Maximum,
    IReadOnlyList<NarrativeConditionDefinition> Children) : ICanonicalWritable
{
    public static NarrativeConditionDefinition Always(string id = "always") => new(NarrativeConditionKind.Always, id, string.Empty, string.Empty, NarrativeValue.None, 0, 0, []);
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)Kind); writer.WriteString(Id); writer.WriteString(PredicateId); writer.WriteString(SlotId); ExpectedValue.WriteCanonical(writer); writer.WriteInt32(Minimum); writer.WriteInt32(Maximum); writer.WriteCount(Children.Count); foreach (NarrativeConditionDefinition child in Children) child.WriteCanonical(writer);
    }
    public static NarrativeConditionDefinition ReadCanonical(CanonicalReader reader)
    {
        NarrativeConditionKind kind = (NarrativeConditionKind)reader.ReadInt32(); string id = reader.ReadRequiredString(); string predicate = reader.ReadRequiredString(); string slot = reader.ReadRequiredString(); NarrativeValue expected = NarrativeValue.ReadCanonical(reader); int min = reader.ReadInt32(); int max = reader.ReadInt32(); int count = reader.ReadCount(); var children = new NarrativeConditionDefinition[count]; for (int i = 0; i < count; i++) children[i] = ReadCanonical(reader); return new(kind, id, predicate, slot, expected, min, max, children);
    }
}

public sealed record NarrativeEffectDefinition(
    NarrativeEffectKind Kind,
    string Id,
    string TargetSlotId,
    string PredicateId,
    NarrativeValue Value,
    int Amount,
    string Argument) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt32((int)Kind); writer.WriteString(Id); writer.WriteString(TargetSlotId); writer.WriteString(PredicateId); Value.WriteCanonical(writer); writer.WriteInt32(Amount); writer.WriteString(Argument); }
    public static NarrativeEffectDefinition ReadCanonical(CanonicalReader reader) => new((NarrativeEffectKind)reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadRequiredString(), NarrativeValue.ReadCanonical(reader), reader.ReadInt32(), reader.ReadRequiredString());
}

public sealed record QuestObjectiveDefinition(
    string Id,
    string DisplayTemplate,
    ObjectiveKind Kind,
    ObjectiveComposition Composition,
    bool Hidden,
    bool Optional,
    string TargetSlotId,
    NarrativeConditionDefinition CompletionCondition,
    NarrativeConditionDefinition FailureCondition,
    IReadOnlyList<NarrativeEffectDefinition> CompletionEffects) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id); writer.WriteString(DisplayTemplate); writer.WriteInt32((int)Kind); writer.WriteInt32((int)Composition); writer.WriteBoolean(Hidden); writer.WriteBoolean(Optional); writer.WriteString(TargetSlotId); CompletionCondition.WriteCanonical(writer); FailureCondition.WriteCanonical(writer); writer.WriteCount(CompletionEffects.Count); foreach (NarrativeEffectDefinition effect in CompletionEffects) effect.WriteCanonical(writer);
    }
    public static QuestObjectiveDefinition ReadCanonical(CanonicalReader reader)
    {
        string id = reader.ReadRequiredString(); string display = reader.ReadRequiredString(); ObjectiveKind kind = (ObjectiveKind)reader.ReadInt32(); ObjectiveComposition composition = (ObjectiveComposition)reader.ReadInt32(); bool hidden = reader.ReadBoolean(); bool optional = reader.ReadBoolean(); string target = reader.ReadRequiredString(); NarrativeConditionDefinition completion = NarrativeConditionDefinition.ReadCanonical(reader); NarrativeConditionDefinition failure = NarrativeConditionDefinition.ReadCanonical(reader); int count = reader.ReadCount(); var effects = new NarrativeEffectDefinition[count]; for (int i = 0; i < count; i++) effects[i] = NarrativeEffectDefinition.ReadCanonical(reader); return new(id, display, kind, composition, hidden, optional, target, completion, failure, effects);
    }
}

public sealed record QuestNodeDefinition(
    string Id,
    QuestNodeKind Kind,
    string ObjectiveId,
    NarrativeConditionDefinition EntryCondition,
    IReadOnlyList<string> NextNodeIds,
    IReadOnlyList<NarrativeEffectDefinition> Effects) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)Kind); writer.WriteString(ObjectiveId); EntryCondition.WriteCanonical(writer); writer.WriteCount(NextNodeIds.Count); foreach (string next in NextNodeIds.Order(StringComparer.Ordinal)) writer.WriteString(next); writer.WriteCount(Effects.Count); foreach (NarrativeEffectDefinition effect in Effects) effect.WriteCanonical(writer); }
    public static QuestNodeDefinition ReadCanonical(CanonicalReader reader) { string id = reader.ReadRequiredString(); QuestNodeKind kind = (QuestNodeKind)reader.ReadInt32(); string objective = reader.ReadRequiredString(); NarrativeConditionDefinition condition = NarrativeConditionDefinition.ReadCanonical(reader); int nextCount = reader.ReadCount(); var next = new string[nextCount]; for (int i = 0; i < nextCount; i++) next[i] = reader.ReadRequiredString(); int effectCount = reader.ReadCount(); var effects = new NarrativeEffectDefinition[effectCount]; for (int i = 0; i < effectCount; i++) effects[i] = NarrativeEffectDefinition.ReadCanonical(reader); return new(id, kind, objective, condition, next, effects); }
}

public sealed record QuestRouteDefinition(
    string Id,
    QuestRouteKind Kind,
    string EntryNodeId,
    IReadOnlyList<string> RequiredObjectiveIds,
    SkillId? PrimarySkill,
    int Difficulty,
    RetryPolicy RetryPolicy) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)Kind); writer.WriteString(EntryNodeId); writer.WriteCount(RequiredObjectiveIds.Count); foreach (string objective in RequiredObjectiveIds.Order(StringComparer.Ordinal)) writer.WriteString(objective); writer.WriteBoolean(PrimarySkill.HasValue); if (PrimarySkill.HasValue) writer.WriteInt32((int)PrimarySkill.Value); writer.WriteInt32(Difficulty); writer.WriteInt32((int)RetryPolicy); }
    public static QuestRouteDefinition ReadCanonical(CanonicalReader reader) { string id = reader.ReadRequiredString(); QuestRouteKind kind = (QuestRouteKind)reader.ReadInt32(); string entry = reader.ReadRequiredString(); int count = reader.ReadCount(); var objectives = new string[count]; for (int i = 0; i < count; i++) objectives[i] = reader.ReadRequiredString(); SkillId? skill = reader.ReadBoolean() ? (SkillId)reader.ReadInt32() : null; return new(id, kind, entry, objectives, skill, reader.ReadInt32(), (RetryPolicy)reader.ReadInt32()); }
}

public sealed record QuestRecoveryDefinition(string Id, RecoveryKind Kind, string TriggerSlotId, string ReplacementSlotId, string ActivationNodeId) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)Kind); writer.WriteString(TriggerSlotId); writer.WriteString(ReplacementSlotId); writer.WriteString(ActivationNodeId); }
    public static QuestRecoveryDefinition ReadCanonical(CanonicalReader reader) => new(reader.ReadRequiredString(), (RecoveryKind)reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadRequiredString());
}

public sealed record DialogueChoiceDefinition(
    string Id,
    string PlayerTextTemplate,
    NarrativeConditionDefinition Availability,
    SkillId? Skill,
    int Difficulty,
    RetryPolicy RetryPolicy,
    string NextNodeId,
    IReadOnlyList<NarrativeEffectDefinition> Effects) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteString(PlayerTextTemplate); Availability.WriteCanonical(writer); writer.WriteBoolean(Skill.HasValue); if (Skill.HasValue) writer.WriteInt32((int)Skill.Value); writer.WriteInt32(Difficulty); writer.WriteInt32((int)RetryPolicy); writer.WriteString(NextNodeId); writer.WriteCount(Effects.Count); foreach (NarrativeEffectDefinition effect in Effects) effect.WriteCanonical(writer); }
    public static DialogueChoiceDefinition ReadCanonical(CanonicalReader reader) { string id = reader.ReadRequiredString(); string text = reader.ReadRequiredString(); NarrativeConditionDefinition availability = NarrativeConditionDefinition.ReadCanonical(reader); SkillId? skill = reader.ReadBoolean() ? (SkillId)reader.ReadInt32() : null; int difficulty = reader.ReadInt32(); RetryPolicy retry = (RetryPolicy)reader.ReadInt32(); string next = reader.ReadRequiredString(); int count = reader.ReadCount(); var effects = new NarrativeEffectDefinition[count]; for (int i = 0; i < count; i++) effects[i] = NarrativeEffectDefinition.ReadCanonical(reader); return new(id, text, availability, skill, difficulty, retry, next, effects); }
}

public sealed record DialogueNodeDefinition(
    string Id,
    DialogueIntent Intent,
    string SpeakerSlotId,
    string TextTemplate,
    NarrativeConditionDefinition Availability,
    string RequiredFactSlotId,
    IReadOnlyList<DialogueChoiceDefinition> Choices,
    string DefaultNextNodeId,
    bool EndsConversation) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(Id); writer.WriteInt32((int)Intent); writer.WriteString(SpeakerSlotId); writer.WriteString(TextTemplate); Availability.WriteCanonical(writer); writer.WriteString(RequiredFactSlotId); writer.WriteCount(Choices.Count); foreach (DialogueChoiceDefinition choice in Choices) choice.WriteCanonical(writer); writer.WriteString(DefaultNextNodeId); writer.WriteBoolean(EndsConversation); }
    public static DialogueNodeDefinition ReadCanonical(CanonicalReader reader) { string id = reader.ReadRequiredString(); DialogueIntent intent = (DialogueIntent)reader.ReadInt32(); string speaker = reader.ReadRequiredString(); string text = reader.ReadRequiredString(); NarrativeConditionDefinition availability = NarrativeConditionDefinition.ReadCanonical(reader); string requiredFact = reader.ReadRequiredString(); int count = reader.ReadCount(); var choices = new DialogueChoiceDefinition[count]; for (int i = 0; i < count; i++) choices[i] = DialogueChoiceDefinition.ReadCanonical(reader); return new(id, intent, speaker, text, availability, requiredFact, choices, reader.ReadRequiredString(), reader.ReadBoolean()); }
}

public sealed record QuestKernelDefinition(
    string Id,
    int Version,
    string DisplayName,
    IReadOnlyList<string> Themes,
    IReadOnlyList<QuestSlotDefinition> Slots,
    IReadOnlyList<QuestObjectiveDefinition> Objectives,
    IReadOnlyList<QuestNodeDefinition> Nodes,
    IReadOnlyList<QuestRouteDefinition> Routes,
    IReadOnlyList<QuestRecoveryDefinition> Recoveries,
    IReadOnlyList<DialogueNodeDefinition> Dialogue,
    IReadOnlyList<NarrativeEffectDefinition> Consequences,
    IReadOnlyList<string> FollowUpHookIds) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(Id); writer.WriteInt32(Version); writer.WriteString(DisplayName); WriteStrings(writer, Themes); writer.WriteCount(Slots.Count); foreach (QuestSlotDefinition slot in Slots.OrderBy(static value => value.Id, StringComparer.Ordinal)) slot.WriteCanonical(writer); writer.WriteCount(Objectives.Count); foreach (QuestObjectiveDefinition objective in Objectives.OrderBy(static value => value.Id, StringComparer.Ordinal)) objective.WriteCanonical(writer); writer.WriteCount(Nodes.Count); foreach (QuestNodeDefinition node in Nodes.OrderBy(static value => value.Id, StringComparer.Ordinal)) node.WriteCanonical(writer); writer.WriteCount(Routes.Count); foreach (QuestRouteDefinition route in Routes.OrderBy(static value => value.Id, StringComparer.Ordinal)) route.WriteCanonical(writer); writer.WriteCount(Recoveries.Count); foreach (QuestRecoveryDefinition recovery in Recoveries.OrderBy(static value => value.Id, StringComparer.Ordinal)) recovery.WriteCanonical(writer); writer.WriteCount(Dialogue.Count); foreach (DialogueNodeDefinition dialogue in Dialogue.OrderBy(static value => value.Id, StringComparer.Ordinal)) dialogue.WriteCanonical(writer); writer.WriteCount(Consequences.Count); foreach (NarrativeEffectDefinition consequence in Consequences.OrderBy(static value => value.Id, StringComparer.Ordinal)) consequence.WriteCanonical(writer); WriteStrings(writer, FollowUpHookIds);
    }
    public static QuestKernelDefinition ReadCanonical(CanonicalReader reader)
    {
        string id = reader.ReadRequiredString(); int version = reader.ReadInt32(); string display = reader.ReadRequiredString(); string[] themes = ReadStrings(reader); int slotCount = reader.ReadCount(); var slots = new QuestSlotDefinition[slotCount]; for (int i = 0; i < slotCount; i++) slots[i] = QuestSlotDefinition.ReadCanonical(reader); int objectiveCount = reader.ReadCount(); var objectives = new QuestObjectiveDefinition[objectiveCount]; for (int i = 0; i < objectiveCount; i++) objectives[i] = QuestObjectiveDefinition.ReadCanonical(reader); int nodeCount = reader.ReadCount(); var nodes = new QuestNodeDefinition[nodeCount]; for (int i = 0; i < nodeCount; i++) nodes[i] = QuestNodeDefinition.ReadCanonical(reader); int routeCount = reader.ReadCount(); var routes = new QuestRouteDefinition[routeCount]; for (int i = 0; i < routeCount; i++) routes[i] = QuestRouteDefinition.ReadCanonical(reader); int recoveryCount = reader.ReadCount(); var recoveries = new QuestRecoveryDefinition[recoveryCount]; for (int i = 0; i < recoveryCount; i++) recoveries[i] = QuestRecoveryDefinition.ReadCanonical(reader); int dialogueCount = reader.ReadCount(); var dialogue = new DialogueNodeDefinition[dialogueCount]; for (int i = 0; i < dialogueCount; i++) dialogue[i] = DialogueNodeDefinition.ReadCanonical(reader); int consequenceCount = reader.ReadCount(); var consequences = new NarrativeEffectDefinition[consequenceCount]; for (int i = 0; i < consequenceCount; i++) consequences[i] = NarrativeEffectDefinition.ReadCanonical(reader); return new(id, version, display, themes, slots, objectives, nodes, routes, recoveries, dialogue, consequences, ReadStrings(reader));
    }
    private static void WriteStrings(CanonicalWriter writer, IReadOnlyList<string> values) { writer.WriteCount(values.Count); foreach (string value in values.Order(StringComparer.Ordinal)) writer.WriteString(value); }
    private static string[] ReadStrings(CanonicalReader reader) { int count = reader.ReadCount(); var result = new string[count]; for (int i = 0; i < count; i++) result[i] = reader.ReadRequiredString(); return result; }
}

public sealed record GeneratedQuestPlan(
    StableId Id,
    QuestKernelDefinition Kernel,
    IReadOnlyDictionary<string, StableId> BoundSlots,
    IReadOnlyDictionary<string, string> BindingReasons,
    IReadOnlyList<string> ValidationHistory,
    IReadOnlyList<string> RepairHistory,
    ulong SeedValue) : ICanonicalWritable
{
    public StableId ResolveSlot(string id) => BoundSlots.TryGetValue(id, out StableId value) ? value : StableId.Empty;
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); Kernel.WriteCanonical(writer); writer.WriteCount(BoundSlots.Count); foreach ((string key, StableId value) in BoundSlots.OrderBy(static item => item.Key, StringComparer.Ordinal)) { writer.WriteString(key); writer.WriteStableId(value); } writer.WriteCount(BindingReasons.Count); foreach ((string key, string value) in BindingReasons.OrderBy(static item => item.Key, StringComparer.Ordinal)) { writer.WriteString(key); writer.WriteString(value); } WriteStrings(writer, ValidationHistory); WriteStrings(writer, RepairHistory); writer.WriteUInt64(SeedValue); }
    public static GeneratedQuestPlan ReadCanonical(CanonicalReader reader) { StableId id = reader.ReadStableId(); QuestKernelDefinition kernel = QuestKernelDefinition.ReadCanonical(reader); int slotCount = reader.ReadCount(); var slots = new SortedDictionary<string, StableId>(StringComparer.Ordinal); for (int i = 0; i < slotCount; i++) slots.Add(reader.ReadRequiredString(), reader.ReadStableId()); int reasonCount = reader.ReadCount(); var reasons = new SortedDictionary<string, string>(StringComparer.Ordinal); for (int i = 0; i < reasonCount; i++) reasons.Add(reader.ReadRequiredString(), reader.ReadRequiredString()); return new(id, kernel, slots, reasons, ReadStrings(reader), ReadStrings(reader), reader.ReadUInt64()); }
    private static void WriteStrings(CanonicalWriter writer, IReadOnlyList<string> values) { writer.WriteCount(values.Count); foreach (string value in values) writer.WriteString(value); }
    private static string[] ReadStrings(CanonicalReader reader) { int count = reader.ReadCount(); var result = new string[count]; for (int i = 0; i < count; i++) result[i] = reader.ReadRequiredString(); return result; }
}
