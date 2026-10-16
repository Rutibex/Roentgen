# ADR 0006 — Command, event, and replay ordering

## Decision

Authoritative mutations use command envelopes. Commands sort by tick, origin priority, sequence, and command ID. Systems sort by order and ordinal ID. Event handlers sort by priority and ordinal ID. Replay logs record commands and periodic state checksums in a chained JSONL format.

## Rationale

Implicit registration order, thread completion, frame timing, and hash-table ordering cannot define simulation outcomes.

## Consequences

Every command producer, system, event handler, and checksum contributor requires a stable permanent ID and explicit ordering metadata.
