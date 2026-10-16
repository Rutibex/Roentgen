# ADR 0009 — Render-thread graphics ownership

## Decision

All MonoGame graphics-resource creation, mutation, binding, and disposal occurs on the MonoGame render thread. Worker threads produce CPU data and enqueue bounded upload operations.

## Reason

This prevents intermittent device-context faults and gives streaming work an explicit backpressure point.
