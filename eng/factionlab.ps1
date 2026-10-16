param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
dotnet run --project tools/Roentgen.FactionLab -- @Arguments
exit $LASTEXITCODE
