using Roentgen.Core;

namespace Roentgen.Rpg;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "rpg",
        "Character Progression",
        new Version(0, 12, 0),
        ["core", "contracts", "entities", "items"]);
}
