#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
dotnet run --project tools/Roentgen.StructureLab -- generate --seed 5931035436993955150 --output artifacts/structurelab
dotnet run --project tools/Roentgen.StructureLab -- validate --seed 5931035436993955150
