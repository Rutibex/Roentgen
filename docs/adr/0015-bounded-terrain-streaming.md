# ADR 0015 — Bounded terrain streaming and render uploads

## Decision

CPU terrain generation and GPU resource upload use separate bounded queues and budgets. Every asynchronous result carries an epoch and is rejected when stale.

## Consequences

The game applies back-pressure instead of consuming unbounded memory. The render thread controls all GPU creation and disposal. Teleports and rapid traversal can waste bounded CPU work, but stale results cannot overwrite current residency.
