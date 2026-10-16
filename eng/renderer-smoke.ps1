[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
python "$root/eng/validate_repository.py"
if (-not $NoBuild) {
    dotnet tool restore
    dotnet restore "$root/Roentgen.sln"
    dotnet build "$root/src/Roentgen.App/Roentgen.App.csproj" -c Release --no-restore
}
dotnet run --project "$root/src/Roentgen.App/Roentgen.App.csproj" -c Release --no-build
