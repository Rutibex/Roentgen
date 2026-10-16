$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
dotnet run --project "$Root/tools/Roentgen.QuestLab/Roentgen.QuestLab.csproj" -- @args
exit $LASTEXITCODE
