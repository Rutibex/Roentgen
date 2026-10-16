$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Remove-Item "$root/artifacts", "$root/.packages" -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $root -Directory -Recurse | Where-Object { $_.Name -in @('bin', 'obj') } | Remove-Item -Recurse -Force
