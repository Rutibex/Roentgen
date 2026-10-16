using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.Combat;

public sealed record CombatAttackCommand(
    StableId ActorId,
    FixedVector3 Origin,
    FixedVector3 Direction,
    StableId IntendedTargetId,
    StableId IntendedBodyPartId) : ISimulationCommand
{
    public string TypeId => "roentgen.combat.attack";
    public int SchemaVersion => 1;
    public void WritePayload(CanonicalWriter writer)
    {
        writer.WriteStableId(ActorId);
        CombatActorState.WriteVector(writer, Origin);
        CombatActorState.WriteVector(writer, Direction);
        writer.WriteStableId(IntendedTargetId);
        writer.WriteStableId(IntendedBodyPartId);
    }
}

public sealed record CombatReloadCommand(StableId ActorId) : ISimulationCommand
{
    public string TypeId => "roentgen.combat.reload";
    public int SchemaVersion => 1;
    public void WritePayload(CanonicalWriter writer) => writer.WriteStableId(ActorId);
}

public sealed record CombatBlockCommand(StableId ActorId, bool Blocking) : ISimulationCommand
{
    public string TypeId => "roentgen.combat.block";
    public int SchemaVersion => 1;
    public void WritePayload(CanonicalWriter writer)
    {
        writer.WriteStableId(ActorId);
        writer.WriteBoolean(Blocking);
    }
}

public sealed record CombatEquipCommand(StableId ActorId, StableId WeaponId) : ISimulationCommand
{
    public string TypeId => "roentgen.combat.equip";
    public int SchemaVersion => 1;
    public void WritePayload(CanonicalWriter writer)
    {
        writer.WriteStableId(ActorId);
        writer.WriteStableId(WeaponId);
    }
}

public sealed class CombatAttackCommandHandler(CombatEncounterRuntime runtime) : ICommandHandler
{
    public string HandlerId => "combat.handler.attack";
    public string CommandTypeId => "roentgen.combat.attack";
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;
    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        StableId actorId = reader.ReadStableId();
        FixedVector3 origin = CombatActorState.ReadVector(reader);
        FixedVector3 direction = CombatActorState.ReadVector(reader);
        StableId targetId = reader.ReadStableId();
        StableId bodyPartId = reader.ReadStableId();
        reader.EnsureComplete();
        runtime.QueueAttack(actorId, origin, direction, targetId, bodyPartId);
        _ = context;
    }
}

public sealed class CombatReloadCommandHandler(CombatEncounterRuntime runtime) : ICommandHandler
{
    public string HandlerId => "combat.handler.reload";
    public string CommandTypeId => "roentgen.combat.reload";
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;
    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        runtime.QueueReload(reader.ReadStableId());
        reader.EnsureComplete();
        _ = context;
    }
}

public sealed class CombatBlockCommandHandler(CombatEncounterRuntime runtime) : ICommandHandler
{
    public string HandlerId => "combat.handler.block";
    public string CommandTypeId => "roentgen.combat.block";
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;
    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        runtime.SetBlocking(reader.ReadStableId(), reader.ReadBoolean());
        reader.EnsureComplete();
        _ = context;
    }
}

public sealed class CombatEquipCommandHandler(CombatEncounterRuntime runtime) : ICommandHandler
{
    public string HandlerId => "combat.handler.equip";
    public string CommandTypeId => "roentgen.combat.equip";
    public int MinimumSchemaVersion => 1;
    public int MaximumSchemaVersion => 1;
    public void Handle(CommandEnvelope command, SimulationTickContext context)
    {
        var reader = new CanonicalReader(command.Payload);
        runtime.Equip(reader.ReadStableId(), reader.ReadStableId());
        reader.EnsureComplete();
        _ = context;
    }
}
