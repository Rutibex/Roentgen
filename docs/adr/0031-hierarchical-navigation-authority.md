# ADR 0031: Hierarchical navigation is authoritative

## Decision

AI chooses semantic destinations. `Roentgen.Navigation` alone owns route construction, dynamic invalidation, and route checksums. Animation never owns displacement and combat AI does not move actors directly.

## Consequences

Outdoor, interior, streaming, collision, schedules, and companion commands consume one route vocabulary. Deterministic search and stable tie breaking permit replay and cross-platform regression tests.
