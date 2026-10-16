#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$ROOT/tools/Roentgen.ContentLab/Roentgen.ContentLab.csproj" -- "${@:-audit}"
