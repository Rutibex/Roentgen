# ADR 0005 — PCG32 random stream version 1

## Decision

The first Roentgen deterministic random algorithm is `pcg32-roentgen-v1`, seeded from SHA-256-derived domain and stream inputs.

## Rationale

PCG32 is compact, fast, has a simple fully specified transition, supports independent sequences, and is easy to reproduce outside C# for golden-vector verification.

## Consequences

The implementation is frozen by golden vectors. A future random algorithm receives a new identifier and does not modify version 1 output.
