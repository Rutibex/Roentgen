#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPEAT="${1:-5}"
python3 "$ROOT/eng/validate_repository.py"
python3 "$ROOT/eng/verify_golden_vectors.py"
for ((run=1; run<=REPEAT; run++)); do
  echo "Determinism test pass $run of $REPEAT"
  dotnet test "$ROOT/tests/Roentgen.Determinism.Tests/Roentgen.Determinism.Tests.csproj" -c Release
done
