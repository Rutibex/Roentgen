param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
dotnet run --project tools/Roentgen.CombatLab -- @Arguments
