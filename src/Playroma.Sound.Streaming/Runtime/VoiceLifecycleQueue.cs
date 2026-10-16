namespace Playroma.Sound.Streaming.Runtime;

internal sealed class VoiceLifecycleQueue
{
    private readonly VoiceLifecycleEvent[] _items;
    private int _read;
    private int _write;

    public VoiceLifecycleQueue(int capacity)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new VoiceLifecycleEvent[capacity + 1];
    }

    public bool TryEnqueue(VoiceLifecycleEvent item)
    {
        int write = Volatile.Read(ref _write);
        int next = write + 1;
        if (next == _items.Length) next = 0;
        if (next == Volatile.Read(ref _read)) return false;
        _items[write] = item;
        Volatile.Write(ref _write, next);
        return true;
    }

    public bool TryDequeue(out VoiceLifecycleEvent item)
    {
        int read = Volatile.Read(ref _read);
        if (read == Volatile.Read(ref _write))
        {
            item = default;
            return false;
        }
        item = _items[read];
        _items[read] = default;
        int next = read + 1;
        if (next == _items.Length) next = 0;
        Volatile.Write(ref _read, next);
        return true;
    }

    public void Clear()
    {
        Array.Clear(_items);
        Volatile.Write(ref _read, 0);
        Volatile.Write(ref _write, 0);
    }
}
