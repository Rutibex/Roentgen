using Roentgen.Core;

namespace Roentgen.AI;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "ai",
        "AI",
        new Version(0, 10, 0),
        ["core", "entities", "navigation", "simulation", "combat", "factions"]);
}
