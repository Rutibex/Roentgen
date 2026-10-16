using System.Buffers.Binary;
using System.Security.Cryptography;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Generation;

public static class GenerationDomain
{
    public const int DerivationVersion = 1;

    public static DomainSeed Derive(CampaignSeed campaignSeed, string domain, int derivationVersion = DerivationVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        if (derivationVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(derivationVersion));
        }

        string normalizedDomain = NormalizeDomain(domain);
        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.domain-seed");
        writer.WriteInt32(derivationVersion);
        writer.WriteUInt64(campaignSeed.Value);
        writer.WriteString(normalizedDomain);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(writer.WrittenSpan, hash);
        return new DomainSeed(BinaryPrimitives.ReadUInt64BigEndian(hash[0..8]), normalizedDomain, derivationVersion);
    }

    public static DomainSeed Derive(DomainSeed parent, string childDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childDomain);
        if (parent.DerivationVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parent), "Parent domain derivation version must be positive.");
        }

        string normalizedParent = NormalizeDomain(parent.Domain);
        if (!string.Equals(normalizedParent, parent.Domain, StringComparison.Ordinal))
        {
            throw new ArgumentException("Parent domain path is not canonical.", nameof(parent));
        }

        string normalizedChild = NormalizeDomain(childDomain);
        string fullDomain = $"{parent.Domain}.{normalizedChild}";

        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.child-domain-seed");
        writer.WriteInt32(parent.DerivationVersion);
        writer.WriteUInt64(parent.Value);
        writer.WriteString(parent.Domain);
        writer.WriteString(normalizedChild);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(writer.WrittenSpan, hash);
        return new DomainSeed(BinaryPrimitives.ReadUInt64BigEndian(hash[0..8]), fullDomain, parent.DerivationVersion);
    }

    public static string NormalizeDomain(string domain)
    {
        string normalized = domain.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A generation domain cannot be empty.", nameof(domain));
        }

        foreach (char character in normalized)
        {
            bool valid = char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_';
            if (!valid)
            {
                throw new ArgumentException($"Generation domain '{domain}' contains invalid character '{character}'.", nameof(domain));
            }
        }

        return normalized;
    }
}
