$ErrorActionPreference = 'Stop'
python eng/verify_character_contracts.py
python eng/verify_character_vectors.py
dotnet test tests/Roentgen.Characters.Tests/Roentgen.Characters.Tests.csproj --configuration Release
