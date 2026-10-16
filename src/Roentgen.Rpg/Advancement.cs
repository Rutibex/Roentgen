using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Rpg;

public sealed record ExperienceAward(
    StableId AwardId,
    string SourceType,
    string SourceKey,
    int Amount,
    ExperienceRepeatPolicy RepeatPolicy,
    ulong AwardedTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(AwardId);
        writer.WriteString(SourceType);
        writer.WriteString(SourceKey);
        writer.WriteInt32(Amount);
        writer.WriteInt32((int)RepeatPolicy);
        writer.WriteUInt64(AwardedTick);
    }
    public static ExperienceAward ReadCanonical(CanonicalReader reader) => new(
        reader.ReadStableId(), reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadInt32(),
        (ExperienceRepeatPolicy)reader.ReadInt32(), reader.ReadUInt64());
}

public sealed record LevelAward(int Level, int SkillPoints, int PerkPoints, int AttributePoints);
public sealed record ExperienceGrantResult(bool Accepted, int LevelsGained, IReadOnlyList<LevelAward> Awards, string Message);

public sealed class ExperienceSystem
{
    public static long ExperienceToNextLevel(int level)
    {
        if (level is < 1 or > ProgressionRules.MaximumLevel) throw new ArgumentOutOfRangeException(nameof(level));
        return 200L + 75L * level + 15L * level * level;
    }

    public ExperienceGrantResult Grant(CharacterProgressionState state, ExperienceAward award)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.AwardLedger.TryAdd(award)) return new(false, 0, [], "Experience award was already credited or invalid.");
        state.AddExperience(award.Amount);
        var awards = new List<LevelAward>();
        while (state.Level < ProgressionRules.MaximumLevel)
        {
            long threshold = ExperienceToNextLevel(state.Level);
            if (state.CurrentExperience < threshold) break;
            state.ConsumeExperienceForLevel(threshold);
            int skillPoints = 8 + state.Attributes[AttributeId.Intellect] / 2;
            int perkPoints = state.Level % 2 == 0 ? 1 : 0;
            int attributePoints = state.Level % 10 == 0 ? 1 : 0;
            state.GrantLevelAwards(skillPoints, perkPoints, attributePoints);
            awards.Add(new LevelAward(state.Level, skillPoints, perkPoints, attributePoints));
        }
        return new(true, awards.Count, awards, awards.Count == 0 ? "Experience credited." : $"Advanced {awards.Count} level(s).");
    }
}
