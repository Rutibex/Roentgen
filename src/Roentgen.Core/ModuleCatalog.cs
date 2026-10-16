namespace Roentgen.Core;

public sealed class ModuleCatalog
{
    private readonly SortedDictionary<string, ModuleDescriptor> _modules = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ModuleDescriptor> Modules => _modules.Values;

    public void Register(IRoentgenModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        Register(module.Descriptor);
    }

    public void Register(ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.Id))
        {
            throw new ArgumentException("Module ID cannot be empty.", nameof(descriptor));
        }

        if (!_modules.TryAdd(descriptor.Id, descriptor))
        {
            throw new InvalidOperationException($"Module '{descriptor.Id}' is already registered.");
        }
    }

    public bool Contains(string moduleId) => _modules.ContainsKey(moduleId);
}
