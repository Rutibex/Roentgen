#!/usr/bin/env bash
set -euo pipefail
dotnet run --project tools/Roentgen.AudioLab/Roentgen.AudioLab.csproj -c Release -- "$@"
