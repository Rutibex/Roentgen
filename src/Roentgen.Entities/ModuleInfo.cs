using Roentgen.Core;

namespace Roentgen.Entities;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "entities",
        "Entities",
        new Version(0, 1, 0),
        ["core", "contracts"]);
}
