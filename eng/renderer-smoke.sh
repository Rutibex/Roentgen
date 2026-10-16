#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$ROOT/eng/validate_repository.py"
dotnet tool restore
dotnet restore "$ROOT/Roentgen.sln"
dotnet build "$ROOT/src/Roentgen.App/Roentgen.App.csproj" -c Release --no-restore
dotnet run --project "$ROOT/src/Roentgen.App/Roentgen.App.csproj" -c Release --no-build
