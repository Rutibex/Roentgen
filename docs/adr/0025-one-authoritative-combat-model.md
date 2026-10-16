# ADR 0025: One authoritative combat model

## Status

Accepted.

## Decision

Real-time combat and Roentgen Focus use the same deterministic actors, weapons, action timelines, hit queries, projectiles, armor, damage, injury, status and death systems. Roentgen Focus is an interface and planning mode over this model.

## Consequences

Phase 10 must expose pure evaluation and serializable committed actions. Phase 11 cannot use separate hit percentages or damage formulas. Fixes to the combat resolver automatically apply to both modes.
