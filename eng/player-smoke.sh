#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
python3 "$ROOT/eng/verify_player_contracts.py"
dotnet test "$ROOT/tests/Roentgen.Player.Tests/Roentgen.Player.Tests.csproj" --configuration Release
