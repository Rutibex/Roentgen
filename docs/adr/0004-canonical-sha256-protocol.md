# ADR 0004 — Canonical SHA-256 protocol

## Decision

Roentgen uses explicit tagged canonical bytes and SHA-256 for stable IDs, state checksums, payload checksums, provenance fingerprints, and replay chains.

## Rationale

Runtime hashes, JSON property ordering, culture-sensitive text, and reflection order are unsuitable for persistence. SHA-256 is available in the base class library, stable across platforms, and independently reproducible.

## Consequences

Changing canonical field order or tags is a protocol change. Canonical encoding must be used deliberately and is not a reflection serializer.
