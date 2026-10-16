param(
    [string]$Output = "artifacts/assetlab"
)
$ErrorActionPreference = "Stop"
dotnet restore Roentgen.sln --locked-mode
if ($LASTEXITCODE -ne 0) { dotnet restore Roentgen.sln }
dotnet run --project tools/Roentgen.AssetLab --configuration Release -- compile-catalog --output $Output
