#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
python eng/verify_world_contracts.py
python eng/verify_world_vectors.py
dotnet test tests/Roentgen.World.Tests/Roentgen.World.Tests.csproj --configuration Release
dotnet test tests/Roentgen.Physics.Tests/Roentgen.Physics.Tests.csproj --configuration Release
dotnet run --project tools/Roentgen.WorldLab -- validate-seeds --count "${ROENTGEN_TERRAIN_SEEDS:-20}" --start 1
