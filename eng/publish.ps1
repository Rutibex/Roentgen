[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'linux-x64')][string]$Runtime = 'win-x64',
    [string]$Version = '0.21.0-preview'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = "$root/artifacts/publish/$Runtime"
Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$root/src/Roentgen.App/Roentgen.App.csproj" -c Release -r $Runtime --self-contained true -o $output `
  -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:Version=$Version
python "$root/eng/write_build_manifest.py" $output $Runtime $Version
