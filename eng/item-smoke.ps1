$Root = Split-Path -Parent $PSScriptRoot
python (Join-Path $Root 'eng/verify_item_contracts.py')
python (Join-Path $Root 'eng/verify_item_vectors.py')
dotnet test (Join-Path $Root 'tests/Roentgen.Items.Tests/Roentgen.Items.Tests.csproj') --configuration Release
