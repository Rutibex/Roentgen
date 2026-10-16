# ADR 0033: One agent state spans all simulation tiers

## Decision

Full, reduced, and strategic simulation are fidelity levels over one stable agent identity and simulation record, not separate NPC instances.

## Consequences

Schedules, travel progress, needs, health, awareness, equipment authority, and activity survive demotion, save/load, and later promotion.
