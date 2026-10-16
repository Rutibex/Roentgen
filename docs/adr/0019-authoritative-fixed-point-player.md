# ADR 0019 — Authoritative fixed-point player

The player uses integer millimetres and fixed-step intent commands. MonoGame input and camera types remain presentation concerns. This preserves deterministic replay and prevents render frame rate from changing movement.
