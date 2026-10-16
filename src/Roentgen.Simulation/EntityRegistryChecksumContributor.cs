using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Simulation;

public sealed class EntityRegistryChecksumContributor(EntityRegistry registry) : IStateChecksumContributor
{
    public string ContributorId => "entities.registry";

    public void WriteState(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        registry.WriteCanonical(writer);
    }
}
