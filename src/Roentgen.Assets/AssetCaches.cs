using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Roentgen.Core;

namespace Roentgen.Assets;

public sealed class AssetRecipeCache<TRecipe> where TRecipe : class
{
    private readonly ConcurrentDictionary<StateChecksum, TRecipe> _entries = new();
    public int Count => _entries.Count;
    public TRecipe GetOrAdd(StateChecksum fingerprint, Func<TRecipe> factory) => _entries.GetOrAdd(fingerprint, _ => factory());
    public bool TryGet(StateChecksum fingerprint, out TRecipe? recipe) => _entries.TryGetValue(fingerprint, out recipe);
    public void Clear() => _entries.Clear();
}

public sealed class CompiledAssetCache<TAsset> where TAsset : class
{
    private readonly object _gate = new();
    private readonly Dictionary<StateChecksum, Entry> _entries = [];
    private readonly long _capacityBytes;
    private long _currentBytes;
    private long _clock;

    public CompiledAssetCache(long capacityBytes)
    {
        if (capacityBytes < 1) throw new ArgumentOutOfRangeException(nameof(capacityBytes));
        _capacityBytes = capacityBytes;
    }

    public long CurrentBytes { get { lock (_gate) return _currentBytes; } }
    public int Count { get { lock (_gate) return _entries.Count; } }

    public bool TryGet(StateChecksum key, out TAsset? asset)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out Entry entry)) { asset = null; return false; }
            entry.LastAccess = ++_clock; asset = entry.Asset; return true;
        }
    }

    public void Add(StateChecksum key, TAsset asset, long estimatedBytes)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (estimatedBytes < 1 || estimatedBytes > _capacityBytes) throw new ArgumentOutOfRangeException(nameof(estimatedBytes));
        lock (_gate)
        {
            if (_entries.Remove(key, out Entry previous)) _currentBytes -= previous.Bytes;
            _entries.Add(key, new Entry(asset, estimatedBytes, ++_clock)); _currentBytes += estimatedBytes;
            while (_currentBytes > _capacityBytes)
            {
                KeyValuePair<StateChecksum, Entry> victim = _entries.OrderBy(static pair => pair.Value.LastAccess).ThenBy(static pair => pair.Key).First();
                _entries.Remove(victim.Key); _currentBytes -= victim.Value.Bytes;
            }
        }
    }

    public void Clear() { lock (_gate) { _entries.Clear(); _currentBytes = 0; } }

    private sealed class Entry(TAsset asset, long bytes, long lastAccess)
    {
        public TAsset Asset { get; } = asset;
        public long Bytes { get; } = bytes;
        public long LastAccess { get; set; } = lastAccess;
    }
}

public sealed record AssetDiskCacheRecord(string Kind, string Id, string Fingerprint, string ContentChecksum, long PayloadBytes, string PayloadSha256);

public sealed class AssetDiskCache
{
    private readonly string _root;

    public AssetDiskCache(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public string Store(string kind, string id, StateChecksum fingerprint, StateChecksum contentChecksum, ReadOnlySpan<byte> payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind); ArgumentException.ThrowIfNullOrWhiteSpace(id);
        string safeKind = Sanitize(kind);
        if (safeKind.Length == 0) throw new ArgumentException("Cache kind contains no safe path characters.", nameof(kind));
        string folder = Path.Combine(_root, safeKind); Directory.CreateDirectory(folder);
        string stem = fingerprint.ToString(); string payloadPath = Path.Combine(folder, stem + ".bin"); string manifestPath = Path.Combine(folder, stem + ".json");
        byte[] bytes = payload.ToArray(); string payloadHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string temporaryPayload = payloadPath + ".tmp"; string temporaryManifest = manifestPath + ".tmp";
        File.WriteAllBytes(temporaryPayload, bytes);
        var record = new AssetDiskCacheRecord(kind, id, fingerprint.ToString(), contentChecksum.ToString(), bytes.LongLength, payloadHash);
        File.WriteAllText(temporaryManifest, JsonSerializer.Serialize(record, AssetRecipeJson.Options));
        File.Move(temporaryPayload, payloadPath, true); File.Move(temporaryManifest, manifestPath, true);
        return payloadPath;
    }

    public bool TryRead(string kind, StateChecksum fingerprint, out byte[] payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        string safeKind = Sanitize(kind);
        if (safeKind.Length == 0) { payload = []; return false; }
        string folder = Path.Combine(_root, safeKind); string stem = fingerprint.ToString();
        string payloadPath = Path.Combine(folder, stem + ".bin"); string manifestPath = Path.Combine(folder, stem + ".json");
        payload = [];
        if (!File.Exists(payloadPath) || !File.Exists(manifestPath)) return false;
        AssetDiskCacheRecord? record = JsonSerializer.Deserialize<AssetDiskCacheRecord>(File.ReadAllText(manifestPath), AssetRecipeJson.Options);
        if (record is null || record.Fingerprint != stem) return false;
        byte[] bytes = File.ReadAllBytes(payloadPath);
        if (bytes.LongLength != record.PayloadBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != record.PayloadSha256) return false;
        payload = bytes; return true;
    }

    private static string Sanitize(string value) => string.Concat(value.Where(static character => char.IsLetterOrDigit(character) || character is '-' or '_'));
}
