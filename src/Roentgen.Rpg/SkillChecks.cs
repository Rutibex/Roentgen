using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Rpg;

public sealed record SkillCheckModifier(string Id, int Value, string Explanation);
public sealed record SkillCheckSeed(ulong Root, ulong AttemptSequence) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteUInt64(Root); writer.WriteUInt64(AttemptSequence); }
}

public sealed record SkillCheckRequest(
    StableId ActorId,
    SkillId Skill,
    int Difficulty,
    bool SupportsCostlySuccess,
    string SourceId,
    SkillCheckSeed Seed,
    IReadOnlyList<SkillCheckModifier> Modifiers);

public sealed record SkillCheckResult(
    SkillId Skill,
    int BaseSkill,
    int EffectiveSkill,
    int Difficulty,
    int Margin,
    SkillCheckOutcome Outcome,
    IReadOnlyList<SkillCheckModifier> Modifiers,
    string Explanation);

public sealed class SkillCheckResolver
{
    public SkillCheckResult Resolve(CharacterProgressionState state, SkillCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        int baseSkill = state.Skills[request.Skill] + state.Familiarity.GetEffectiveBonus(request.Skill);
        SkillCheckModifier[] ordered = request.Modifiers.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        int effective = Math.Clamp(baseSkill + ordered.Sum(x => x.Value), 0, 150);
        int margin = effective - Math.Clamp(request.Difficulty, 0, 120);
        SkillCheckOutcome outcome = margin switch
        {
            >= 20 => SkillCheckOutcome.ExceptionalSuccess,
            >= 0 => SkillCheckOutcome.Success,
            >= -10 when request.SupportsCostlySuccess => SkillCheckOutcome.CostlySuccess,
            _ => SkillCheckOutcome.Failure
        };
        return new(request.Skill, baseSkill, effective, request.Difficulty, margin, outcome, ordered,
            $"{request.Skill}: {effective} against {request.Difficulty}; margin {margin}.");
    }
}
