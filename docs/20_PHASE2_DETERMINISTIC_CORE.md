# Phase 2 — Deterministic Core

**Status:** Implemented baseline  
**Package version:** `0.2.0-phase2`  
**Protocol version:** `1`

## 1. Purpose

Phase 2 establishes the rules that make a generated Roentgen campaign reproducible, debuggable, replayable, and save-compatible. The deterministic core is not a claim that every future floating-point calculation will produce identical bits on every conceivable processor. It is the enforceable architecture that keeps semantic generation, simulation ordering, persistent identity, command execution, replay records, and state verification stable across supported builds.

The Phase 2 exit gate is:

> Stable seed derivation, IDs, command logs, versioned random streams, checksums, and determinism tests pass.

## 2. Implemented deliverables

- Canonical 128-bit `StableId` parsing, formatting, ordering, and derivation.
- Versioned campaign-domain and child-domain seed derivation.
- Versioned PCG32 deterministic random streams.
- Stream snapshots, restoration, deterministic forking, bounded integers, floating samples, bytes, and shuffling.
- Canonical binary state encoding with explicit type tags, big-endian numbers, UTF-8 NFC strings, normalized zero, and rejection of non-finite floating-point values.
- SHA-256 state checksums represented by `StateChecksum`.
- Deterministically ordered map and set wrappers.
- Ordered command envelopes, command IDs, payload checksums, queueing, validation, routing, and sequence allocation.
- Ordered event production and handler dispatch.
- Ordered simulation-system execution.
- State-checksum contributors sorted by stable contributor ID.
- Generation-provenance builder with decisions, validation results, repair history, generator versions, and fingerprints.
- Hash-chained JSONL replay logs with commands, checkpoints, headers, and footers.
- Replay tamper detection and format validation.
- Determinism diagnostics for comparing checkpoint series.
- Cross-language golden vectors for stable IDs, domain seeds, and random streams.
- Static repository policy that rejects nondeterministic APIs from deterministic layers.
- Dedicated deterministic-core test assembly.

## 3. Ownership by project

### `Roentgen.Contracts`

Owns persistence-facing value contracts:

- `StableId`
- `CampaignSeed`
- `DomainSeed`
- `GenerationDecision`
- `GenerationProvenance`

These types contain no MonoGame dependencies.

### `Roentgen.Core`

Owns deterministic representation primitives:

- `CanonicalWriter`
- `CanonicalReader`
- `ICanonicalWritable`
- `StateChecksum`
- `StableIdFactory`
- `DeterministicMap`
- `DeterministicSet`
- `DeterministicFingerprint`
- `DeterminismProtocol`

### `Roentgen.Generation`

Owns random and generation identity:

- `GenerationDomain`
- `Pcg32Random`
- `RandomStreamFactory`
- `RandomStreamSnapshot`
- `GenerationProvenanceBuilder`

### `Roentgen.Simulation`

Owns authoritative ordering:

- `CommandEnvelope`
- `CommandEnvelopeFactory`
- `CommandSequenceAllocator`
- `DeterministicCommandQueue`
- `CommandRouter`
- `EventEnvelope`
- `DeterministicEventBus`
- `SimulationPipeline`
- `SimulationChecksumService`

### `Roentgen.Persistence`

Owns durable command-log and replay representation:

- `ReplayHeader`
- `ReplayCheckpoint`
- `ReplayLogWriter`
- `ReplayLogReader`
- Replay hash-chain computation

### `Roentgen.Diagnostics`

Owns comparison results and mismatch reporting.

## 4. Stable identity policy

A `StableId` is a 128-bit semantic identifier. It is never sourced from `Guid.NewGuid`, runtime object identity, memory addresses, collection enumeration order, or the current clock.

Stable IDs are generated from canonical inputs using SHA-256 and truncating the digest to 128 bits. The current schemes are:

```text
sha256-stable-id-v1
roentgen.stable-id
roentgen.stable-id.child
```

Typical identity hierarchy:

```text
Campaign root
└── Region
    └── Settlement
        ├── Building
        │   ├── Room
        │   └── Container
        └── NPC
```

The semantic local key must remain stable. Display names, translated text, array positions that can change after insertion, and runtime spawn order must not be used as identity keys.

## 5. Seed hierarchy

A campaign begins with one unsigned 64-bit `CampaignSeed`. Every subsystem derives named domains instead of sharing a mutable global random stream.

```text
CampaignSeed
├── world.history
├── world.terrain
│   ├── height.primary
│   ├── erosion.detail
│   └── road.deformation
├── world.factions
├── settlement.<stable-id>
├── structure.<stable-id>
├── character.<stable-id>
├── audio.<stable-id>
└── music.<stable-id>
```

Domain names are:

- Trimmed.
- Lowercased invariantly.
- Restricted to ASCII letters, numbers, period, hyphen, and underscore.
- Versioned by `DomainSeed.DerivationVersion`.

Changing decorative generation must use a new child domain rather than consuming additional values from an established semantic stream.

## 6. Random-stream policy

The Phase 2 algorithm is `pcg32-roentgen-v1`.

A stream is created from:

- Domain seed value.
- Domain path.
- Domain derivation version.
- Stream name.
- Random algorithm ID.
- Stream derivation version.

Supported operations include:

- `NextUInt32`
- `NextUInt64`
- Unbiased bounded `NextInt32`
- `NextBoolean`
- `NextSingle`
- `NextDouble`
- `NextBytes`
- Fisher–Yates `Shuffle`
- Snapshot and restore
- Deterministic fork

Random stream state is persistence-compatible only while its algorithm ID and derivation version match. A future algorithm must receive a new ID; it must not silently alter version 1 behavior.

## 7. Canonical encoding

`CanonicalWriter` exists because ordinary object serialization is not a safe state-checksum format. Canonical fields use explicit tags and lengths so different values cannot produce ambiguous byte sequences.

| Value | Encoding rule |
|---|---|
| Null | Dedicated null tag |
| Boolean | Tag plus `0` or `1` |
| Signed/unsigned integers | Big-endian fixed width |
| Float/double | IEEE bits, negative zero normalized, NaN/infinity rejected |
| String | UTF-8, Unicode NFC, explicit byte length |
| Byte sequence | Explicit length plus raw bytes |
| Stable ID | Two big-endian 64-bit words |
| State checksum | 32 raw digest bytes |
| Collection | Explicit count followed by deterministic element order |

Canonical encoding is a checksum and identity protocol. It is not intended as a general user-facing save format.

## 8. Command ordering

Every authoritative mutation enters simulation through a `CommandEnvelope`.

The queue order is:

1. Target tick.
2. Origin priority.
3. Sequence number.
4. Command ID.

Commands with the same deterministic key are rejected. Commands targeting a missed earlier tick cause an error rather than being silently executed late.

Current priority values are deliberately spaced for future insertion:

```text
Engine       0
Replay     100
Network    200
Player     300
Development 900
```

A command ID includes the issuer, tick, priority, sequence, type, schema version, and payload checksum. Modifying any authoritative field changes the ID.

## 9. Event and system ordering

Events are assigned a monotonic sequence by the authoritative event bus. Event subscribers execute by:

1. Explicit priority.
2. Ordinal handler ID.

Simulation systems execute by:

1. Explicit order.
2. Ordinal system ID.

Events are flushed after command execution and after each simulation system. A bounded dispatch limit detects recursive event storms.

## 10. State checksums

A simulation state checksum includes:

- Protocol marker.
- Protocol version.
- Tick.
- Contributor count.
- Contributors sorted by ordinal contributor ID.
- Each contributor’s length-framed canonical state.

Presentation state is excluded. Examples of excluded state include camera interpolation, window size, frame time, UI animation, logging timestamps, audio playback cursor, and GPU caches.

The checksum service does not discover objects through reflection. Each authoritative subsystem must explicitly register a contributor and define its canonical state.

## 11. Replay logs

Replay logs are newline-delimited JSON with a SHA-256 hash chain. The JSON is an inspection and transport representation; record verification uses the canonical binary hash protocol.

Record sequence:

```text
header
command or checkpoint...
footer
```

The reader verifies:

- Supported format version.
- Header checksum.
- Command payload checksum.
- Command deterministic ID.
- Previous-record checksum.
- Record checksum.
- Footer count.
- Final chain checksum.
- Absence of trailing data.

Replay logs intentionally record commands rather than every resulting event. The Phase 3 headless simulation will consume these commands and compare checkpoint checksums.

## 12. Generation provenance

Every important generated object can retain:

- Object and parent stable IDs.
- Campaign and domain seed.
- Generator ID and schema version.
- Random algorithm ID.
- Ordered generation decisions.
- Validation results.
- Repair history.
- Provenance fingerprint.

This information is the foundation of Seed Inspector, failed-seed exports, generator migration, and regression reproduction.

## 13. Forbidden deterministic APIs

The static validator rejects these APIs in Core, Generation, Simulation, and Persistence:

- `System.Random`
- `Random.Shared`
- `Guid.NewGuid`
- `DateTime.Now`
- `DateTimeOffset.Now`
- `HashCode.Combine`
- `string.GetHashCode`

Additional policy:

- Do not depend on dictionary or hash-set enumeration order.
- Do not derive state from thread completion order.
- Do not use localized formatting as an identity input.
- Do not let visual frame time alter authoritative calculations.
- Do not use filesystem ordering without an ordinal sort.
- Do not use reflection discovery order as execution order.

## 14. Developer workflow

Run complete validation:

```powershell
./eng/build.ps1
```

Run the deterministic suite repeatedly:

```powershell
./eng/determinism.ps1 -Repeat 10
```

Bash equivalents:

```bash
./eng/build.sh
./eng/determinism.sh 10
```

Run language-independent vectors only:

```bash
python eng/verify_golden_vectors.py
```

## 15. Phase 3 integration contract

Phase 3 must build the headless simulation on these primitives rather than replacing them.

It must:

- Accept a campaign seed and replay header.
- Register systems in explicit order.
- Submit all mutations as commands.
- Emit checkpoints through `SimulationChecksumService`.
- Replay a command log without MonoGame.
- Compare the resulting checksum series.
- Export the first mismatch tick and contributor diagnostics.
