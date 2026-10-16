# ADR 0014 — Global integer terrain sampling

## Decision

Authoritative terrain heights are evaluated from absolute world coordinates in integer centimetres. Chunks and LODs are views over that shared function.

## Consequences

Adjacent chunks produce byte-identical edge heights regardless of request order or worker count. Renderer-local floating point cannot enter saved or checksummed world state. Terrain deformation, when introduced, must be represented as deterministic world deltas layered over this baseline.
