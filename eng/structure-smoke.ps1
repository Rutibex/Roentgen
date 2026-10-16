$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    python eng/verify_structure_contracts.py
    python eng/verify_structure_vectors.py
    dotnet test tests/Roentgen.Structures.Tests/Roentgen.Structures.Tests.csproj --configuration Release
    $Count = if ($env:ROENTGEN_STRUCTURE_SEEDS) { $env:ROENTGEN_STRUCTURE_SEEDS } else { '1000' }
    dotnet run --project tools/Roentgen.StructureLab -- validate-seeds --count $Count
    dotnet run --project tools/Roentgen.StructureLab -- stream-smoke --iterations 256
    dotnet run --project tools/Roentgen.StructureLab -- collision-smoke
}
finally { Pop-Location }
