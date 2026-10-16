using Roentgen.Core;

namespace Roentgen.Diagnostics;

public sealed record DeterminismMismatch(
    ulong Tick,
    StateChecksum Expected,
    StateChecksum Actual,
    string? Context = null);

public sealed record DeterminismComparisonResult(
    bool IsMatch,
    IReadOnlyList<DeterminismMismatch> Mismatches);

public static class DeterminismDiagnostics
{
    public static DeterminismComparisonResult Compare(
        IEnumerable<(ulong Tick, StateChecksum Checksum)> expected,
        IEnumerable<(ulong Tick, StateChecksum Checksum)> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Dictionary<ulong, StateChecksum> expectedByTick = expected.ToDictionary(item => item.Tick, item => item.Checksum);
        Dictionary<ulong, StateChecksum> actualByTick = actual.ToDictionary(item => item.Tick, item => item.Checksum);
        var mismatches = new List<DeterminismMismatch>();

        foreach (ulong tick in expectedByTick.Keys.Union(actualByTick.Keys).Order())
        {
            bool hasExpected = expectedByTick.TryGetValue(tick, out StateChecksum expectedChecksum);
            bool hasActual = actualByTick.TryGetValue(tick, out StateChecksum actualChecksum);
            if (!hasExpected || !hasActual || expectedChecksum != actualChecksum)
            {
                mismatches.Add(new DeterminismMismatch(
                    tick,
                    hasExpected ? expectedChecksum : StateChecksum.Zero,
                    hasActual ? actualChecksum : StateChecksum.Zero,
                    !hasExpected ? "Missing expected checkpoint." : !hasActual ? "Missing actual checkpoint." : null));
            }
        }

        return new DeterminismComparisonResult(mismatches.Count == 0, mismatches);
    }
}
