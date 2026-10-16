using Roentgen.Core;

namespace Roentgen.World;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "world",
        "World",
        new Version(0, 7, 0),
        ["core", "generation", "simulation"]);
}
