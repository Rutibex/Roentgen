using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Rpg;

public sealed class FamiliarityState : ICanonicalWritable
{
    private readonly SortedDictionary<SkillId, int> progress = [];
    private readonly SortedSet<string> creditedSources = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<SkillId, int> Progress => progress;
    public bool TryGrant(SkillId skill, string sourceId, int amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (amount <= 0 || !creditedSources.Add($"{(int)skill}:{sourceId}")) return false;
        progress[skill] = Math.Clamp(progress.GetValueOrDefault(skill) + amount, 0, 5000);
        return true;
    }
    public int GetEffectiveBonus(SkillId skill) => progress.GetValueOrDefault(skill) / 1000;
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteCount(progress.Count);
        foreach ((SkillId id, int value) in progress) { writer.WriteInt32((int)id); writer.WriteInt32(value); }
        writer.WriteCount(creditedSources.Count);
        foreach (string source in creditedSources) writer.WriteString(source);
    }
    public static FamiliarityState ReadCanonical(CanonicalReader reader)
    {
        var result = new FamiliarityState();
        int progressCount = reader.ReadCount();
        for (int i = 0; i < progressCount; i++) result.progress[(SkillId)reader.ReadInt32()] = reader.ReadInt32();
        int sourceCount = reader.ReadCount();
        for (int i = 0; i < sourceCount; i++) result.creditedSources.Add(reader.ReadRequiredString());
        return result;
    }
}

public sealed class ProgressionAwardLedger : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, ExperienceAward> awards = [];
    public IReadOnlyCollection<ExperienceAward> Awards => awards.Values;
    public bool TryAdd(ExperienceAward award)
    {
        if (award.AwardId.IsEmpty || award.Amount <= 0) return false;
        if (award.RepeatPolicy != ExperienceRepeatPolicy.Repeatable && awards.ContainsKey(award.AwardId)) return false;
        awards[award.AwardId] = award;
        return true;
    }
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteCount(awards.Count);
        foreach (ExperienceAward award in awards.Values) award.WriteCanonical(writer);
    }
    public static ProgressionAwardLedger ReadCanonical(CanonicalReader reader)
    {
        var result = new ProgressionAwardLedger();
        int count = reader.ReadCount();
        for (int i = 0; i < count; i++)
        {
            ExperienceAward award = ExperienceAward.ReadCanonical(reader);
            result.awards.Add(award.AwardId, award);
        }
        return result;
    }
}

public sealed class CharacterProgressionState : ICanonicalWritable
{
    public const string ComponentTypeId = "rpg.character-progression.v1";
    public const int SchemaVersion = 1;
    private readonly SortedSet<SkillId> specializedSkills = [];
    private readonly SortedSet<string> traitIds = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> perkRanks = new(StringComparer.Ordinal);
    private readonly List<PersonalHistoryTag> history = [];

    public CharacterProgressionState(StableId characterId, AttributeSet attributes, SkillSet skills)
    {
        if (characterId.IsEmpty) throw new ArgumentException("Character ID cannot be empty.", nameof(characterId));
        CharacterId = characterId;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Skills = skills ?? throw new ArgumentNullException(nameof(skills));
        Level = 1;
    }

    public StableId CharacterId { get; }
    public int Level { get; private set; }
    public long CurrentExperience { get; private set; }
    public long LifetimeExperience { get; private set; }
    public AttributeSet Attributes { get; }
    public SkillSet Skills { get; }
    public IReadOnlySet<SkillId> SpecializedSkills => specializedSkills;
    public string BackgroundId { get; private set; } = string.Empty;
    public IReadOnlySet<string> TraitIds => traitIds;
    public IReadOnlyDictionary<string, int> PerkRanks => perkRanks;
    public IReadOnlyList<PersonalHistoryTag> History => history;
    public int UnspentSkillPoints { get; private set; }
    public int UnspentPerkPoints { get; private set; }
    public int UnspentAttributePoints { get; private set; }
    public FamiliarityState Familiarity { get; private set; } = new();
    public ProgressionAwardLedger AwardLedger { get; private set; } = new();
    public ulong Revision { get; private set; }

    public void ConfigureCreation(IEnumerable<SkillId> specializations, string backgroundId, IEnumerable<string> traits, IEnumerable<PersonalHistoryTag> personalHistory)
    {
        if (Revision != 0) throw new InvalidOperationException("Creation configuration may only be applied once.");
        specializedSkills.UnionWith(specializations);
        if (specializedSkills.Count != 3) throw new InvalidOperationException("Exactly three specialized skills are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundId);
        BackgroundId = backgroundId;
        traitIds.UnionWith(traits);
        if (traitIds.Count != 2) throw new InvalidOperationException("Exactly two traits are required.");
        history.AddRange(personalHistory.OrderBy(x => x.Id, StringComparer.Ordinal));
        Revision++;
    }

    public bool TrySpendSkillPoints(SkillId skill, int ranks)
    {
        int available = UnspentSkillPoints;
        if (!Skills.TryPurchaseRanks(skill, ranks, ref available)) return false;
        UnspentSkillPoints = available;
        Revision++;
        return true;
    }

    public bool TrySpendAttributePoint(AttributeId attribute)
    {
        if (UnspentAttributePoints <= 0 || !Attributes.TryAdjust(attribute, 1)) return false;
        UnspentAttributePoints--;
        Revision++;
        return true;
    }

    public bool TryAcquirePerk(PerkDefinition definition, PerkCatalog catalog, PerkContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(catalog);
        if (UnspentPerkPoints <= 0) return false;
        int currentRank = perkRanks.GetValueOrDefault(definition.Id);
        if (currentRank >= definition.MaximumRank || !catalog.AreRequirementsMet(definition, this, context)) return false;
        perkRanks[definition.Id] = currentRank + 1;
        UnspentPerkPoints--;
        Revision++;
        return true;
    }

    public bool HasPerk(string id, int minimumRank = 1) => perkRanks.GetValueOrDefault(id) >= minimumRank;

    public void GrantFamiliarity(SkillId skill, string sourceId, int amount)
    {
        if (Familiarity.TryGrant(skill, sourceId, amount)) Revision++;
    }

    internal void AddExperience(int amount)
    {
        CurrentExperience = checked(CurrentExperience + amount);
        LifetimeExperience = checked(LifetimeExperience + amount);
        Revision++;
    }

    internal void ConsumeExperienceForLevel(long threshold)
    {
        CurrentExperience -= threshold;
        Level++;
        Revision++;
    }

    internal void GrantLevelAwards(int skillPoints, int perkPoints, int attributePoints)
    {
        UnspentSkillPoints = checked(UnspentSkillPoints + skillPoints);
        UnspentPerkPoints = checked(UnspentPerkPoints + perkPoints);
        UnspentAttributePoints = checked(UnspentAttributePoints + attributePoints);
        Revision++;
    }

    public EntityComponentState ToEntityComponent() => EntityComponentState.Create(ComponentTypeId, SchemaVersion, WriteCanonical);

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(ComponentTypeId);
        writer.WriteInt32(SchemaVersion);
        writer.WriteStableId(CharacterId);
        writer.WriteInt32(Level);
        writer.WriteInt64(CurrentExperience);
        writer.WriteInt64(LifetimeExperience);
        Attributes.WriteCanonical(writer);
        Skills.WriteCanonical(writer);
        writer.WriteCount(specializedSkills.Count);
        foreach (SkillId id in specializedSkills) writer.WriteInt32((int)id);
        writer.WriteString(BackgroundId);
        writer.WriteCount(traitIds.Count);
        foreach (string id in traitIds) writer.WriteString(id);
        writer.WriteCount(perkRanks.Count);
        foreach ((string id, int rank) in perkRanks) { writer.WriteString(id); writer.WriteInt32(rank); }
        writer.WriteCount(history.Count);
        foreach (PersonalHistoryTag tag in history.OrderBy(x => x.Id, StringComparer.Ordinal)) { writer.WriteString(tag.Id); writer.WriteString(tag.Value); }
        writer.WriteInt32(UnspentSkillPoints);
        writer.WriteInt32(UnspentPerkPoints);
        writer.WriteInt32(UnspentAttributePoints);
        Familiarity.WriteCanonical(writer);
        AwardLedger.WriteCanonical(writer);
        writer.WriteUInt64(Revision);
    }

    public static CharacterProgressionState ReadCanonical(ReadOnlyMemory<byte> payload)
    {
        var reader = new CanonicalReader(payload);
        if (!string.Equals(reader.ReadRequiredString(), ComponentTypeId, StringComparison.Ordinal)) throw new InvalidDataException("Progression marker is invalid.");
        int schema = reader.ReadInt32();
        if (schema != SchemaVersion) throw new NotSupportedException($"Progression schema {schema} is not supported.");
        StableId id = reader.ReadStableId();
        int level = reader.ReadInt32();
        long currentXp = reader.ReadInt64();
        long lifetimeXp = reader.ReadInt64();
        var result = new CharacterProgressionState(id, AttributeSet.ReadCanonical(reader), SkillSet.ReadCanonical(reader))
        {
            Level = level,
            CurrentExperience = currentXp,
            LifetimeExperience = lifetimeXp
        };
        int specializationCount = reader.ReadCount();
        for (int i = 0; i < specializationCount; i++) result.specializedSkills.Add((SkillId)reader.ReadInt32());
        result.BackgroundId = reader.ReadRequiredString();
        int traitCount = reader.ReadCount();
        for (int i = 0; i < traitCount; i++) result.traitIds.Add(reader.ReadRequiredString());
        int perkCount = reader.ReadCount();
        for (int i = 0; i < perkCount; i++) result.perkRanks.Add(reader.ReadRequiredString(), reader.ReadInt32());
        int historyCount = reader.ReadCount();
        for (int i = 0; i < historyCount; i++) result.history.Add(new PersonalHistoryTag(reader.ReadRequiredString(), reader.ReadRequiredString()));
        result.UnspentSkillPoints = reader.ReadInt32();
        result.UnspentPerkPoints = reader.ReadInt32();
        result.UnspentAttributePoints = reader.ReadInt32();
        result.Familiarity = FamiliarityState.ReadCanonical(reader);
        result.AwardLedger = ProgressionAwardLedger.ReadCanonical(reader);
        result.Revision = reader.ReadUInt64();
        reader.EnsureComplete();
        return result;
    }
}

public sealed class ProgressionRepository
{
    private readonly SortedDictionary<StableId, CharacterProgressionState> states = [];
    public IReadOnlyCollection<CharacterProgressionState> States => states.Values;
    public void Add(CharacterProgressionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!states.TryAdd(state.CharacterId, state)) throw new InvalidOperationException($"Progression state for '{state.CharacterId}' already exists.");
    }
    public void Replace(CharacterProgressionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        states[state.CharacterId] = state;
    }
    public bool TryGet(StableId id, out CharacterProgressionState? state) => states.TryGetValue(id, out state);
    public CharacterProgressionState GetRequired(StableId id) => states.TryGetValue(id, out CharacterProgressionState? state) ? state : throw new KeyNotFoundException($"Progression state for '{id}' is not registered.");
}
