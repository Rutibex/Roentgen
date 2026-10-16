$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  python eng/verify_world_contracts.py
  python eng/verify_world_vectors.py
  dotnet test tests/Roentgen.World.Tests/Roentgen.World.Tests.csproj --configuration Release
  dotnet test tests/Roentgen.Physics.Tests/Roentgen.Physics.Tests.csproj --configuration Release
  $count = if ($env:ROENTGEN_TERRAIN_SEEDS) { $env:ROENTGEN_TERRAIN_SEEDS } else { '20' }
  dotnet run --project tools/Roentgen.WorldLab -- validate-seeds --count $count --start 1
} finally {
  Pop-Location
}
