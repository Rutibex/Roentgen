dotnet test tests/Roentgen.Audio.Tests/Roentgen.Audio.Tests.csproj -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet test tests/Roentgen.Audio.Playroma.Tests/Roentgen.Audio.Playroma.Tests.csproj -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet test tests/Roentgen.Audio.MonoGame.Tests/Roentgen.Audio.MonoGame.Tests.csproj -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet run --project tools/Roentgen.AudioLab/Roentgen.AudioLab.csproj -c Release -- validate
exit $LASTEXITCODE
