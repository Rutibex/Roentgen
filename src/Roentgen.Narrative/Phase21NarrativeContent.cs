using Roentgen.Content;

namespace Roentgen.Narrative;

public static class Phase21NarrativeContent
{
    private static readonly Lazy<IReadOnlyList<QuestKernelDefinition>> Kernels = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<QuestKernelDefinition> CreateLaunchKernels() => Kernels.Value;

    private static IReadOnlyList<QuestKernelDefinition> Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        QuestKernelDefinition[] result = content.Pack.QuestKernels
            .OrderBy(static value => value.Id, StringComparer.Ordinal)
            .Select(static value => Phase16Content.BuildLaunchKernel(value.Id, value.Name, value.Category))
            .ToArray();
        if (result.Length != ContentBudgets.QuestKernels)
            throw new InvalidOperationException($"Phase 21 requires {ContentBudgets.QuestKernels} quest kernels; found {result.Length}.");
        return result;
    }
}
