param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
dotnet run --project tools/Roentgen.CampaignLab -- @Arguments
exit $LASTEXITCODE
