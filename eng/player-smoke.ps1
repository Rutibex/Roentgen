$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
python "$Root/eng/verify_player_contracts.py"
python "$Root/eng/verify_player_vectors.py"
dotnet test "$Root/tests/Roentgen.Player.Tests/Roentgen.Player.Tests.csproj" --configuration Release
