# Simulation State Model

## Entities

An entity contains:

- Stable 128-bit ID.
- Archetype ID.
- Creation tick.
- Ordinally sorted tags.
- Ordinally sorted component states.

A component contains:

- Semantic type ID.
- Positive schema version.
- Canonical opaque payload.
- SHA-256 payload checksum.

Feature projects will own typed codecs layered over these component payloads. The registry does not use runtime type names as persistent identifiers.

## Global state

`SimulationStateStore` contains versioned canonical values under permanent semantic keys. It is intended for state that does not naturally belong to one entity, such as world clocks, campaign stage, global counters, or director state.

## Mutation

Normal gameplay mutation enters through deterministic command handlers and ordered systems. Direct mutation APIs remain public for factories, restoration, and controlled subsystem implementation, but presentation clients must not call them.

## Checksums

The Phase 3 state checksum includes:

- Entire entity registry.
- Entire global state store.
- Completed tick through `SimulationChecksumService`.

Pending commands and sequence counters are integrity-protected by the snapshot payload checksum but are not part of gameplay-state replay checkpoints. This distinction allows replay checkpoints to compare realized state while snapshots still preserve exact continuation state.

## Services

`SimulationServiceRegistry` is populated during composition and frozen at host initialization. Systems retrieve shared authoritative stores through the tick or initialization context rather than static globals.
