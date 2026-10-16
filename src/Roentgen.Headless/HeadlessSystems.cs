using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.Headless;

public sealed class ElapsedTickSystem(SimulationStateStore stateStore) : ISimulationSystem, IPhasedSimulationSystem
{
    public string SystemId => "headless.system.elapsed-ticks";
    public int Order => 0;
    public SimulationSystemPhase Phase => SimulationSystemPhase.Finalization;

    public void Update(SimulationTickContext context) => stateStore.SetInt64("simulation.elapsed-ticks", checked((long)context.Tick));
}

public sealed class EntityPulseSystem(EntityRegistry entities) : ISimulationSystem, IPhasedSimulationSystem
{
    public string SystemId => "headless.system.entity-pulse";
    public int Order => 100;
    public SimulationSystemPhase Phase => SimulationSystemPhase.Gameplay;

    public void Update(SimulationTickContext context)
    {
        if (context.Tick % 60UL != 0UL)
        {
            return;
        }

        foreach (EntityState entity in entities.QueryByTag("simulated"))
        {
            entities.UpsertComponent(entity.Id, EntityComponentState.Create(
                "simulation.last-pulse-tick",
                1,
                writer => writer.WriteUInt64(context.Tick)));
        }
    }
}
