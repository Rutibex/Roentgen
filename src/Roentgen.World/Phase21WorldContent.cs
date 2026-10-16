using Roentgen.Content;

namespace Roentgen.World;

public sealed record Phase21WorldContentSummary(
    WorldGenerationProfile LaunchProfile,
    IReadOnlyList<RegionContentDefinition> Regions,
    IReadOnlyList<SettlementGrammarContentDefinition> SettlementGrammars,
    IReadOnlyList<StructureGrammarContentDefinition> StructureGrammars,
    IReadOnlyList<RoomMotifContentDefinition> RoomMotifs,
    IReadOnlyList<DamageProfileContentDefinition> DamageProfiles,
    string Fingerprint);

public static class Phase21WorldContent
{
    private static readonly Lazy<Phase21WorldContentSummary> Summary = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Phase21WorldContentSummary Launch => Summary.Value;

    private static Phase21WorldContentSummary Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        WorldGenerationProfile profile = WorldGenerationProfile.LaunchCampaign;
        profile.Validate();
        if (profile.WorldWidthMeters != 8192 || profile.WorldDepthMeters != 8192)
            throw new InvalidOperationException("The launch campaign must remain an 8 x 8 kilometre dense world profile.");
        return new Phase21WorldContentSummary(
            profile,
            content.Pack.Regions,
            content.Pack.SettlementGrammars,
            content.Pack.StructureGrammars,
            content.Pack.RoomMotifs,
            content.Pack.DamageProfiles,
            content.Fingerprint);
    }
}
