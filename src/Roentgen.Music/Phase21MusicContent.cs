using Roentgen.Content;

namespace Roentgen.Music;

public sealed record Phase21MusicContentBundle(
    IReadOnlyDictionary<string, MusicBindingContentDefinition> Bindings,
    IReadOnlyDictionary<string, IReadOnlyList<MusicBindingContentDefinition>> BindingsByCategory,
    IReadOnlyDictionary<string, MusicBindingContentDefinition> BindingsBySubject,
    string Fingerprint);

public static class Phase21MusicContent
{
    private static readonly Lazy<Phase21MusicContentBundle> Bundle = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Phase21MusicContentBundle Launch => Bundle.Value;

    public static MusicBindingContentDefinition ForSubject(string subjectId) => Launch.BindingsBySubject.TryGetValue(subjectId, out MusicBindingContentDefinition? binding)
        ? binding
        : throw new KeyNotFoundException($"No Phase 21 music binding exists for '{subjectId}'.");

    private static Phase21MusicContentBundle Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        var bindings = content.Pack.MusicBindings.ToDictionary(static value => value.Id, StringComparer.Ordinal);
        var byCategory = content.Pack.MusicBindings
            .GroupBy(static value => value.Category, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<MusicBindingContentDefinition>)group.OrderBy(static value => value.SubjectId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var bySubject = content.Pack.MusicBindings.ToDictionary(static value => value.SubjectId, StringComparer.Ordinal);

        Require("region", ContentBudgets.RegionFamilies);
        Require("faction", ContentBudgets.MajorFactions);
        Require("companion", ContentBudgets.CompanionArchetypes);
        foreach (MusicBindingContentDefinition binding in bindings.Values)
        {
            if (string.IsNullOrWhiteSpace(binding.StyleId) || binding.States.Count == 0)
                throw new InvalidDataException($"Music binding '{binding.Id}' lacks style or state coverage.");
        }

        return new Phase21MusicContentBundle(bindings, byCategory, bySubject, content.Fingerprint);

        void Require(string category, int count)
        {
            int actual = byCategory.TryGetValue(category, out IReadOnlyList<MusicBindingContentDefinition>? values) ? values.Count : 0;
            if (actual != count) throw new InvalidDataException($"Music category '{category}' requires {count} bindings; found {actual}.");
        }
    }
}
