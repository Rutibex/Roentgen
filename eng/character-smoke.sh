#!/usr/bin/env sh
set -eu
python3 eng/verify_character_contracts.py
python3 eng/verify_character_vectors.py
dotnet test tests/Roentgen.Characters.Tests/Roentgen.Characters.Tests.csproj --configuration Release
