param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
dotnet run --project tools/Roentgen.BalanceLab -- @Args
