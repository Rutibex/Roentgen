using Roentgen.Core;

namespace Roentgen.Navigation;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "navigation",
        "Navigation",
        new Version(0, 7, 0),
        ["core", "world", "physics"]);
}
