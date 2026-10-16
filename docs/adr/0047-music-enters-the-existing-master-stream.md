# ADR 0047 — Music enters the existing master stream

## Decision

The SoundFont renderer implements the Phase 18 streaming-source contract and is mixed into the existing master source. It never creates another audio device or PCM host.
