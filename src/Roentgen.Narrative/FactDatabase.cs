using Roentgen.Contracts;

namespace Roentgen.Narrative;

public sealed class FactDatabase
{
    private readonly SortedDictionary<StableId, FactRecord> byId = [];
    private readonly Dictionary<string, SortedSet<StableId>> byPredicate = new(StringComparer.Ordinal);
    private readonly Dictionary<StableId, SortedSet<StableId>> bySubject = [];

    public IReadOnlyCollection<FactRecord> Facts => byId.Values;

    public bool TryGet(StableId id, out FactRecord? fact) => byId.TryGetValue(id, out fact);
    public FactRecord Get(StableId id) => byId.TryGetValue(id, out FactRecord? fact) ? fact : throw new KeyNotFoundException($"Unknown fact '{id}'.");

    public void Upsert(FactRecord fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (byId.TryGetValue(fact.Id, out FactRecord? previous)) RemoveIndexes(previous);
        byId[fact.Id] = fact;
        AddIndexes(fact);
    }

    public bool Remove(StableId id)
    {
        if (!byId.Remove(id, out FactRecord? fact)) return false;
        RemoveIndexes(fact);
        return true;
    }

    public IReadOnlyList<FactRecord> Query(string predicateId, StableId? subjectId = null, ulong? tick = null)
    {
        if (!byPredicate.TryGetValue(predicateId, out SortedSet<StableId>? ids)) return [];
        IEnumerable<FactRecord> query = ids.Select(id => byId[id]);
        if (subjectId.HasValue) query = query.Where(fact => fact.SubjectId == subjectId.Value);
        if (tick.HasValue) query = query.Where(fact => !fact.IsExpired(tick.Value));
        return query.OrderBy(static fact => fact.Id).ToArray();
    }

    public bool Exists(string predicateId, StableId subjectId, NarrativeValue? value = null, FactTruthState truth = FactTruthState.True, ulong tick = 0)
    {
        return Query(predicateId, subjectId, tick).Any(fact => fact.Truth == truth && (!value.HasValue || fact.Object.CompareTo(value.Value) == 0));
    }

    public void Clear()
    {
        byId.Clear(); byPredicate.Clear(); bySubject.Clear();
    }

    private void AddIndexes(FactRecord fact)
    {
        if (!byPredicate.TryGetValue(fact.PredicateId, out SortedSet<StableId>? predicateIds)) byPredicate.Add(fact.PredicateId, predicateIds = []);
        predicateIds.Add(fact.Id);
        if (!bySubject.TryGetValue(fact.SubjectId, out SortedSet<StableId>? subjectIds)) bySubject.Add(fact.SubjectId, subjectIds = []);
        subjectIds.Add(fact.Id);
    }

    private void RemoveIndexes(FactRecord fact)
    {
        if (byPredicate.TryGetValue(fact.PredicateId, out SortedSet<StableId>? predicateIds)) { predicateIds.Remove(fact.Id); if (predicateIds.Count == 0) byPredicate.Remove(fact.PredicateId); }
        if (bySubject.TryGetValue(fact.SubjectId, out SortedSet<StableId>? subjectIds)) { subjectIds.Remove(fact.Id); if (subjectIds.Count == 0) bySubject.Remove(fact.SubjectId); }
    }
}

public sealed class KnowledgeLedger
{
    private readonly SortedDictionary<StableId, KnowledgeClaim> claims = [];
    public StableId KnowerId { get; }
    public IReadOnlyCollection<KnowledgeClaim> Claims => claims.Values;

    public KnowledgeLedger(StableId knowerId) => KnowerId = knowerId;
    public void Upsert(KnowledgeClaim claim)
    {
        if (claim.KnowerId != KnowerId) throw new InvalidOperationException("Knowledge claim belongs to another knower.");
        claims[claim.Id] = claim;
    }
    public bool Knows(StableId factId, int minimumConfidencePermille = 1) => claims.Values.Any(claim => claim.FactId == factId && !claim.Contradicted && claim.ConfidencePermille >= minimumConfidencePermille);
    public IEnumerable<KnowledgeClaim> ForFact(StableId factId) => claims.Values.Where(claim => claim.FactId == factId);
    public bool Remove(StableId claimId) => claims.Remove(claimId);
}
