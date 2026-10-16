# Command, Event, and Replay Architecture

## 1. Authoritative mutation rule

Simulation state may be changed only by:

1. A registered command handler processing a valid command envelope.
2. A registered simulation system executing during its assigned tick.
3. A deterministic event handler invoked by the event bus.
4. Explicit load or migration code before simulation resumes.

UI, rendering, audio, and developer tools must submit commands rather than directly changing authoritative state.

## 2. Command lifecycle

```text
Input or system intent
    ↓
ISimulationCommand writes canonical payload
    ↓
CommandEnvelopeFactory
    ├── payload bytes
    ├── payload SHA-256
    └── deterministic command StableId
    ↓
DeterministicCommandQueue
    ↓ sorted by tick/priority/sequence/ID
CommandRouter
    ↓ schema-range validation
ICommandHandler
    ↓
Simulation state and deterministic events
```

### Command type IDs

Type IDs must be permanent namespaced identifiers, for example:

```text
player.move.v1
player.interact.v1
combat.fire-weapon.v1
inventory.transfer.v1
quest.choose-response.v1
```

The numeric `SchemaVersion` remains separate from the type ID and permits controlled migrations.

### Sequence allocation

Each authoritative command producer owns a `CommandSequenceAllocator`. Sequence allocation happens on the authoritative input collection thread. Producers must not race to obtain sequence values.

## 3. Event lifecycle

Events describe consequences that already occurred or requests that remain inside the deterministic simulation boundary.

Examples:

```text
combat.damage-applied
entity.died
inventory.item-added
faction.reputation-changed
quest.fact-changed
world.door-state-changed
```

Events are not a replacement for commands from the player or external systems.

The event bus assigns a global monotonic event sequence. Events published by a handler are appended and processed before the flush completes. A maximum event count detects cycles.

## 4. Tick pipeline

For tick `T`:

1. Drain commands targeting `T`.
2. Dispatch commands in canonical order.
3. Flush resulting events.
4. Execute each simulation system in canonical order.
5. Flush events after each system.
6. Optionally compute the state checksum for `T`.
7. Record a replay checkpoint when requested.

Commands targeting a tick earlier than the current tick are a deterministic fault. They are not executed late.

## 5. Replay file format

File encoding:

- UTF-8 without BOM.
- Unix newline `\n` on every platform.
- One JSON object per line.
- PascalCase property names in format version 1.

### Header

Contains:

- Format version.
- Product.
- Constitution version.
- Campaign seed.
- Tick rate.
- Random algorithm ID.
- Build fingerprint.
- Header checksum.

### Command record

Contains:

- Command and issuer IDs.
- Target tick.
- Origin priority.
- Sequence.
- Type and schema version.
- Base64 canonical payload.
- Payload checksum.
- Previous chain checksum.
- Record checksum.

### Checkpoint

Contains tick, state checksum, previous checksum, and record checksum.

### Footer

Contains record count and final checksum.

The formal per-line schema is `schemas/replay-record.schema.json`.

## 6. Hash chain

The header begins the chain. Each command or checkpoint includes the previous digest in its canonical hash input. Reordering, deleting, inserting, or modifying records invalidates the chain.

The footer is not another chain element; it declares the verified final chain digest and record count.

## 7. Replay compatibility

A replay can be executed only when:

- Replay format is supported.
- Determinism protocol is supported.
- Random algorithm is supported.
- Command types and schema versions have handlers or migrations.
- Required generator and content versions are available.

The reader verifies structural integrity. Phase 3 will verify semantic replay by running the commands and comparing checkpoints.

## 8. Error handling

Replay faults are explicit:

- Unsupported format → `NotSupportedException`.
- Broken checksum chain → `InvalidDataException`.
- Payload mismatch → `InvalidDataException`.
- Invalid command identity → `InvalidDataException`.
- Missing footer → `InvalidDataException`.
- Trailing data after footer → `InvalidDataException`.
- Unknown command schema at execution → `NotSupportedException`.

No reader attempts to “repair” a replay silently.
