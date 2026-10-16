#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$ROOT/eng/validate_repository.py"
dotnet test "$ROOT/Roentgen.sln" -c Release --collect:"XPlat Code Coverage" --results-directory "$ROOT/artifacts/test-results"
