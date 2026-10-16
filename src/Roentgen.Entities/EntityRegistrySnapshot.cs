using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Entities;

public sealed record ComponentSnapshot(
    string TypeId,
    int SchemaVersion,
    byte[] Payload,
    StateChecksum PayloadChecksum);

public sealed record EntitySnapshot(
    StableId Id,
    string ArchetypeId,
    ulong CreatedTick,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ComponentSnapshot> Components);

public sealed record EntityRegistrySnapshot(
    ulong MutationVersion,
    IReadOnlyList<EntitySnapshot> Entities);
