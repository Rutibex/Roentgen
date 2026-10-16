using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.Combat;

public sealed class CombatSimulationSystem(CombatEncounterRuntime runtime) : ISimulationSystem, IPhasedSimulationSystem
{
    public string SystemId => "combat.system.realtime";
    public int Order => 100;
    public SimulationSystemPhase Phase => SimulationSystemPhase.Gameplay;

    public void Update(SimulationTickContext context)
    {
        runtime.Update(context.Tick);
        foreach (CombatFeedbackEntry entry in runtime.State.Feedback.Where(value => value.Tick == context.Tick))
        {
            context.Events.Publish(entry.ActorId, context.Tick, new CombatFeedbackEvent(entry));
        }
    }
}

public sealed class CombatEntitySynchronizationSystem(CombatEncounterRuntime runtime, EntityRegistry entities) : ISimulationSystem, IPhasedSimulationSystem
{
    public string SystemId => "combat.system.entity-sync";
    public int Order => 300;
    public SimulationSystemPhase Phase => SimulationSystemPhase.PostSimulation;

    public void Update(SimulationTickContext context)
    {
        foreach (CombatActorState actor in runtime.State.Actors.OrderBy(static value => value.Id))
        {
            if (!entities.TryGet(actor.Id, out EntityState? entity) || entity is null)
            {
                entities.Create(actor.Id, actor.Presentation.Appearance.ArchetypeId, context.Tick);
                entities.AddTag(actor.Id, "combat-actor");
            }
            entities.UpsertComponent(actor.Id, EntityComponentState.Create("combat.actor-state", 1, actor.WriteCanonical));
        }
        foreach (WeaponRuntimeState weapon in runtime.State.Weapons.OrderBy(static value => value.Id))
        {
            if (!entities.TryGet(weapon.Id, out EntityState? entity) || entity is null)
            {
                entities.Create(weapon.Id, weapon.DefinitionId, context.Tick);
                entities.AddTag(weapon.Id, "combat-weapon");
            }
            entities.UpsertComponent(weapon.Id, EntityComponentState.Create("combat.weapon-state", 1, weapon.WriteCanonical));
        }
    }
}

public sealed class CombatPersistenceSystem(CombatEncounterRuntime runtime, SimulationStateStore stateStore) : ISimulationSystem, IPhasedSimulationSystem, ISimulationRestoreAware
{
    public string SystemId => "combat.system.persistence";
    public int Order => 900;
    public SimulationSystemPhase Phase => SimulationSystemPhase.Finalization;
    public void Update(SimulationTickContext context)
    {
        runtime.PersistTo(stateStore);
        _ = context;
    }
    public void OnSimulationRestored(SimulationInitializationContext context)
    {
        runtime.RestoreFrom(stateStore);
        _ = context;
    }
}

public static class CombatSimulationInstaller
{
    public static void Install(SimulationHost host, CombatEncounterRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(runtime);
        if (host.IsInitialized) throw new InvalidOperationException("Combat must be installed before simulation initialization.");
        host.Services.Register(runtime);
        host.CommandRouter.Register(new CombatAttackCommandHandler(runtime));
        host.CommandRouter.Register(new CombatReloadCommandHandler(runtime));
        host.CommandRouter.Register(new CombatBlockCommandHandler(runtime));
        host.CommandRouter.Register(new CombatEquipCommandHandler(runtime));
        runtime.PersistTo(host.StateStore);
        host.RegisterSystem(new CombatSimulationSystem(runtime));
        host.RegisterSystem(new CombatEntitySynchronizationSystem(runtime, host.Entities));
        host.RegisterSystem(new CombatPersistenceSystem(runtime, host.StateStore));
    }
}
