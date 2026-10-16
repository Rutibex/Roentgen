#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
python eng/verify_structure_contracts.py
python eng/verify_structure_vectors.py
dotnet test tests/Roentgen.Structures.Tests/Roentgen.Structures.Tests.csproj --configuration Release
dotnet run --project tools/Roentgen.StructureLab -- validate-seeds --count "${ROENTGEN_STRUCTURE_SEEDS:-1000}"
dotnet run --project tools/Roentgen.StructureLab -- stream-smoke --iterations 256
dotnet run --project tools/Roentgen.StructureLab -- collision-smoke
