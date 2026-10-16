namespace Roentgen.Audio.MonoGame;

public sealed class PcmBlockRingBuffer
{
    private readonly byte[][] _blocks;
    private int _read;
    private int _write;

    public PcmBlockRingBuffer(int capacity, int bytesPerBlock)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (bytesPerBlock <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerBlock));
        _blocks = Enumerable.Range(0, capacity + 1).Select(_ => new byte[bytesPerBlock]).ToArray();
        BytesPerBlock = bytesPerBlock;
    }

    public int Capacity => _blocks.Length - 1;
    public int BytesPerBlock { get; }
    public int Count
    {
        get
        {
            int read = Volatile.Read(ref _read);
            int write = Volatile.Read(ref _write);
            int count = write - read;
            return count >= 0 ? count : count + _blocks.Length;
        }
    }

    public bool TryGetWriteSpan(out Span<byte> destination, out int token)
    {
        int write = Volatile.Read(ref _write);
        int next = write + 1;
        if (next == _blocks.Length) next = 0;
        if (next == Volatile.Read(ref _read))
        {
            destination = default;
            token = -1;
            return false;
        }
        destination = _blocks[write];
        token = write;
        return true;
    }

    public void CommitWrite(int token)
    {
        if (token < 0 || token >= _blocks.Length) throw new ArgumentOutOfRangeException(nameof(token));
        int next = token + 1;
        if (next == _blocks.Length) next = 0;
        Volatile.Write(ref _write, next);
    }

    public bool TryRead(out byte[] block)
    {
        int read = Volatile.Read(ref _read);
        if (read == Volatile.Read(ref _write))
        {
            block = null!;
            return false;
        }
        block = _blocks[read];
        int next = read + 1;
        if (next == _blocks.Length) next = 0;
        Volatile.Write(ref _read, next);
        return true;
    }
}
