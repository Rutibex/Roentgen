using Roentgen.Core;

namespace Roentgen.Items;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "items",
        "Items",
        new Version(0, 13, 0),
        ["core", "contracts"]);
}
