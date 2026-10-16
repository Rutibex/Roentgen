namespace Roentgen.Rendering;

public sealed class RenderThreadGuard
{
    private int? _ownerThreadId;

    public bool IsBound => _ownerThreadId.HasValue;

    public void BindCurrentThread()
    {
        int current = Environment.CurrentManagedThreadId;
        if (_ownerThreadId is int owner && owner != current)
        {
            throw new InvalidOperationException($"Render thread already belongs to thread {owner}; current thread is {current}.");
        }

        _ownerThreadId = current;
    }

    public void AssertAccess()
    {
        if (_ownerThreadId is not int owner)
        {
            throw new InvalidOperationException("Render thread has not been bound.");
        }

        int current = Environment.CurrentManagedThreadId;
        if (owner != current)
        {
            throw new InvalidOperationException($"Graphics resource access requires render thread {owner}; current thread is {current}.");
        }
    }
}
