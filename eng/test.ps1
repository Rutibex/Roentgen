$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
python "$root/eng/validate_repository.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test "$root/Roentgen.sln" -c Release --collect:"XPlat Code Coverage" --results-directory "$root/artifacts/test-results"
