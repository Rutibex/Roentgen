using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.Headless;

public static class HeadlessSimulationFactory
{
    public static SimulationHost Create(
        CampaignSeed campaignSeed,
        int ticksPerSecond = 60,
        int checksumInterval = 0,
        ISimulationTelemetrySink? telemetry = null)
    {
        var entities = new EntityRegistry();
        var stateStore = new SimulationStateStore();
        var commands = new DeterministicCommandQueue();
        var sequences = new CommandSequenceAllocator();
        var router = new CommandRouter();
        var events = new DeterministicEventBus();
        var checksums = new SimulationChecksumService();
        var services = new SimulationServiceRegistry();

        services.Register(entities);
        services.Register(stateStore);
        services.Register(commands);
        services.Register(router);
        services.Register(events);

        checksums.Register(new EntityRegistryChecksumContributor(entities));
        checksums.Register(stateStore);

        router.Register(new SpawnEntityCommandHandler(entities));
        router.Register(new DestroyEntityCommandHandler(entities));
        router.Register(new SetEntityInt64CommandHandler(entities));
        router.Register(new SetGlobalInt64CommandHandler(stateStore));
        router.Register(new AddGlobalInt64CommandHandler(stateStore));

        var host = new SimulationHost(
            campaignSeed,
            new SimulationHostOptions(ticksPerSecond, checksumInterval),
            entities,
            stateStore,
            commands,
            sequences,
            router,
            events,
            checksums,
            services,
            telemetry);
        host.RegisterSystem(new EntityPulseSystem(entities));
        host.RegisterSystem(new ElapsedTickSystem(stateStore));
        return host;
    }

    public static IReadOnlyList<CommandEnvelope> ScheduleSmokeScenario(SimulationHost host, int entityCount = 8, ulong maximumTick = ulong.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (entityCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entityCount));
        }

        StableId issuer = StableIdFactory.CreateCampaignRoot(host.CampaignSeed);
        var commands = new List<CommandEnvelope>();
        for (int index = 0; index < entityCount; index++)
        {
            StableId entityId = StableIdFactory.CreateChild(issuer, "headless-smoke-entity", index);
            commands.Add(host.Enqueue(
                issuer,
                1,
                CommandOriginPriority.Engine,
                new SpawnEntityCommand(entityId, index % 2 == 0 ? "test.humanoid" : "test.machine", ["simulated", "smoke"] )));
            if (maximumTick >= 2UL)
            {
                commands.Add(host.Enqueue(
                    issuer,
                    2,
                    CommandOriginPriority.Engine,
                    new SetEntityInt64ComponentCommand(entityId, "rpg.health", 100 + index)));
            }
        }

        if (maximumTick >= 5UL)
        {
            commands.Add(host.Enqueue(issuer, 5, CommandOriginPriority.Engine, new SetGlobalInt64Command("scenario.stage", 1)));
        }

        if (maximumTick >= 30UL)
        {
            commands.Add(host.Enqueue(issuer, 30, CommandOriginPriority.Engine, new AddGlobalInt64Command("scenario.stage", 2)));
        }

        if (entityCount > 0 && maximumTick >= 120UL)
        {
            StableId finalEntity = StableIdFactory.CreateChild(issuer, "headless-smoke-entity", entityCount - 1);
            commands.Add(host.Enqueue(issuer, 120, CommandOriginPriority.Engine, new DestroyEntityCommand(finalEntity)));
        }

        return commands;
    }
}
