using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.Headless;

public static class HeadlessCommandTypes
{
    public const string SpawnEntity = "headless.entity.spawn";
    public const string DestroyEntity = "headless.entity.destroy";
    public const string SetEntityInt64 = "headless.entity.component.int64.set";
    public const string SetGlobalInt64 = "headless.global.int64.set";
    public const string AddGlobalInt64 = "headless.global.int64.add";
}

public sealed record SpawnEntityCommand(StableId EntityId, string ArchetypeId, IReadOnlyList<string> Tags) : ISimulationCommand
{
    public string TypeId => HeadlessCommandTypes.SpawnEntity;
    public int SchemaVersion => 1;

    public void WritePayload(CanonicalWriter writer)
    {
        writer.WriteStableId(EntityId);
        writer.WriteString(ArchetypeId);
        string[] tags = Tags.OrderBy(item => item, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        writer.WriteCount(tags.Length);
        foreach (string tag in tags)
        {
            writer.WriteString(tag);
        }
    }
}

public sealed record DestroyEntityCommand(StableId EntityId) : ISimulationCommand
{
    public string TypeId => HeadlessCommandTypes.DestroyEntity;
    public int SchemaVersion => 1;
    public void WritePayload(CanonicalWriter writer) => writer.WriteStableId(EntityId);
}

public sealed record SetEntityInt64ComponentCommand(StableId EntityId, string ComponentTypeId, long Value) : ISimulationCommand
{
    public string TypeId => HeadlessCommandTypes.SetEntityInt64;
    public int SchemaVersion => 1;

    public void WritePayload(CanonicalWriter writer)
    {
        writer.WriteStableId(EntityId);
        writer.WriteString(ComponentTypeId);
        writer.WriteInt64(Value);
    }
}

public sealed record SetGlobalInt64Command(string Key, long Value) : ISimulationCommand
{
    public string TypeId => HeadlessCommandTypes.SetGlobalInt64;
    public int SchemaVersion => 1;

    public void WritePayload(CanonicalWriter writer)
    {
        writer.WriteString(Key);
        writer.WriteInt64(Value);
    }
}

public sealed record AddGlobalInt64Command(string Key, long Delta) : ISimulationCommand
{
    public string TypeId => HeadlessCommandTypes.AddGlobalInt64;
    public int SchemaVersion => 1;

    public void WritePayload(CanonicalWriter writer)
    {
        writer.WriteString(Key);
        writer.WriteInt64(Delta);
    }
}

public sealed class SpawnEntityCommandHandler(EntityRegistry entities) : ICommandHandler
{
    public string HandlerId => "headless.handler.entity.spawn";
    public string CommandTypeId => HeadlessCommandTypes.SpawnEntity;
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;

    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        StableId id = reader.ReadStableId();
        string archetypeId = reader.ReadRequiredString();
        int tagCount = reader.ReadCount();
        var tags = new List<string>(tagCount);
        for (int index = 0; index < tagCount; index++)
        {
            tags.Add(reader.ReadRequiredString());
        }

        reader.EnsureComplete();
        entities.Create(id, archetypeId, context.Tick);
        foreach (string tag in tags)
        {
            entities.AddTag(id, tag);
        }
    }
}

public sealed class DestroyEntityCommandHandler(EntityRegistry entities) : ICommandHandler
{
    public string HandlerId => "headless.handler.entity.destroy";
    public string CommandTypeId => HeadlessCommandTypes.DestroyEntity;
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;

    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        StableId id = reader.ReadStableId();
        reader.EnsureComplete();
        if (!entities.Destroy(id))
        {
            throw new InvalidOperationException($"Cannot destroy missing entity '{id}' at tick {context.Tick}.");
        }
    }
}

public sealed class SetEntityInt64CommandHandler(EntityRegistry entities) : ICommandHandler
{
    public string HandlerId => "headless.handler.entity.component.int64.set";
    public string CommandTypeId => HeadlessCommandTypes.SetEntityInt64;
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;

    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        StableId id = reader.ReadStableId();
        string componentTypeId = reader.ReadRequiredString();
        long value = reader.ReadInt64();
        reader.EnsureComplete();
        entities.UpsertComponent(id, EntityComponentState.Create(componentTypeId, 1, writer => writer.WriteInt64(value)));
    }
}

public sealed class SetGlobalInt64CommandHandler(SimulationStateStore stateStore) : ICommandHandler
{
    public string HandlerId => "headless.handler.global.int64.set";
    public string CommandTypeId => HeadlessCommandTypes.SetGlobalInt64;
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;

    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        string key = reader.ReadRequiredString();
        long value = reader.ReadInt64();
        reader.EnsureComplete();
        stateStore.SetInt64(key, value);
    }
}

public sealed class AddGlobalInt64CommandHandler(SimulationStateStore stateStore) : ICommandHandler
{
    public string HandlerId => "headless.handler.global.int64.add";
    public string CommandTypeId => HeadlessCommandTypes.AddGlobalInt64;
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;

    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        string key = reader.ReadRequiredString();
        long delta = reader.ReadInt64();
        reader.EnsureComplete();
        stateStore.SetInt64(key, checked(stateStore.GetInt64(key) + delta));
    }
}
