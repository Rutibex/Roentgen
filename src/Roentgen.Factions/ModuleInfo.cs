using Roentgen.Core;

namespace Roentgen.Factions;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "factions",
        "Factions and Settlements",
        new Version(0, 15, 0),
        ["core", "contracts"]);
}
