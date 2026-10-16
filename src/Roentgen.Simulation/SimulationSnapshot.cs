using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Simulation;

public sealed record SimulationSnapshot(
    int FormatVersion,
    CampaignSeed CampaignSeed,
    int TicksPerSecond,
    ulong CompletedTick,
    double TimeScale,
    ulong NextCommandSequence,
    ulong NextEventSequence,
    EntityRegistrySnapshot EntityRegistry,
    SimulationStateStoreSnapshot StateStore,
    IReadOnlyList<CommandEnvelope> PendingCommands,
    StateChecksum StateChecksum)
{
    public const int CurrentFormatVersion = 1;
}
