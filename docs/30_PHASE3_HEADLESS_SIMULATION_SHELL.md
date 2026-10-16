# Phase 3 — Headless Simulation Shell

## Status

Implemented repository baseline for the authoritative non-graphical Roentgen runtime.

## Purpose

Phase 3 makes the simulation independently executable before rendering, physics, world generation, or user-interface production begins. `Roentgen.Headless` is the reference host for deterministic commands, simulation systems, entity state, snapshots, replays, soak tests, and performance telemetry.

MonoGame is not referenced by the headless project or any authoritative simulation project.

## Delivered architecture

### `Roentgen.Entities`

- Stable-ID ordered `EntityRegistry`.
- Entity archetypes, tags, and opaque versioned component payloads.
- Canonical component payload checksums.
- Deterministic queries and canonical registry serialization.
- Complete capture and restore snapshots.

### `Roentgen.Simulation`

- `SimulationHost` composition and lifecycle.
- Fixed-step manual headless stepping.
- Named simulation-system phases.
- Deterministic phase/order/system-ID scheduling.
- Frozen service registry after initialization.
- Canonical global state store.
- Entity and global-state checksum contributors.
- Pending-command and sequence-state snapshot support.
- Tick telemetry contracts.

### `Roentgen.Persistence`

- Binary simulation snapshot format `RGSNAP01`.
- SHA-256 whole-payload integrity protection.
- Canonical validation of entities, components, state values, commands, and ordering.
- Exact continuation from a tick boundary.

### `Roentgen.Diagnostics`

- Per-tick duration and load telemetry.
- P50, P95, P99, average, and maximum tick duration.
- Command, event, entity, memory, checkpoint, and fault reporting.
- JSON and console summaries.

### `Roentgen.Headless`

- `run` mode.
- `replay` mode.
- `soak` mode.
- Interactive `console` mode.
- `inspect-snapshot` mode.
- Built-in deterministic smoke scenario.
- Public factory and runner APIs for tests and future tools.

## Authoritative-state rule

Authoritative mutable data must reside in one of these locations:

1. `EntityRegistry` components and metadata.
2. `SimulationStateStore` values.
3. Pending deterministic command envelopes.
4. Explicit versioned sequence counters captured in snapshots.

Simulation systems may cache derived data, but may not keep hidden authoritative state in private fields. This makes checksums, snapshots, and replay verification complete and inspectable.

## Tick lifecycle

For each tick:

1. Increment the completed-tick clock.
2. Drain commands targeting exactly that tick.
3. Dispatch command handlers.
4. Flush generated events.
5. Execute systems in phase/order/ID order.
6. Flush events after every system.
7. Compute a checkpoint when requested.
8. Emit non-authoritative telemetry.

## System phases

1. Input
2. PreSimulation
3. World
4. Gameplay
5. ArtificialIntelligence
6. PostSimulation
7. Finalization

Phase and order collisions are resolved by ordinal `SystemId` comparison.

## Included smoke systems

- `EntityPulseSystem`: stamps simulated entities every 60 ticks.
- `ElapsedTickSystem`: stores the authoritative elapsed tick count in finalization.

These are deliberately small reference systems. They prove lifecycle, scheduling, mutation, snapshots, replay, and soak operation without pre-implementing Phase 4 gameplay.

## Exit-gate evidence

The repository includes tests that:

- Run 10,000 ticks without MonoGame or UI dependencies.
- Round-trip entity registries.
- Restore a snapshot containing pending future commands and continue identically.
- Record and replay checkpoint history.
- Validate system schedule ordering.
- Exercise the command console tokenizer.
- Repeat multiple soak seeds and compare final checksums.
- Reject corrupted snapshots.

## Fault containment

An exception during authoritative tick execution permanently faults that host instance. It rejects additional commands and ticks; recovery requires constructing a fresh host and restoring a previously verified snapshot. Command sequence allocation is rolled back when envelope construction or queue insertion fails before a command becomes authoritative.
