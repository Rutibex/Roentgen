using Roentgen.Content;

namespace Roentgen.Campaign;

public sealed record Phase21CampaignContentBundle(
    IReadOnlyList<CampaignKernelContentDefinition> CampaignKernels,
    IReadOnlyList<EpilogueRuleContentDefinition> EpilogueRules,
    IReadOnlyDictionary<string, int> RoleDistribution,
    string Fingerprint);

public static class Phase21CampaignContent
{
    private static readonly Lazy<Phase21CampaignContentBundle> Bundle = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);
    public static Phase21CampaignContentBundle Launch => Bundle.Value;

    private static Phase21CampaignContentBundle Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        var distribution = content.Pack.CampaignKernels
            .GroupBy(static value => value.Role, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        string[] requiredRoles = ["opening-crisis", "regional-crisis", "faction-introduction", "revelation", "regional-expansion", "irreversible-choice", "counteraction", "final-convergence"];
        foreach (string role in requiredRoles)
            if (!distribution.ContainsKey(role)) throw new InvalidDataException($"Phase 21 campaign content lacks role '{role}'.");
        if (content.Pack.CampaignKernels.Count != ContentBudgets.CampaignKernels)
            throw new InvalidOperationException($"Phase 21 requires {ContentBudgets.CampaignKernels} campaign kernels.");
        return new Phase21CampaignContentBundle(content.Pack.CampaignKernels, content.Pack.EpilogueRules, distribution, content.Fingerprint);
    }
}
