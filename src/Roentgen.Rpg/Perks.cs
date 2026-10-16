using Roentgen.Contracts;

namespace Roentgen.Rpg;

public sealed record PerkRequirement(
    PerkRequirementKind Kind,
    string Id,
    int MinimumValue = 1);

public sealed record PerkEffectDefinition(
    string EffectType,
    string TargetId,
    int Value,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record PerkDefinition(
    string Id,
    string Name,
    string Description,
    string Family,
    int MaximumRank,
    PerkBalanceClass BalanceClass,
    IReadOnlyList<PerkRequirement> Requirements,
    IReadOnlyList<PerkEffectDefinition> Effects);

public sealed record PerkContext(
    IReadOnlySet<string> FactionTraining,
    IReadOnlySet<string> Discoveries);

public sealed record PerkValidationIssue(string PerkId, string Code, string Message);
public sealed record PerkValidationReport(bool IsValid, int BehaviorOrOptionPercent, IReadOnlyList<PerkValidationIssue> Issues);

public sealed class PerkCatalog
{
    private readonly SortedDictionary<string, PerkDefinition> definitions;
    private readonly HashSet<string> registeredEffectTypes;

    public PerkCatalog(IEnumerable<PerkDefinition> definitions, IEnumerable<string>? registeredEffectTypes = null)
    {
        this.definitions = new(definitions.ToDictionary(x => x.Id, StringComparer.Ordinal), StringComparer.Ordinal);
        this.registeredEffectTypes = new HashSet<string>(registeredEffectTypes ?? Phase12PerkEffectTypes.All, StringComparer.Ordinal);
    }
    public IReadOnlyCollection<PerkDefinition> All => definitions.Values;
    public PerkDefinition Get(string id) => definitions.TryGetValue(id, out PerkDefinition? perk) ? perk : throw new KeyNotFoundException($"Perk '{id}' is unknown.");

    public bool AreRequirementsMet(PerkDefinition perk, CharacterProgressionState state, PerkContext context)
    {
        foreach (PerkRequirement requirement in perk.Requirements)
        {
            bool met = requirement.Kind switch
            {
                PerkRequirementKind.Level => state.Level >= requirement.MinimumValue,
                PerkRequirementKind.Attribute => Enum.TryParse(requirement.Id, true, out AttributeId attribute) && state.Attributes[attribute] >= requirement.MinimumValue,
                PerkRequirementKind.Skill => Enum.TryParse(requirement.Id, true, out SkillId skill) && state.Skills[skill] >= requirement.MinimumValue,
                PerkRequirementKind.PriorPerk => state.HasPerk(requirement.Id, requirement.MinimumValue),
                PerkRequirementKind.Background => string.Equals(state.BackgroundId, requirement.Id, StringComparison.Ordinal),
                PerkRequirementKind.Trait => state.TraitIds.Contains(requirement.Id),
                PerkRequirementKind.FactionTraining => context.FactionTraining.Contains(requirement.Id),
                PerkRequirementKind.Discovery => context.Discoveries.Contains(requirement.Id),
                _ => false
            };
            if (!met) return false;
        }
        return true;
    }

    public IReadOnlyList<PerkEffectDefinition> GetEffects(CharacterProgressionState state, string effectType)
    {
        var result = new List<PerkEffectDefinition>();
        foreach ((string perkId, int rank) in state.PerkRanks.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!definitions.TryGetValue(perkId, out PerkDefinition? perk)) continue;
            for (int index = 0; index < rank; index++)
                result.AddRange(perk.Effects.Where(effect => string.Equals(effect.EffectType, effectType, StringComparison.Ordinal)));
        }
        return result;
    }

    public PerkValidationReport Validate()
    {
        var issues = new List<PerkValidationIssue>();
        foreach (PerkDefinition perk in definitions.Values)
        {
            if (perk.MaximumRank <= 0) issues.Add(new(perk.Id, "rank", "Maximum rank must be positive."));
            if (perk.Effects.Count == 0) issues.Add(new(perk.Id, "no-effect", "Perk has no registered behavior."));
            foreach (PerkEffectDefinition effect in perk.Effects)
                if (!registeredEffectTypes.Contains(effect.EffectType)) issues.Add(new(perk.Id, "effect", $"Effect type '{effect.EffectType}' is not registered."));
            foreach (PerkRequirement requirement in perk.Requirements.Where(x => x.Kind == PerkRequirementKind.PriorPerk))
                if (!definitions.ContainsKey(requirement.Id)) issues.Add(new(perk.Id, "requirement", $"Required perk '{requirement.Id}' does not exist."));
        }
        DetectCycles(issues);
        int behavior = definitions.Count == 0 ? 0 : definitions.Values.Count(x => x.BalanceClass != PerkBalanceClass.Numerical) * 100 / definitions.Count;
        if (behavior < 70) issues.Add(new("catalog", "balance-ratio", "At least 70% of perks must alter behavior or options."));
        return new(issues.Count == 0, behavior, issues);
    }

    private void DetectCycles(List<PerkValidationIssue> issues)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) { issues.Add(new(id, "cycle", "Perk prerequisites contain a cycle.")); return; }
            foreach (string prior in definitions[id].Requirements.Where(x => x.Kind == PerkRequirementKind.PriorPerk).Select(x => x.Id)) if (definitions.ContainsKey(prior)) Visit(prior);
            visiting.Remove(id); visited.Add(id);
        }
        foreach (string id in definitions.Keys) Visit(id);
    }
}

public static class Phase12PerkEffectTypes
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "combat.accuracy.modify", "combat.recoil.modify", "combat.action.unlock", "combat.targeting.unlock", "combat.injury.rule",
        "focus.cost.modify", "focus.capacity.modify", "focus.knowledge.unlock", "focus.queue.modify", "focus.fallback.unlock",
        "skill.effective.modify", "skill.option.unlock", "interaction.solution.unlock", "interaction.failure.transform",
        "progression.familiarity.modify", "world.discovery.reveal", "dialogue.option.unlock", "economy.option.unlock"
    };
}
