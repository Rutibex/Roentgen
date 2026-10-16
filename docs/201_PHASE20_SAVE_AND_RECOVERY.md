# Phase 20 Save, Load, and Recovery

Phase 20 introduces the `RGSAVE20` container and `SaveSlotManager`.

## Container sections

Each save contains metadata, player transform/state, progression, detailed player-world interaction state, combat state, the Phase 17 campaign aggregate (items, society, narrative, and campaign director), adaptive music state, optional thumbnail bytes, and a sorted generator-version table.

## Integrity and recovery

- SHA-256 covers the canonical payload.
- Every named section has a bounded payload and checksum.
- Files are written to a temporary path and parsed successfully before installation.
- The previous primary becomes `.bak` before atomic replacement.
- A damaged primary can recover from its valid backup and reports that recovery to the player.
- Slot IDs are path-safe and bounded.
- Manual, quick, rotating automatic, transition, and major-choice slot kinds are defined.
- Autosaves are rotated to the configured slot limit.
- Unknown future format versions are rejected rather than guessed.

## Deterministic restoration

Loading reconstructs the campaign from its saved seed, verifies the stable player identity, restores player-world state, progression, item/society/narrative/campaign snapshots, combat state, adaptive music state, play time, and a fresh Focus runtime bound to the restored authoritative systems.
