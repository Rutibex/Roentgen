using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Factions;
using Roentgen.Items;

namespace Roentgen.Simulation;

public sealed class Phase15SocietyRuntime
{
    private readonly SocietySimulationEngine engine;
    private readonly Phase15SocietyEconomyBridge economyBridge = new();
    private ulong lastProcessedDay;
    public Phase15SocietyRuntime(SocietyWorldState world, CampaignSeed campaignSeed) { World = world ?? throw new ArgumentNullException(nameof(world)); engine = new SocietySimulationEngine(campaignSeed); lastProcessedDay = world.Day; }
    public SocietyWorldState World { get; private set; }
    public SocietyTickResult? LastResult { get; private set; }
    public IReadOnlyList<SocietyMarketProjection> LastMarketProjection { get; private set; } = [];
    public void UpdateFromSimulationTick(ulong simulationTick, ItemWorldState items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ulong targetDay = simulationTick / checked((ulong)BuildIdentity.SimulationTicksPerSecond * 60UL * 24UL);
        bool changed = false;
        while (lastProcessedDay < targetDay)
        {
            LastResult = engine.AdvanceDay(World);
            lastProcessedDay++;
            changed = true;
        }
        if (changed || LastMarketProjection.Count == 0)
        {
            LastMarketProjection = economyBridge.Project(World, items);
        }
    }
    public void AdvanceDebugDay(ItemWorldState items) { LastResult = engine.AdvanceDay(World); lastProcessedDay = World.Day; LastMarketProjection = economyBridge.Project(World, items); }
    public byte[] Save() => World.ToCanonicalPayload();
    public void Load(ReadOnlyMemory<byte> payload, ItemWorldState items) { World.RestoreFrom(payload); lastProcessedDay = World.Day; LastMarketProjection = economyBridge.Project(World, items); }
}
