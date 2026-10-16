namespace Roentgen.Core;

public static class DeterminismProtocol
{
    public const int CurrentVersion = 1;
    public const string StableIdAlgorithm = "sha256-stable-id-v1";
    public const string StateChecksumAlgorithm = "sha256-canonical-state-v1";
    public const string RandomAlgorithm = "pcg32-roentgen-v1";
    public const string ReplayFormat = "roentgen-replay-jsonl-v1";

    public static IReadOnlyDictionary<string, string> Describe() => new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["protocol"] = CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["stableId"] = StableIdAlgorithm,
        ["stateChecksum"] = StateChecksumAlgorithm,
        ["random"] = RandomAlgorithm,
        ["replay"] = ReplayFormat
    };
}
