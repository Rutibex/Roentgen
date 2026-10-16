# ADR 0046 — Use the supplied fixed SoundFont

## Decision

`default.sf2` is the sole required release bank. It is not regenerated or replaced. Startup verifies its exact SHA-256 fingerprint and falls back to silent music on failure.
