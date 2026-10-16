$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    dotnet run --project tools/Roentgen.StructureLab -- generate --seed 5931035436993955150 --output artifacts/structurelab
    dotnet run --project tools/Roentgen.StructureLab -- validate --seed 5931035436993955150
}
finally { Pop-Location }
