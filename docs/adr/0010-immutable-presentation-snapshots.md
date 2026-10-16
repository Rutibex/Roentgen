# ADR 0010 — Immutable presentation snapshots

## Decision

The renderer consumes immutable frame snapshots rather than querying or mutating authoritative entity and simulation stores during drawing.

## Reason

Presentation can interpolate, skip, or repeat frames without changing deterministic simulation behavior.
