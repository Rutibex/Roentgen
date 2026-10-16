[CmdletBinding()]
param(
    [int]$Seeds = 10,
    [UInt64]$Ticks = 10000,
    [int]$Entities = 64,
    [int]$Repetitions = 2
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts/soak'
New-Item $out -ItemType Directory -Force | Out-Null
dotnet run --project "$root/src/Roentgen.Headless/Roentgen.Headless.csproj" -c Release -- soak `
  --start-seed 1 --seeds $Seeds --ticks $Ticks --entities $Entities --repetitions $Repetitions `
  --report "$out/soak.json"
exit $LASTEXITCODE
