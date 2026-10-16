namespace Roentgen.Simulation;

public sealed class SimulationServiceRegistry
{
    private readonly Dictionary<Type, object> _services = [];
    private bool _frozen;

    public int Count => _services.Count;
    public bool IsFrozen => _frozen;

    public void Register<TService>(TService service) where TService : class
    {
        ArgumentNullException.ThrowIfNull(service);
        if (_frozen)
        {
            throw new InvalidOperationException("Simulation services are frozen after host initialization.");
        }

        if (!_services.TryAdd(typeof(TService), service))
        {
            throw new InvalidOperationException($"Simulation service '{typeof(TService).FullName}' is already registered.");
        }
    }

    public TService GetRequired<TService>() where TService : class =>
        _services.TryGetValue(typeof(TService), out object? value)
            ? (TService)value
            : throw new InvalidOperationException($"Simulation service '{typeof(TService).FullName}' is not registered.");

    public bool TryGet<TService>(out TService? service) where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out object? value))
        {
            service = (TService)value;
            return true;
        }

        service = null;
        return false;
    }

    public void Freeze() => _frozen = true;
}
