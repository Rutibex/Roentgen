param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments=@("audit"))
$Root=(Resolve-Path (Join-Path $PSScriptRoot "..")).Path
dotnet run --project (Join-Path $Root "tools/Roentgen.ContentLab/Roentgen.ContentLab.csproj") -- @Arguments
exit $LASTEXITCODE
