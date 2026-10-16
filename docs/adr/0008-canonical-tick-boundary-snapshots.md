# ADR 0008 — Canonical tick-boundary snapshots

## Decision

Snapshots are captured only after a completed tick with an empty event queue. They include entity state, global state, pending commands, and sequence counters in a canonical integrity-protected binary format.

## Consequences

- Restore resumes exactly from a stable boundary.
- In-flight event dispatch never needs serialization.
- Snapshot corruption and noncanonical ordering are rejected early.
