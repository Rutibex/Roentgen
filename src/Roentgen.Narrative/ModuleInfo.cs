using Roentgen.Core;

namespace Roentgen.Narrative;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "narrative",
        "Narrative",
        new Version(0, 16, 0),
        ["core", "contracts", "rpg"]);
}
