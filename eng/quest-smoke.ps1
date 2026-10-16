$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
dotnet run --project "$Root/tools/Roentgen.QuestLab/Roentgen.QuestLab.csproj" -- validate
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project "$Root/tools/Roentgen.QuestLab/Roentgen.QuestLab.csproj" -- solve
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test "$Root/tests/Roentgen.Narrative.Tests/Roentgen.Narrative.Tests.csproj" -c Release
exit $LASTEXITCODE
