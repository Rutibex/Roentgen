using Roentgen.Core;

namespace Roentgen.Combat;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "combat",
        "Combat",
        new Version(0, 10, 0),
        ["core", "contracts", "entities", "items", "rpg", "simulation", "physics"]);
}
