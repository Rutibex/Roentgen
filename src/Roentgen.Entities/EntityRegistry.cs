using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Entities;

public sealed class EntityRegistry : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, EntityState> _entities = [];

    public int Count => _entities.Count;
    public ulong MutationVersion { get; private set; }
    public IEnumerable<EntityState> Entities => _entities.Values;

    public EntityState Create(StableId id, string archetypeId, ulong createdTick)
    {
        if (_entities.ContainsKey(id))
        {
            throw new InvalidOperationException($"Entity '{id}' already exists.");
        }

        var entity = new EntityState(id, archetypeId, createdTick);
        _entities.Add(id, entity);
        MutationVersion = checked(MutationVersion + 1UL);
        return entity;
    }

    public bool Destroy(StableId id)
    {
        bool removed = _entities.Remove(id);
        if (removed)
        {
            MutationVersion = checked(MutationVersion + 1UL);
        }

        return removed;
    }

    public EntityState GetRequired(StableId id) =>
        _entities.TryGetValue(id, out EntityState? entity)
            ? entity
            : throw new KeyNotFoundException($"Entity '{id}' does not exist.");

    public bool TryGet(StableId id, out EntityState? entity) => _entities.TryGetValue(id, out entity);

    public void SetArchetype(StableId id, string archetypeId)
    {
        GetRequired(id).SetArchetype(archetypeId);
        MutationVersion = checked(MutationVersion + 1UL);
    }

    public bool AddTag(StableId id, string tag)
    {
        bool changed = GetRequired(id).AddTag(tag);
        if (changed)
        {
            MutationVersion = checked(MutationVersion + 1UL);
        }

        return changed;
    }

    public bool RemoveTag(StableId id, string tag)
    {
        bool changed = GetRequired(id).RemoveTag(tag);
        if (changed)
        {
            MutationVersion = checked(MutationVersion + 1UL);
        }

        return changed;
    }

    public void UpsertComponent(StableId id, EntityComponentState component)
    {
        GetRequired(id).UpsertComponent(component);
        MutationVersion = checked(MutationVersion + 1UL);
    }

    public bool RemoveComponent(StableId id, string componentTypeId)
    {
        bool changed = GetRequired(id).RemoveComponent(componentTypeId);
        if (changed)
        {
            MutationVersion = checked(MutationVersion + 1UL);
        }

        return changed;
    }

    public IReadOnlyList<EntityState> QueryByTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return _entities.Values.Where(entity => entity.HasTag(tag)).ToArray();
    }

    public EntityRegistrySnapshot CaptureSnapshot()
    {
        EntitySnapshot[] entities = _entities.Values.Select(entity => new EntitySnapshot(
            entity.Id,
            entity.ArchetypeId,
            entity.CreatedTick,
            entity.Tags.ToArray(),
            entity.Components.Select(component => new ComponentSnapshot(
                component.TypeId,
                component.SchemaVersion,
                component.Payload.ToArray(),
                component.PayloadChecksum)).ToArray())).ToArray();
        return new EntityRegistrySnapshot(MutationVersion, entities);
    }

    public void Restore(EntityRegistrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _entities.Clear();
        foreach (EntitySnapshot source in snapshot.Entities.OrderBy(item => item.Id))
        {
            EntityState entity = CreateRestored(source.Id, source.ArchetypeId, source.CreatedTick);
            foreach (string tag in source.Tags.Order(StringComparer.Ordinal))
            {
                entity.AddTag(tag);
            }

            foreach (ComponentSnapshot component in source.Components.OrderBy(item => item.TypeId, StringComparer.Ordinal))
            {
                entity.UpsertComponent(EntityComponentState.Restore(
                    component.TypeId,
                    component.SchemaVersion,
                    component.Payload,
                    component.PayloadChecksum));
            }
        }

        MutationVersion = snapshot.MutationVersion;
    }

    public void Clear()
    {
        if (_entities.Count == 0)
        {
            return;
        }

        _entities.Clear();
        MutationVersion = checked(MutationVersion + 1UL);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString("roentgen.entity-registry");
        writer.WriteInt32(1);
        writer.WriteUInt64(MutationVersion);
        writer.WriteCount(_entities.Count);
        foreach (EntityState entity in _entities.Values)
        {
            entity.WriteCanonical(writer);
        }
    }

    private EntityState CreateRestored(StableId id, string archetypeId, ulong createdTick)
    {
        if (_entities.ContainsKey(id))
        {
            throw new InvalidDataException($"Snapshot contains duplicate entity '{id}'.");
        }

        var entity = new EntityState(id, archetypeId, createdTick);
        _entities.Add(id, entity);
        return entity;
    }
}
