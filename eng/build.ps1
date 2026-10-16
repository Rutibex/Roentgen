[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
python "$root/eng/bootstrap.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python "$root/eng/validate_repository.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
python "$root/eng/verify_golden_vectors.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet tool restore
dotnet restore "$root/Roentgen.sln" --locked-mode
if ($LASTEXITCODE -ne 0) { dotnet restore "$root/Roentgen.sln" }
dotnet build "$root/Roentgen.sln" -c Release --no-restore
dotnet test "$root/Roentgen.sln" -c Release --no-build --no-restore
