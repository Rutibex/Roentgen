using Roentgen.Content;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public sealed record Phase21SocietyContentBundle(
    IReadOnlyDictionary<string, FactionKernelDefinition> FactionKernels,
    IReadOnlyList<OrganizationContentDefinition> Organizations,
    IReadOnlyList<SettlementGenerationProfile> SettlementProfiles,
    string Fingerprint);

public static class Phase21SocietyContent
{
    private static readonly Lazy<Phase21SocietyContentBundle> Bundle = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);
    public static Phase21SocietyContentBundle Launch => Bundle.Value;

    private static Phase21SocietyContentBundle Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        var factions = content.Pack.Factions.ToDictionary(static value => value.Id, BuildFactionKernel, StringComparer.Ordinal);
        var profiles = content.Pack.SettlementGrammars.Select((value, index) => BuildSettlement(value, content.Pack.Factions[index % content.Pack.Factions.Count].Id, index)).ToArray();
        if (factions.Count != ContentBudgets.MajorFactions || content.Pack.Organizations.Count != ContentBudgets.MinorOrganizations || profiles.Length != ContentBudgets.SettlementGrammars)
            throw new InvalidOperationException("Phase 21 society content did not meet its launch budgets.");
        return new Phase21SocietyContentBundle(factions, content.Pack.Organizations, profiles, content.Fingerprint);
    }

    private static FactionKernelDefinition BuildFactionKernel(FactionContentDefinition source)
    {
        int seed = StableIndex(source.Id, 1600) - 800;
        IdeologyAxis[] axes = Enum.GetValues<IdeologyAxis>();
        var ideology = axes.Select((axis, index) => new IdeologyValue(axis, Math.Clamp(seed + index * 173 - 500, -1000, 1000))).ToArray();
        SocietyCommodity[] commodities = Enum.GetValues<SocietyCommodity>();
        var needs = source.MaterialNeeds.Select((_, index) => new FactionNeed(commodities[(StableIndex(source.Id, commodities.Length) + index) % commodities.Length], 8_000 + index * 4_000, 700 + index * 100)).ToArray();
        return new FactionKernelDefinition(
            source.Id,
            source.Name,
            source.FoundingEvent,
            source.Ideology,
            ideology,
            needs,
            Set(source.Taboos),
            new LeadershipModelDefinition(ParseLeadership(source.Leadership), source.Leadership, 180, true, Set(source.SpeechTags)),
            Set(source.EquipmentTags),
            Set(source.SettlementTags),
            Set(source.SpeechTags),
            source.Objectives.Select((objective, index) => new FactionObjectiveDefinition($"{source.Id}.objective.{index + 1}", objective, 1000 - index * 100, Set(source.MaterialNeeds))).ToArray(),
            Set(source.InternalDivisions));
    }

    private static SettlementGenerationProfile BuildSettlement(SettlementGrammarContentDefinition source, string factionId, int index) => new(
        StableIdFactory.Create("settlement", source.Id),
        source.Name,
        ParseTopology(source.Id, index),
        StableIdFactory.Create("faction", factionId),
        source.FoundingPurpose,
        source.EconomicRole,
        $"Generated launch crisis for {source.Name}",
        source.DefenseDoctrine,
        $"architecture.{source.Id}",
        Set(source.DistrictTags),
        Set(source.ServiceTags),
        source.PopulationCapacity,
        550 + index * 25);

    private static LeadershipSelectionMethod ParseLeadership(string value) => value switch
    {
        "consensus" => LeadershipSelectionMethod.Consensus,
        "election" => LeadershipSelectionMethod.Election,
        "technical-qualification" => LeadershipSelectionMethod.TechnicalQualification,
        _ => LeadershipSelectionMethod.Appointment
    };

    private static SettlementTopologyGrammar ParseTopology(string id, int index)
    {
        if (id.Contains("medical", StringComparison.Ordinal)) return SettlementTopologyGrammar.MedicalCompound;
        if (id.Contains("industrial", StringComparison.Ordinal)) return SettlementTopologyGrammar.IndustrialCampus;
        if (id.Contains("excavation", StringComparison.Ordinal)) return SettlementTopologyGrammar.Excavation;
        if (id.Contains("cliff", StringComparison.Ordinal)) return SettlementTopologyGrammar.CliffTerrace;
        if (id.Contains("flood", StringComparison.Ordinal)) return SettlementTopologyGrammar.FloodPlatform;
        if (id.Contains("agricultural", StringComparison.Ordinal)) return SettlementTopologyGrammar.DistributedAgricultural;
        return (SettlementTopologyGrammar)(index % Enum.GetValues<SettlementTopologyGrammar>().Length);
    }

    private static IReadOnlySet<string> Set(IEnumerable<string> values) => new SortedSet<string>(values, StringComparer.Ordinal);
    private static int StableIndex(string value, int modulus) { uint hash = 2166136261; foreach (char character in value) hash = (hash ^ character) * 16777619; return (int)(hash % (uint)modulus); }
}
