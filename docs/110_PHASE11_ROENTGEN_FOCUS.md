# Phase 11 — Roentgen Focus

Phase 11 implements a deterministic full-pause diagnostic planner over the Phase 10 combat system. Planning freezes authoritative simulation while interface and tactical-camera time remain active. Committed actions reserve Focus, receive immutable resolution seeds, and execute through the existing real-time combat runtime in bounded slow time.

## Delivered

- `Roentgen.Focus` platform-neutral project.
- Target discovery and consequential target graphs for humanoids, mutants, quadrupeds, equipment, and extensible machines.
- Fixed-point forecasts with factor, consequence, uncertainty, armor, injury, incapacitation, and lethality fields.
- Focus resource reservation, spending, recovery, and refund semantics.
- Editable drafts, validation, immutable commit, deterministic seeds, and sequential execution.
- Explicit temporal modes and interruption reasons.
- Tactical-camera state and accessibility contract.
- Twelve typed Focus perk definitions for Phase 12 progression integration.
- Diagnostic mask/composite effects, JSON schemas, FocusLab, and xUnit coverage.
