using Roentgen.Core;

namespace Roentgen.Music;

public sealed class ModuleInfo : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "music",
        "Adaptive Music Contracts and Campaign Motifs",
        new Version(0, 19, 0),
        ["core", "audio"]);
}
