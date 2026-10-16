# ADR 0043 — Semantic Audio Is Observer-Only

**Status:** Accepted

Audio consumes stable semantic events and snapshots but never changes authoritative simulation. Audio state and floating-point PCM are excluded from gameplay checksums. Enabling, disabling, faulting, or replacing the audio device cannot alter campaign results.
