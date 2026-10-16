param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project "$root/tools/Roentgen.MusicLab/Roentgen.MusicLab.csproj" -- @Arguments
