[CmdletBinding()]
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$arguments = @("$root/eng/bootstrap.py")
if ($Force) { $arguments += '--force' }
python @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
