using System.Buffers.Binary;
using System.Security.Cryptography;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Generation;

public readonly record struct RandomStreamSnapshot(
    string AlgorithmId,
    DomainSeed DomainSeed,
    string StreamName,
    ulong State,
    ulong Increment,
    ulong ValuesGenerated);

public interface IDeterministicRandom
{
    string AlgorithmId { get; }
    ulong ValuesGenerated { get; }
    uint NextUInt32();
    ulong NextUInt64();
    int NextInt32(int exclusiveMaximum);
    int NextInt32(int inclusiveMinimum, int exclusiveMaximum);
    bool NextBoolean();
    float NextSingle();
    double NextDouble();
    void NextBytes(Span<byte> destination);
    RandomStreamSnapshot Capture();
}

public sealed class Pcg32Random : IDeterministicRandom
{
    public const string CurrentAlgorithmId = "pcg32-roentgen-v1";
    private const ulong Multiplier = 6364136223846793005UL;

    private readonly DomainSeed _domainSeed;
    private readonly string _streamName;
    private ulong _state;
    private readonly ulong _increment;

    internal Pcg32Random(DomainSeed domainSeed, string streamName, ulong seed, ulong sequence)
    {
        _domainSeed = domainSeed;
        _streamName = streamName;
        _increment = unchecked((sequence << 1) | 1UL);
        _state = 0UL;
        NextUInt32Core();
        _state = unchecked(_state + seed);
        NextUInt32Core();
        ValuesGenerated = 0UL;
    }

    private Pcg32Random(RandomStreamSnapshot snapshot)
    {
        if (!string.Equals(snapshot.AlgorithmId, CurrentAlgorithmId, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Random algorithm '{snapshot.AlgorithmId}' is not supported.");
        }

        if ((snapshot.Increment & 1UL) == 0UL)
        {
            throw new InvalidDataException("A PCG stream increment must be odd.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.StreamName);
        string normalizedStream = GenerationDomain.NormalizeDomain(snapshot.StreamName);
        if (!string.Equals(normalizedStream, snapshot.StreamName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A restored random stream name must already be canonical.");
        }

        string normalizedDomain = GenerationDomain.NormalizeDomain(snapshot.DomainSeed.Domain);
        if (!string.Equals(normalizedDomain, snapshot.DomainSeed.Domain, StringComparison.Ordinal) || snapshot.DomainSeed.DerivationVersion <= 0)
        {
            throw new InvalidDataException("A restored random domain is not canonical.");
        }

        _domainSeed = snapshot.DomainSeed;
        _streamName = snapshot.StreamName;
        _state = snapshot.State;
        _increment = snapshot.Increment;
        ValuesGenerated = snapshot.ValuesGenerated;
    }

    public string AlgorithmId => CurrentAlgorithmId;

    public ulong ValuesGenerated { get; private set; }

    public uint NextUInt32()
    {
        uint value = NextUInt32Core();
        ValuesGenerated++;
        return value;
    }

    public ulong NextUInt64() => ((ulong)NextUInt32() << 32) | NextUInt32();

    public int NextInt32(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }

        uint bound = (uint)exclusiveMaximum;
        uint threshold = unchecked(0U - bound) % bound;
        uint value;
        do
        {
            value = NextUInt32();
        }
        while (value < threshold);

        return (int)(value % bound);
    }

    public int NextInt32(int inclusiveMinimum, int exclusiveMaximum)
    {
        if (inclusiveMinimum >= exclusiveMaximum)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }

        long width = (long)exclusiveMaximum - inclusiveMinimum;
        if (width > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum), "The requested range is too wide.");
        }

        return inclusiveMinimum + NextInt32((int)width);
    }

    public bool NextBoolean() => (NextUInt32() & 1U) != 0U;

    public float NextSingle() => (NextUInt32() >> 8) * (1.0f / 16_777_216.0f);

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9_007_199_254_740_992.0);

    public void NextBytes(Span<byte> destination)
    {
        int offset = 0;
        while (destination.Length - offset >= sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], NextUInt32());
            offset += sizeof(uint);
        }

        if (offset < destination.Length)
        {
            uint tail = NextUInt32();
            for (int index = offset; index < destination.Length; index++)
            {
                destination[index] = (byte)tail;
                tail >>= 8;
            }
        }
    }

    public void Shuffle<T>(Span<T> values)
    {
        for (int index = values.Length - 1; index > 0; index--)
        {
            int other = NextInt32(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    public Pcg32Random Fork(string childStreamName) => RandomStreamFactory.Create(_domainSeed, $"{_streamName}/{childStreamName}");

    public RandomStreamSnapshot Capture() => new(AlgorithmId, _domainSeed, _streamName, _state, _increment, ValuesGenerated);

    public static Pcg32Random Restore(RandomStreamSnapshot snapshot) => new(snapshot);

    private uint NextUInt32Core()
    {
        ulong oldState = _state;
        _state = unchecked(oldState * Multiplier + _increment);
        uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rotation = (int)(oldState >> 59);
        return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
    }
}

public static class RandomStreamFactory
{
    public const int StreamDerivationVersion = 1;

    public static Pcg32Random Create(DomainSeed domainSeed, string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        if (domainSeed.DerivationVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(domainSeed), "Domain derivation version must be positive.");
        }

        string normalizedDomain = GenerationDomain.NormalizeDomain(domainSeed.Domain);
        if (!string.Equals(normalizedDomain, domainSeed.Domain, StringComparison.Ordinal))
        {
            throw new ArgumentException("Domain path must already be canonical.", nameof(domainSeed));
        }

        string normalized = GenerationDomain.NormalizeDomain(streamName.Replace('/', '.'));

        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.random-stream");
        writer.WriteInt32(StreamDerivationVersion);
        writer.WriteString(Pcg32Random.CurrentAlgorithmId);
        writer.WriteUInt64(domainSeed.Value);
        writer.WriteString(domainSeed.Domain);
        writer.WriteInt32(domainSeed.DerivationVersion);
        writer.WriteString(normalized);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(writer.WrittenSpan, hash);
        ulong seed = BinaryPrimitives.ReadUInt64BigEndian(hash[0..8]);
        ulong sequence = BinaryPrimitives.ReadUInt64BigEndian(hash[8..16]);
        return new Pcg32Random(domainSeed, normalized, seed, sequence);
    }
}
