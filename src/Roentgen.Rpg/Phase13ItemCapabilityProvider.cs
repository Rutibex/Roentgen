using Roentgen.Contracts;
using Roentgen.Items;

namespace Roentgen.Rpg;

public sealed class RpgItemSkillCapabilityProvider : IItemSkillCapabilityProvider
{
    private readonly ProgressionCapabilityService capabilities;
    public RpgItemSkillCapabilityProvider(ProgressionCapabilityService capabilities) => this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    public int GetEffectiveSkill(StableId actorId, SkillId skill) => capabilities.GetEffectiveSkill(actorId, skill);
}

public sealed class StartingEquipmentGrantInstantiator
{
    private readonly ItemFactory factory;
    public StartingEquipmentGrantInstantiator(ItemFactory factory) => this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    public IReadOnlyList<ItemInstanceState> Instantiate(ItemWorldState world, StableId characterId, CampaignSeed seed, StartingEquipmentGrant grant, int carryCapacityGrams)
    {
        world.EnsureInventory(characterId, carryCapacityGrams, Math.Max(10000, carryCapacityGrams / 2));
        return factory.InstantiateStartingPackage(world, characterId, seed, grant.ItemDefinitionIds);
    }
}
