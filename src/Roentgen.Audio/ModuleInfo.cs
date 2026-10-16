using Roentgen.Core;

namespace Roentgen.Audio;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "roentgen.audio",
        "Roentgen Semantic Audio",
        new Version(0, 18, 0),
        ["roentgen.core", "roentgen.diagnostics"]);
}
