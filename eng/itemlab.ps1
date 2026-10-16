param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
$Root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $Root 'tools/Roentgen.ItemLab/Roentgen.ItemLab.csproj') -- @Arguments
