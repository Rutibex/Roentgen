#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$ROOT/eng/verify_phase21_contracts.py"
python3 "$ROOT/eng/verify_phase21_vectors.py"
python3 "$ROOT/eng/verify_source_manifest.py"
dotnet restore "$ROOT/Roentgen.sln" --locked-mode
dotnet build "$ROOT/Roentgen.sln" -c Release --no-restore
dotnet test "$ROOT/tests/Roentgen.Phase21.Tests/Roentgen.Phase21.Tests.csproj" -c Release --no-build
dotnet run --project "$ROOT/tools/Roentgen.ContentLab/Roentgen.ContentLab.csproj" -c Release --no-build -- audit
