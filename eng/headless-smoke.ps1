[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts/headless-smoke'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item $out -ItemType Directory -Force | Out-Null
$project = Join-Path $root 'src/Roentgen.Headless/Roentgen.Headless.csproj'
dotnet run --project $project -c Release -- run `
  --seed 7351982471 --ticks 10000 --entities 64 --checksum-interval 600 `
  --replay-out "$out/smoke.replay.jsonl" `
  --snapshot-out "$out/smoke.rgsnap" `
  --telemetry-out "$out/smoke.telemetry.json"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project $project -c Release -- replay `
  --input "$out/smoke.replay.jsonl" `
  --snapshot-out "$out/replayed.rgsnap" `
  --telemetry-out "$out/replay.telemetry.json"
exit $LASTEXITCODE
