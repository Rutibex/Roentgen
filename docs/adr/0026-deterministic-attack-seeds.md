# ADR 0026: Deterministic attack seeds

## Status

Accepted.

## Decision

Every attack variation derives from campaign seed, actor ID, weapon ID, committed simulation tick, per-weapon sequence and a named domain. Global mutable randomness and render-frame randomness are forbidden.

## Consequences

Pellets, spread and jam rolls are reproducible. Adding an unrelated visual effect cannot change combat outcomes. Replays and Roentgen Focus previews can refer to the same committed sequence.
