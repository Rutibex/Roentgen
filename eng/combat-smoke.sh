#!/usr/bin/env sh
set -eu
python3 eng/verify_combat_contracts.py
python3 eng/verify_combat_vectors.py
dotnet test tests/Roentgen.Combat.Tests/Roentgen.Combat.Tests.csproj --configuration Release
dotnet run --project tools/Roentgen.CombatLab --configuration Release -- duel --ticks 600
