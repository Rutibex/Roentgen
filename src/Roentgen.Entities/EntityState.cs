using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Entities;

public sealed class EntityState : ICanonicalWritable
{
    private readonly SortedSet<string> _tags = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, EntityComponentState> _components = new(StringComparer.Ordinal);

    internal EntityState(StableId id, string archetypeId, ulong createdTick)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Entity ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(archetypeId);
        Id = id;
        ArchetypeId = archetypeId;
        CreatedTick = createdTick;
    }

    public StableId Id { get; }
    public string ArchetypeId { get; private set; }
    public ulong CreatedTick { get; }
    public IReadOnlyCollection<string> Tags => _tags;
    public IReadOnlyCollection<EntityComponentState> Components => _components.Values;

    public bool HasTag(string tag) => _tags.Contains(tag);

    public bool TryGetComponent(string typeId, out EntityComponentState? component) =>
        _components.TryGetValue(typeId, out component);

    internal bool AddTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return _tags.Add(tag.Normalize(System.Text.NormalizationForm.FormC));
    }

    internal bool RemoveTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return _tags.Remove(tag.Normalize(System.Text.NormalizationForm.FormC));
    }

    internal void SetArchetype(string archetypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archetypeId);
        ArchetypeId = archetypeId.Normalize(System.Text.NormalizationForm.FormC);
    }

    internal void UpsertComponent(EntityComponentState component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _components[component.TypeId] = component;
    }

    internal bool RemoveComponent(string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        return _components.Remove(typeId);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStableId(Id);
        writer.WriteString(ArchetypeId);
        writer.WriteUInt64(CreatedTick);
        writer.WriteCount(_tags.Count);
        foreach (string tag in _tags)
        {
            writer.WriteString(tag);
        }

        writer.WriteCount(_components.Count);
        foreach (EntityComponentState component in _components.Values)
        {
            component.WriteCanonical(writer);
        }
    }
}
