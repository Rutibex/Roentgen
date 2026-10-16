# Headless CLI

## Build

```bash
dotnet build src/Roentgen.Headless/Roentgen.Headless.csproj -c Release
```

## Run a deterministic scenario

```bash
dotnet run --project src/Roentgen.Headless -- run \
  --seed 7351982471 \
  --ticks 10000 \
  --scenario smoke \
  --entities 64 \
  --checksum-interval 600 \
  --replay-out artifacts/run.replay.jsonl \
  --snapshot-out artifacts/run.rgsnap \
  --telemetry-out artifacts/run.telemetry.json
```

`--scenario none` starts with no scheduled commands. A snapshot can be continued with `--snapshot-in`.

## Verify a replay

```bash
dotnet run --project src/Roentgen.Headless -- replay \
  --input artifacts/run.replay.jsonl \
  --snapshot-out artifacts/replayed.rgsnap \
  --telemetry-out artifacts/replay.telemetry.json
```

Exit code `3` means at least one checkpoint mismatched.

## Run a soak test

```bash
dotnet run --project src/Roentgen.Headless -- soak \
  --start-seed 1 \
  --seeds 100 \
  --ticks 10000 \
  --entities 64 \
  --repetitions 2 \
  --report artifacts/soak.json
```

Each seed is repeated. A different final checksum for a repeated seed is a determinism failure.

## Interactive console

```bash
dotnet run --project src/Roentgen.Headless -- console --seed 7351982471
```

Commands:

```text
status
step [ticks]
run [ticks]
entities
systems
checksum
spawn <archetype> [tag ...]
destroy <stable-id>
set-global <key> <int64>
add-global <key> <delta>
set-component <stable-id> <type-id> <int64>
snapshot <path>
load <path>
quit
```

## Inspect a snapshot

```bash
dotnet run --project src/Roentgen.Headless -- inspect-snapshot --input artifacts/run.rgsnap
```

## Exit codes

- `0`: success.
- `1`: argument, I/O, validation, or runtime failure.
- `2`: telemetry recorded a host fault.
- `3`: replay checkpoint mismatch.
- `4`: soak mismatch or fault.
- `130`: cancellation.
