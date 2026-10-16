#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python "$ROOT/eng/verify_item_contracts.py"
python "$ROOT/eng/verify_item_vectors.py"
dotnet test "$ROOT/tests/Roentgen.Items.Tests/Roentgen.Items.Tests.csproj" --configuration Release
