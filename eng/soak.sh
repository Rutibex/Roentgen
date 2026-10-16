#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SEEDS="${1:-10}"
TICKS="${2:-10000}"
OUT="$ROOT/artifacts/soak"
mkdir -p "$OUT"
dotnet run --project "$ROOT/src/Roentgen.Headless/Roentgen.Headless.csproj" -c Release -- soak \
  --start-seed 1 --seeds "$SEEDS" --ticks "$TICKS" --entities 64 --repetitions 2 \
  --report "$OUT/soak.json"
