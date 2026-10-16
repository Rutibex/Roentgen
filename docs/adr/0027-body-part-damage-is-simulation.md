# ADR 0027: Body-part damage is simulation

## Status

Accepted.

## Decision

Generated anatomical target parts are authoritative simulation components rather than UI labels. Each has integrity, trauma, functional state, armor coverage and consequences.

## Consequences

Humanoid and creature injuries affect behavior before Roentgen Focus exists. Every later creature family must provide valid target parts and functional mappings. Rendering consumes injury presentation derived from simulation state.
