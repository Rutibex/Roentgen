using Roentgen.Core;

namespace Roentgen.UI;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "ui",
        "UI",
        new Version(0, 1, 0),
        ["core", "contracts", "simulation", "rpg", "items", "narrative", "factions", "persistence"]);
}
