# ADR 0039 — Campaign coordinates rather than replaces quest and society authority

## Decision

`Roentgen.Campaign` owns milestones, revelations, commitments, convergence, endings, and epilogues. It binds Phase 16 quest plans and observes Phase 15 society state but does not duplicate their mutable authority.

## Consequences

The dependency graph remains acyclic and headless. Campaign transitions occur through stable IDs and typed records. Presentation remains downstream.
