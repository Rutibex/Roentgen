namespace Roentgen.Core;

public sealed record ModuleDescriptor(
    string Id,
    string DisplayName,
    Version Version,
    IReadOnlyList<string> Dependencies);

public interface IRoentgenModule
{
    ModuleDescriptor Descriptor { get; }
}
