# ADR 0011 — Forward renderer first

## Decision

Roentgen begins with a bounded forward renderer, one directional shadow map, explicit render targets, and custom effects.

## Reason

The target low-poly procedural style benefits more from predictable material and lighting costs than from an early deferred pipeline. The design remains replaceable behind the snapshot contract.
