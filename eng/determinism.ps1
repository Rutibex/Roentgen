[CmdletBinding()]
param([int]$Repeat = 5)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
python "$root/eng/validate_repository.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python "$root/eng/verify_golden_vectors.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
for ($run = 1; $run -le $Repeat; $run++) {
    Write-Host "Determinism test pass $run of $Repeat"
    dotnet test "$root/tests/Roentgen.Determinism.Tests/Roentgen.Determinism.Tests.csproj" -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
