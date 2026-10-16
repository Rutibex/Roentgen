# Phase 13 — Inventory, Equipment, and Crafting

Phase 13 makes `Roentgen.Items` the authoritative owner of persistent physical objects. An item keeps one stable identity while it is stored, equipped, fired, damaged, repaired, modified, traded, dropped, looted, or reserved by Roentgen Focus.

## Delivered systems

- Immutable item definitions and deterministic generated blueprints.
- Persistent item instances, condition channels, locations, charges, component assemblies, and ammunition feeds.
- Indexed inventories, nested containment validation, equipment loadouts, stack rules, capacity calculations, and atomic revision-checked transactions.
- Six representative generated weapon families with twenty-four component definitions.
- Eight ammunition families and authoritative loaded-ammunition state.
- Layered armor capability compilation tied to real equipped item IDs.
- Twelve consumables and typed effect manifests.
- Repair plans using compatible parts and RPG skill capability providers.
- Seven crafting station categories, thirty recipes, recipe knowledge, deterministic outputs, and bounded failure.
- Twelve deterministic loot profiles.
- Four merchant archetypes, revision-bound trade quotes, and atomic trade transfer.
- Three settlement economy fixtures with production, consumption, scarcity, taxes, risk, and daily simulation.
- Combat, RPG, simulation, UI, and application adapters.
- ItemLab, schemas, samples, tests, static validators, and soak commands.

## Authority boundary

`Roentgen.Items` owns condition, ammunition, installed components, location, ownership, quantity, provenance, and reservations. Combat owns transient recoil, heat, attack timing, and action sequencing. Rendering owns GPU resources. UI sends item commands and never mutates item collections directly.
