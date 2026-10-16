using Roentgen.Contracts;

namespace Roentgen.Generation;

public static class CharacterSeedDomains
{
    public static DomainSeed Root(CampaignSeed campaignSeed, StableId characterId) =>
        GenerationDomain.Derive(campaignSeed, $"character.{characterId}");

    public static DomainSeed Body(DomainSeed root) => GenerationDomain.Derive(root, "body");
    public static DomainSeed Face(DomainSeed root) => GenerationDomain.Derive(root, "face");
    public static DomainSeed Appearance(DomainSeed root) => GenerationDomain.Derive(root, "appearance");
    public static DomainSeed Clothing(DomainSeed root) => GenerationDomain.Derive(root, "clothing");
    public static DomainSeed Equipment(DomainSeed root) => GenerationDomain.Derive(root, "equipment");
    public static DomainSeed Gait(DomainSeed root) => GenerationDomain.Derive(root, "gait");
    public static DomainSeed CreatureBodyPlan(DomainSeed root) => GenerationDomain.Derive(root, "creature-body-plan");
    public static DomainSeed Repair(DomainSeed root, int attempt = 0) => GenerationDomain.Derive(root, $"repair.{attempt}");
}
