using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Narrative;

public sealed record NarrativeBindingCandidate(StableId Id, string TypeId, IReadOnlySet<string> Tags, int ThematicScore, bool Available, string Description);

public sealed class QuestBindingContext
{
    private readonly SortedDictionary<string, List<NarrativeBindingCandidate>> candidates = new(StringComparer.Ordinal);
    public void Add(NarrativeBindingCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidates.TryGetValue(candidate.TypeId, out List<NarrativeBindingCandidate>? values)) candidates.Add(candidate.TypeId, values = []);
        values.Add(candidate);
    }
    public IReadOnlyList<NarrativeBindingCandidate> GetCandidates(string typeId) => candidates.TryGetValue(typeId, out List<NarrativeBindingCandidate>? values) ? values.OrderBy(static value => value.Id).ToArray() : [];
}

public sealed record QuestBindingResult(bool Success, GeneratedQuestPlan? Plan, IReadOnlyList<string> Errors, IReadOnlyList<string> Repairs);

public sealed class QuestBinder
{
    public const int SchemaVersion = 1;

    public QuestBindingResult Bind(QuestKernelDefinition kernel, QuestBindingContext context, StableId campaignRootId, ulong seedValue)
    {
        ArgumentNullException.ThrowIfNull(kernel); ArgumentNullException.ThrowIfNull(context);
        var bindings = new SortedDictionary<string, StableId>(StringComparer.Ordinal);
        var reasons = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();
        var repairs = new List<string>();
        var used = new HashSet<StableId>();

        foreach (QuestSlotDefinition slot in kernel.Slots.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            NarrativeBindingCandidate[] available = context.GetCandidates(slot.TypeId)
                .Where(candidate => candidate.Available && (!slot.Unique || !used.Contains(candidate.Id)) && slot.Tags.All(candidate.Tags.Contains))
                .OrderByDescending(static candidate => candidate.ThematicScore)
                .ThenBy(static candidate => candidate.Id)
                .ToArray();
            if (available.Length == 0)
            {
                if (slot.Required) errors.Add($"slot.unbound:{slot.Id}:{slot.TypeId}");
                continue;
            }
            int bestScore = available[0].ThematicScore;
            NarrativeBindingCandidate[] best = available.Where(candidate => candidate.ThematicScore == bestScore).ToArray();
            int selectedIndex = DeterministicIndex(seedValue, slot.Id, best.Length);
            NarrativeBindingCandidate selected = best[selectedIndex];
            bindings.Add(slot.Id, selected.Id);
            reasons.Add(slot.Id, $"{selected.Description}; score={selected.ThematicScore}; deterministic-index={selectedIndex}/{best.Length}");
            if (slot.Unique) used.Add(selected.Id);
        }

        if (errors.Count > 0) return new QuestBindingResult(false, null, errors, repairs);
        StableId questId = StableIdFactory.CreateChild(campaignRootId, "quest", $"{kernel.Id}:{seedValue}:{SchemaVersion}");
        var plan = new GeneratedQuestPlan(questId, kernel, bindings, reasons, ["binding.valid", $"routes:{kernel.Routes.Count}"], repairs, seedValue);
        return new QuestBindingResult(true, plan, errors, repairs);
    }

    private static int DeterministicIndex(ulong seed, string slotId, int count)
    {
        if (count <= 1) return 0;
        var writer = new CanonicalWriter(); writer.WriteUInt64(seed); writer.WriteString(slotId); writer.WriteInt32(SchemaVersion);
        StateChecksum checksum = writer.ComputeChecksum(); Span<byte> bytes = stackalloc byte[StateChecksum.ByteLength]; checksum.WriteBytes(bytes);
        uint value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes[..4]);
        return checked((int)(value % (uint)count));
    }
}
