$ErrorActionPreference="Stop"
$Root=(Resolve-Path (Join-Path $PSScriptRoot "..")).Path
python (Join-Path $Root "eng/verify_phase21_contracts.py")
python (Join-Path $Root "eng/verify_phase21_vectors.py")
python (Join-Path $Root "eng/verify_source_manifest.py")
dotnet restore (Join-Path $Root "Roentgen.sln") --locked-mode
dotnet build (Join-Path $Root "Roentgen.sln") -c Release --no-restore
dotnet test (Join-Path $Root "tests/Roentgen.Phase21.Tests/Roentgen.Phase21.Tests.csproj") -c Release --no-build
dotnet run --project (Join-Path $Root "tools/Roentgen.ContentLab/Roentgen.ContentLab.csproj") -c Release --no-build -- audit
