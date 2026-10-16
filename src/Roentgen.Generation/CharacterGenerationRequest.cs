using Roentgen.Contracts;

namespace Roentgen.Generation;

public sealed record CharacterGenerationRequest(
    CampaignSeed CampaignSeed,
    StableId CharacterId,
    string ArchetypeId,
    CharacterSpecies Species = CharacterSpecies.Human,
    StableId? ParentId = null,
    int? HeightMillimetersOverride = null,
    int? BuildPermilleOverride = null);
