using Roentgen.Core;

namespace Roentgen.Persistence;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "persistence",
        "Persistence",
        new Version(0, 1, 0),
        ["core", "contracts", "simulation", "world", "entities", "factions", "narrative", "items", "rpg"]);
}
