# ADR 0007 — Headless authoritative host

## Decision

`Roentgen.Headless` and `SimulationHost` define the authoritative execution model. MonoGame will become a command-producing and snapshot-consuming presentation client rather than the owner of gameplay state.

## Consequences

- Simulation tests do not require a graphics device.
- Server-like automation, replay, and soak tools use the production host.
- Presentation code cannot directly mutate authoritative state.
