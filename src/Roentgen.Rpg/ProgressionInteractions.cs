using Roentgen.Contracts;

namespace Roentgen.Rpg;

public sealed record ProgressionInteractionOption(
    string Id,
    SkillId Skill,
    int Difficulty,
    bool SupportsCostlySuccess,
    string RequiredToolTag,
    int TimeCostTicks,
    int NoisePermille,
    string SuccessResultId,
    string CostlySuccessResultId,
    string FailureResultId,
    IReadOnlySet<string> RequiredBehaviorTags);

public sealed record ProgressionInteractionEvaluation(
    ProgressionInteractionOption Option,
    bool Available,
    SkillCheckResult? Check,
    IReadOnlyList<string> MissingRequirements,
    string PredictedOutcome);

public sealed class ProgressionInteractionService
{
    private readonly ProgressionCapabilityService capabilities;
    private readonly ProgressionRepository repository;
    private readonly SkillCheckResolver resolver = new();

    public ProgressionInteractionService(ProgressionCapabilityService capabilities, ProgressionRepository repository)
    {
        this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ProgressionInteractionEvaluation Evaluate(
        StableId actorId,
        ProgressionInteractionOption option,
        IReadOnlySet<string> actorToolTags,
        IReadOnlyList<SkillCheckModifier> modifiers,
        SkillCheckSeed seed)
    {
        CharacterCapabilitySnapshot snapshot = capabilities.Get(actorId);
        var missing = new List<string>();
        if (!string.IsNullOrWhiteSpace(option.RequiredToolTag) && !actorToolTags.Contains(option.RequiredToolTag))
            missing.Add($"tool:{option.RequiredToolTag}");
        foreach (string behavior in option.RequiredBehaviorTags)
            if (!snapshot.BehaviorTags.Any(tag => tag.StartsWith(behavior, StringComparison.Ordinal))) missing.Add(behavior);
        if (missing.Count > 0) return new(option, false, null, missing, "Unavailable");
        CharacterProgressionState state = repository.GetRequired(actorId);
        var request = new SkillCheckRequest(actorId, option.Skill, option.Difficulty, option.SupportsCostlySuccess, option.Id, seed, modifiers);
        SkillCheckResult check = resolver.Resolve(state, request);
        string result = check.Outcome switch
        {
            SkillCheckOutcome.ExceptionalSuccess or SkillCheckOutcome.Success => option.SuccessResultId,
            SkillCheckOutcome.CostlySuccess => option.CostlySuccessResultId,
            _ => option.FailureResultId
        };
        return new(option, true, check, [], result);
    }
}
