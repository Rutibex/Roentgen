#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$ROOT/tools/Roentgen.QuestLab/Roentgen.QuestLab.csproj" -- validate
dotnet run --project "$ROOT/tools/Roentgen.QuestLab/Roentgen.QuestLab.csproj" -- solve
dotnet test "$ROOT/tests/Roentgen.Narrative.Tests/Roentgen.Narrative.Tests.csproj" -c Release
