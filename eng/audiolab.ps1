param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
dotnet run --project tools/Roentgen.AudioLab/Roentgen.AudioLab.csproj -c Release -- @Arguments
exit $LASTEXITCODE
