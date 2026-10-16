# Phase 15 — Factions and Settlements

Phase 15 adds the authoritative strategic society layer above Phase 14's actor simulation and Phase 13's physical item economy.

## Authority

`SocietyWorldState` is the single checksummed authority for factions, settlements, directed relationships, territory claims, leadership, population cohorts, services, resources, trade routes and shipments, reputation evidence, crimes, disguises, and strategic conflicts. Persistent political references use `StableId`; string matching is retained only in explicit legacy conversion helpers.

The domain is split into:

1. authored kernels and definitions;
2. immutable generated campaign plans;
3. mutable runtime state.

`Roentgen.Factions` depends only on `Roentgen.Core` and `Roentgen.Contracts`. Simulation advances the state, Items receives market projections, Persistence stores the canonical payload, UI builds presentation models, and the application composes them.

## Generated regression society

The permanent micro-campaign contains three deterministic factions and settlements:

- Prospect Clinic Compact / Prospect Clinic;
- Blackwater Recovery Authority / Blackwater Intake;
- Red Quarry Cooperative / Red Quarry Crossing.

Each has ideology axes, needs, taboos, leadership rules, divisions, objectives, equipment and architecture identities, settlement topology, population cohorts, service capacity, production and consumption, territory, directed diplomacy, and bilateral trade.

## Strategic day

The fixed and versioned daily order is:

1. resolve shipments;
2. operate services;
3. apply production and consumption;
4. dispatch conservative shipments;
5. update population and migration pressure;
6. resolve succession;
7. advance strategic conflicts;
8. process witnessed crimes and warrants;
9. apply reputation evidence once;
10. advance the society day and checksum.

Strategic simulation never runs at the 60 Hz gameplay rate. The application maps simulation time to strategic days and only reprojects markets when the strategic state changes.

## Trade conservation

A shipment deducts its loaded quantity from the exporter at departure. The importer receives only the delivered quantity after recorded route loss. Shipment IDs include route and departure day, preventing duplicate dispatch during simulation-tier promotion or save/load.

## Reputation, crimes, and disguise

Reputation is a vector of trust, fear, respect, obligation, ideological alignment, known crime severity, and public familiarity. Changes originate from evidence records with a source, reliability, publicity, day, and reason.

Crimes remain unreported until witnesses or evidence exist. Processing can produce a report, warrant, fine, and reputation evidence. The state does not provide factions with omniscient knowledge.

Disguise evaluation is observer-specific. It considers quality, inspection skill, observer familiarity, exposure evidence, credentials, equipment, and identity coverage, then records the observer's believed faction and confidence.

## Strategic conflict

Conflicts move through a bounded ladder from dispute to pressure, embargo, sabotage, raids, border fighting, open conflict, siege or blockade, ceasefire, and settlement. High player relevance reserves a conflict for a playable encounter instead of resolving it invisibly.

## Integration

- `Phase15SocietyRuntime` advances the society and projects settlement policy into Phase 13 markets.
- `Phase15SocietyEconomyBridge` keeps item pricing and merchant policy subordinate to the society authority.
- `SocietyWorldSnapshotSerializer` adds checksummed atomic persistence.
- `Phase15SocietyScreenBuilder` exposes faction, settlement, reputation, territory, conflict, route, and warrant presentation models.
- `Roentgen.FactionLab` provides deterministic generation, inspection, simulation, mutation, validation, repair, export, and replay commands.
