using Roentgen.Core;

namespace Roentgen.Assets;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "assets",
        "Procedural Assets",
        new Version(0, 5, 0),
        ["core", "generation"]);
}
