using Roentgen.Content;

namespace Roentgen.Audio;

public sealed record Phase21AudioContentBundle(
    IReadOnlyDictionary<string, AudioProfileContentDefinition> Profiles,
    IReadOnlyDictionary<string, IReadOnlyList<AudioProfileContentDefinition>> ProfilesBySemanticEvent,
    IReadOnlyDictionary<string, int> CategoryCounts,
    string Fingerprint);

public static class Phase21AudioContent
{
    private static readonly Lazy<Phase21AudioContentBundle> Bundle = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Phase21AudioContentBundle Launch => Bundle.Value;

    public static AudioProfileContentDefinition Get(string id) => Launch.Profiles.TryGetValue(id, out AudioProfileContentDefinition? profile)
        ? profile
        : throw new KeyNotFoundException($"Unknown Phase 21 audio profile '{id}'.");

    public static IReadOnlyList<AudioProfileContentDefinition> ForSemanticEvent(string semanticEvent) =>
        Launch.ProfilesBySemanticEvent.TryGetValue(semanticEvent, out IReadOnlyList<AudioProfileContentDefinition>? profiles) ? profiles : [];

    private static Phase21AudioContentBundle Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        var profiles = content.Pack.AudioProfiles.ToDictionary(static value => value.Id, StringComparer.Ordinal);
        var byEvent = content.Pack.AudioProfiles
            .SelectMany(static profile => profile.SemanticEvents.Select(semanticEvent => (semanticEvent, profile)))
            .GroupBy(static value => value.semanticEvent, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<AudioProfileContentDefinition>)group.Select(static value => value.profile).OrderBy(static value => value.Id, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var categories = content.Pack.AudioProfiles
            .GroupBy(static value => value.Category, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        foreach (AudioProfileContentDefinition profile in profiles.Values)
        {
            if (profile.SemanticEvents.Count == 0 || profile.VariationCount < 1 || string.IsNullOrWhiteSpace(profile.QualityBudget))
                throw new InvalidDataException($"Audio profile '{profile.Id}' is incomplete.");
        }
        if (!profiles.ContainsKey("audio.interface.complete"))
            throw new InvalidDataException("The launch audio catalog requires the complete interface profile.");

        return new Phase21AudioContentBundle(profiles, byEvent, categories, content.Fingerprint);
    }
}
