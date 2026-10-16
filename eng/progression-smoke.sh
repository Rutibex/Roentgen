#!/usr/bin/env bash
set -euo pipefail
dotnet test tests/Roentgen.Rpg.Tests
dotnet run --project tools/Roentgen.BalanceLab -- scenario
