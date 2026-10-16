using Roentgen.Core;

namespace Roentgen.Animation;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "animation",
        "Animation",
        new Version(0, 9, 0),
        ["core", "contracts"]);
}
