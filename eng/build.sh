#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$ROOT/eng/bootstrap.py"
python3 "$ROOT/eng/validate_repository.py"
python3 "$ROOT/eng/verify_golden_vectors.py"
dotnet tool restore
dotnet restore "$ROOT/Roentgen.sln" --locked-mode || dotnet restore "$ROOT/Roentgen.sln"
dotnet build "$ROOT/Roentgen.sln" -c Release --no-restore
dotnet test "$ROOT/Roentgen.sln" -c Release --no-build --no-restore
