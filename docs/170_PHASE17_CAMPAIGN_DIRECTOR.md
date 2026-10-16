# Phase 17 — Campaign Director

Phase 17 converts Roentgen's generated world, factions, facts, and quest kernels into a finite campaign with a deterministic opening, escalation, revelations, irreversible choices, final convergence, ending, and epilogue.

## Delivered architecture

- `Roentgen.Campaign` is a presentation-neutral runtime project.
- `CampaignGenerator` produces an immutable `GeneratedCampaignPlan` from the campaign seed, authoritative society, Phase 16 binding catalog, and optional regional topology.
- `CampaignWorldState` owns mutable campaign progress and canonical checksums.
- `CampaignDirector` offers milestones, observes quest resolution, records commitments, proves revelations, resolves convergence, selects endings, and generates epilogues.
- `Phase17CampaignRuntime` exposes the director to fixed-step simulation without making campaign code depend on MonoGame.
- `CampaignSnapshotV17Serializer` stores item, society, narrative, and campaign sections with per-section and global SHA-256 checksums.
- `CampaignAutoAgent` validates complete campaigns across the six established regression builds.
- `Roentgen.CampaignLab` exposes generation, inspection, validation, solving, failure injection, and ending analysis.

## Authority boundaries

The campaign director does not directly mutate rendering, UI, audio, or MonoGame state. Quests remain owned by `Roentgen.Narrative`; political state remains owned by `Roentgen.Factions`; the campaign layer coordinates those systems through stable IDs and typed state transitions.

## Determinism

Every generated campaign is derived from a named `DomainSeed`. Selection ties are resolved through versioned PCG streams. Generated plans, runtime states, commitments, replacement records, events, endings, and epilogues have canonical ordering and checksums.

## Campaign shape

The Phase 17 launch skeleton contains twelve ordered semantic roles:

1. Opening contradiction.
2. First regional crisis.
3. Faction introduction.
4. First revelation.
5. Expansion into additional regions.
6. Faction realignment.
7. First irreversible alignment.
8. Counteraction.
9. Second revelation.
10. Second irreversible alignment.
11. Final convergence and final revelation.
12. Ending and epilogue.

This is a generated DAG contract, not authored scene content. Each milestone binds a Phase 16 quest plan and records its region, factions, location, critical roles, recovery policy, and temporal window.

## Exit gate

Phase 17 is accepted when generation is deterministic, the milestone graph validates, all six regression builds reach an ending, critical-role removal has a valid deterministic replacement or aftermath route, save/load preserves checksums, and static repository validation passes. Native .NET build and xUnit execution remain authoritative workstation/CI checks.
