using Roentgen.Core;

namespace Roentgen.Physics;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "physics",
        "Physics",
        new Version(0, 7, 0),
        ["core", "world"]);
}
