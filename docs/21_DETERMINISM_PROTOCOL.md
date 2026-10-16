# Determinism Protocol Version 1

This document is normative for persisted identity, generation seeds, random streams, command ordering, replay verification, and semantic state checksums.

## 1. Versioned algorithm identifiers

| Function | Identifier |
|---|---|
| Protocol | `1` |
| Stable IDs | `sha256-stable-id-v1` |
| State checksums | `sha256-canonical-state-v1` |
| Random stream | `pcg32-roentgen-v1` |
| Replay file | `roentgen-replay-jsonl-v1` |

An implementation change that alters an output vector requires a new algorithm or protocol version.

## 2. Stable ID derivation

### Root ID

Canonical fields:

```text
"roentgen.stable-id"
1
namespace ID
local key
```

Compute SHA-256 and interpret digest bytes 0–7 and 8–15 as unsigned big-endian words.

### Child ID

Canonical fields:

```text
"roentgen.stable-id.child"
1
parent StableId
child kind
local key
```

The first 128 digest bits become the child ID.

## 3. Domain seed derivation

Canonical fields:

```text
"roentgen.domain-seed"
derivation version
campaign seed
normalized domain path
```

The first 64 digest bits, interpreted unsigned big-endian, become the domain value.

A child domain is derived from the parent value and path rather than from the campaign root again. This preserves hierarchical provenance.

## 4. Random stream derivation

Canonical fields:

```text
"roentgen.random-stream"
stream derivation version
"pcg32-roentgen-v1"
domain seed value
domain path
domain derivation version
normalized stream name
```

Digest bytes 0–7 become the PCG seed. Bytes 8–15 become the PCG sequence. The PCG increment is `(sequence << 1) | 1` with unsigned 64-bit wraparound.

## 5. PCG32 transition

```text
oldState = state
state = oldState * 6364136223846793005 + increment
xorshifted = uint32(((oldState >> 18) XOR oldState) >> 27)
rotation = oldState >> 59
output = rotateRight(xorshifted, rotation)
```

Initialization:

```text
state = 0
advance once
state += seed
advance once
reported generated-value count = 0
```

The two initialization advances are not counted as values returned to the caller.

## 6. Bounded integers

Modulo bias is rejected using a threshold:

```text
bound = exclusive maximum
threshold = unsigned(-bound) mod bound
repeat value = NextUInt32 while value < threshold
result = value mod bound
```

## 7. Canonical string rules

- Null is distinct from empty.
- Unicode is normalized to NFC.
- Encoding is UTF-8 without a byte-order mark.
- String lengths are byte lengths, not UTF-16 code-unit counts.
- Identity and ordering use ordinal semantics.

## 8. Floating-point rules

Canonical state:

- Rejects NaN.
- Rejects positive and negative infinity.
- Normalizes negative zero to positive zero.
- Writes IEEE 754 bit patterns in big-endian order.

Future simulation systems should prefer integers, fixed-point values, or quantized canonical values for gameplay state that must remain stable across CPU architectures.

## 9. Collection rules

- Sequence order is semantic and is written as supplied.
- Maps must be sorted by a stable comparer before writing.
- String keys use ordinal ordering.
- Sets must be sorted before writing.
- Hash table capacity, insertion layout, and runtime hash values are never serialized.

## 10. Command ordering

Commands are strictly ordered by this tuple:

```text
(TargetTick, OriginPriority, Sequence, CommandId)
```

No wall-clock timestamp participates.

## 11. Simulation system ordering

Systems are strictly ordered by:

```text
(Order, SystemId ordinal)
```

Event handlers are strictly ordered by:

```text
(Priority, HandlerId ordinal)
```

## 12. State checksum framing

The simulation checksum writes:

```text
"roentgen.simulation-state"
1
tick
contributor count
for each contributor sorted by ID:
    contributor ID
    byte length and canonical contributor payload
```

The final SHA-256 digest is represented as four unsigned big-endian 64-bit words and printed as 64 lowercase hexadecimal characters.

## 13. Golden vectors

`eng/determinism-golden-vectors.json` is normative. `eng/verify_golden_vectors.py` implements an independent reference calculation. `tests/fixtures/phase2-golden.replay.jsonl` and `eng/verify_replay_fixture.py` freeze the replay chain across languages. C# tests consume the same fixture.

Changing a golden value is a protocol change, not a routine test update.
