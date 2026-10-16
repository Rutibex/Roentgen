using Roentgen.Core;

namespace Roentgen.Rendering;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "rendering",
        "Rendering",
        new Version(0, 7, 0),
        ["core", "assets", "world", "entities", "animation", "diagnostics"]);
}
