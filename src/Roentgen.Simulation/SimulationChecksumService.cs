using Roentgen.Core;

namespace Roentgen.Simulation;

public interface IStateChecksumContributor
{
    string ContributorId { get; }
    void WriteState(CanonicalWriter writer);
}

public sealed class SimulationChecksumService
{
    private readonly SortedDictionary<string, IStateChecksumContributor> _contributors = new(StringComparer.Ordinal);

    public int ContributorCount => _contributors.Count;

    public void Register(IStateChecksumContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributor.ContributorId);
        if (!_contributors.TryAdd(contributor.ContributorId, contributor))
        {
            throw new InvalidOperationException($"Checksum contributor '{contributor.ContributorId}' is already registered.");
        }
    }

    public StateChecksum Compute(ulong tick)
    {
        var writer = new CanonicalWriter();
        writer.WriteString("roentgen.simulation-state");
        writer.WriteInt32(1);
        writer.WriteUInt64(tick);
        writer.WriteCount(_contributors.Count);
        foreach ((string contributorId, IStateChecksumContributor contributor) in _contributors)
        {
            writer.WriteString(contributorId);
            var section = new CanonicalWriter();
            contributor.WriteState(section);
            writer.WriteBytes(section.WrittenSpan);
        }

        return writer.ComputeChecksum();
    }
}
