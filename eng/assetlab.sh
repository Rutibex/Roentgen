#!/usr/bin/env bash
set -euo pipefail
output="${1:-artifacts/assetlab}"
dotnet restore Roentgen.sln --locked-mode || dotnet restore Roentgen.sln
dotnet run --project tools/Roentgen.AssetLab --configuration Release -- compile-catalog --output "$output"
