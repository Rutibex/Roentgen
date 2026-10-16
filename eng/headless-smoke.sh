#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/headless-smoke"
rm -rf "$OUT"
mkdir -p "$OUT"
dotnet run --project "$ROOT/src/Roentgen.Headless/Roentgen.Headless.csproj" -c Release -- run \
  --seed 7351982471 --ticks 10000 --entities 64 --checksum-interval 600 \
  --replay-out "$OUT/smoke.replay.jsonl" \
  --snapshot-out "$OUT/smoke.rgsnap" \
  --telemetry-out "$OUT/smoke.telemetry.json"
dotnet run --project "$ROOT/src/Roentgen.Headless/Roentgen.Headless.csproj" -c Release -- replay \
  --input "$OUT/smoke.replay.jsonl" \
  --snapshot-out "$OUT/replayed.rgsnap" \
  --telemetry-out "$OUT/replay.telemetry.json"
