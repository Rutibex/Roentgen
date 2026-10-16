namespace Roentgen.Music.Playroma;

internal sealed class FixedSpscQueue<T> where T : class
{
    private readonly T?[] _items;
    private int _read;
    private int _write;

    public FixedSpscQueue(int capacity)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new T[capacity + 1];
    }

    public int Capacity => _items.Length - 1;
    public int Count
    {
        get
        {
            int write = Volatile.Read(ref _write);
            int read = Volatile.Read(ref _read);
            return write >= read ? write - read : _items.Length - read + write;
        }
    }

    public bool TryEnqueue(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        int write = _write;
        int next = (write + 1) % _items.Length;
        if (next == Volatile.Read(ref _read)) return false;
        _items[write] = item;
        Volatile.Write(ref _write, next);
        return true;
    }

    public bool TryDequeue(out T? item)
    {
        int read = _read;
        if (read == Volatile.Read(ref _write)) { item = null; return false; }
        item = _items[read];
        _items[read] = null;
        Volatile.Write(ref _read, (read + 1) % _items.Length);
        return item is not null;
    }
}
