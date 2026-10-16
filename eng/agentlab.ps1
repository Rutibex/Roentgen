param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
dotnet run --project tools/Roentgen.AgentLab -- @Arguments
exit $LASTEXITCODE
