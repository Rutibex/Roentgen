# Simulation Snapshot Format 1

## File layout

```text
8 bytes   ASCII magic: RGSNAP01
8 bytes   signed big-endian canonical-payload length
N bytes   canonical snapshot payload
32 bytes  SHA-256 of the canonical payload
```

Trailing bytes are forbidden.

## Canonical payload

The payload contains, in order:

1. Format marker and version.
2. Campaign seed and tick rate.
3. Completed tick and time scale.
4. Next command and event sequence counters.
5. Entity-registry mutation version.
6. Entities sorted by `StableId`.
7. Tags and components sorted ordinally.
8. Global-state mutation version.
9. Global values sorted by key.
10. Pending commands in canonical queue order.
11. Authoritative state checksum at the completed tick.

Each component and global-state value contains its own payload checksum. Every pending command is revalidated with `CommandEnvelopeFactory` during loading.

## Capture boundary

Snapshots may only be captured when the event bus is empty after a completed tick. This prevents transient partially-dispatched event state from being omitted.

## Restore rules

A host accepts a snapshot only when:

- The format is supported.
- Campaign seed matches.
- Tick rate matches.
- All nested checksums are valid.
- Entity, tag, component, and key ordering is canonical.
- Pending commands are valid, target ticks after the completed snapshot tick, and are in canonical order.
- The next command sequence is greater than every pending command sequence.
- The restored authoritative state checksum equals the stored checksum.

## Versioning

Format changes require a new format version and an explicit migration reader. Existing fields must never be reinterpreted silently.
