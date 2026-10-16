using Roentgen.Contracts;

namespace Roentgen.Rpg;

public sealed record ProgressionCommandResult(bool Accepted, string Message, ulong Revision);

public sealed class ProgressionCommandService
{
    private readonly ProgressionRepository repository;
    private readonly PerkCatalog perks;
    private readonly ExperienceSystem experience = new();
    public ProgressionCommandService(ProgressionRepository repository, PerkCatalog perks)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.perks = perks ?? throw new ArgumentNullException(nameof(perks));
    }
    public ProgressionCommandResult SpendSkill(StableId actorId, SkillId skill, int ranks)
    {
        CharacterProgressionState state = repository.GetRequired(actorId);
        bool accepted = state.TrySpendSkillPoints(skill, ranks);
        return new(accepted, accepted ? "Skill ranks purchased." : "Skill purchase was rejected.", state.Revision);
    }
    public ProgressionCommandResult SpendAttribute(StableId actorId, AttributeId attribute)
    {
        CharacterProgressionState state = repository.GetRequired(actorId);
        bool accepted = state.TrySpendAttributePoint(attribute);
        return new(accepted, accepted ? "Attribute increased." : "Attribute increase was rejected.", state.Revision);
    }
    public ProgressionCommandResult AcquirePerk(StableId actorId, string perkId, PerkContext context)
    {
        CharacterProgressionState state = repository.GetRequired(actorId);
        bool accepted = state.TryAcquirePerk(perks.Get(perkId), perks, context);
        return new(accepted, accepted ? "Perk acquired." : "Perk acquisition was rejected.", state.Revision);
    }
    public ExperienceGrantResult GrantExperience(StableId actorId, ExperienceAward award) => experience.Grant(repository.GetRequired(actorId), award);
}
