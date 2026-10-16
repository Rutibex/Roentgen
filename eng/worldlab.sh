#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
dotnet run --project tools/Roentgen.WorldLab -- summary --seed 0x524F454E5447454E --output artifacts/worldlab/summary.json
dotnet run --project tools/Roentgen.WorldLab -- export-map --seed 0x524F454E5447454E --layer regions --output artifacts/worldlab/regions.ppm
dotnet run --project tools/Roentgen.WorldLab -- inspect-chunk --seed 0x524F454E5447454E --x 0 --z 0 --output artifacts/worldlab/chunk-0-0.json
dotnet run --project tools/Roentgen.WorldLab -- validate-seeds --count 20 --start 1
