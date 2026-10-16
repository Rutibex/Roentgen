namespace Roentgen.Core;

internal static class DeterministicComparer<T>
{
    public static IComparer<T> Default { get; } = typeof(T) == typeof(string)
        ? (IComparer<T>)(object)StringComparer.Ordinal
        : Comparer<T>.Default;
}

public sealed class DeterministicMap<TKey, TValue> where TKey : notnull
{
    private readonly SortedDictionary<TKey, TValue> _items;

    public DeterministicMap(IComparer<TKey>? comparer = null)
    {
        _items = new SortedDictionary<TKey, TValue>(comparer ?? DeterministicComparer<TKey>.Default);
    }

    public int Count => _items.Count;

    public IEnumerable<TKey> Keys => _items.Keys;

    public IEnumerable<TValue> Values => _items.Values;

    public TValue this[TKey key]
    {
        get => _items[key];
        set => _items[key] = value;
    }

    public bool Add(TKey key, TValue value) => _items.TryAdd(key, value);

    public bool Remove(TKey key) => _items.Remove(key);

    public bool ContainsKey(TKey key) => _items.ContainsKey(key);

    public bool TryGetValue(TKey key, out TValue value) => _items.TryGetValue(key, out value!);

    public IEnumerable<KeyValuePair<TKey, TValue>> Enumerate() => _items;
}

public sealed class DeterministicSet<T>
{
    private readonly SortedSet<T> _items;

    public DeterministicSet(IComparer<T>? comparer = null)
    {
        _items = new SortedSet<T>(comparer ?? DeterministicComparer<T>.Default);
    }

    public int Count => _items.Count;

    public bool Add(T value) => _items.Add(value);

    public bool Remove(T value) => _items.Remove(value);

    public bool Contains(T value) => _items.Contains(value);

    public IEnumerable<T> Enumerate() => _items;
}
