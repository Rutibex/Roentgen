# Build, test and package Roentgen

## Prerequisites

- SDK from [`global.json`](../global.json): .NET 10.0.302 (roll-forward to a later SDK feature band permitted). Confirm using `dotnet --info`.
- MonoGame DesktopGL framework and other managed dependencies from NuGet. Restore requires network on first setup.
- Windows or Linux with working graphics drivers/OpenGL for the interactive app. Headless tools can run without a display.
- Python 3 for repository validation scripts. `eng/validate_repository.py` also imports `jsonschema` and `yaml` (PyYAML); install with `python -m pip install jsonschema PyYAML` if missing.
- MonoGame content tool pinned in [`.config/dotnet-tools.json`](../.config/dotnet-tools.json), only needed for optional shader compilation.

## Compile and launch

Run all commands from the repository root.

```sh
dotnet restore Roentgen.sln
dotnet build src/Roentgen.App/Roentgen.App.csproj -c Release
dotnet run --project src/Roentgen.App/Roentgen.App.csproj -c Release --no-build
```

If parallel compilation causes a resource-related failure on a small machine, use `dotnet build src/Roentgen.App/Roentgen.App.csproj -c Release -m:1 -p:UseSharedCompilation=false`.

### Optional shaders

Normal builds use the fallback renderer without compiling custom effects. To explicitly build `.fx` effects, restore the local tool and enable `RoentgenCompileCustomEffects`:

```sh
dotnet tool restore
dotnet build src/Roentgen.App/Roentgen.App.csproj -c Release -p:RoentgenCompileCustomEffects=true
```

## Validation

```sh
python eng/verify_source_manifest.py
python eng/verify_golden_vectors.py
python eng/verify_replay_fixture.py
python eng/validate_repository.py

dotnet build Roentgen.sln -c Release
dotnet test Roentgen.sln -c Release
```

`eng/test.ps1` and `eng/test.sh` provide additional test orchestration. CI includes targeted subsystem checks. Source validators are supplementary; **they do not prove rendering quality, game completion, or player accessibility**.

The tracked `eng/source-manifest.sha256` is a checksum fixture. When intentional source changes are complete, regenerate it with `python eng/write_source_manifest.py` and commit both the changed files and the updated manifest. Do not generate the manifest as an intermediate step while actively editing files.

## Publish executable builds

To build **self-contained** binaries, run on the platform you intend to ship (cross-targeting depends on runtime packs):

```sh
# Linux shell
bash eng/publish.sh linux-x64 0.21.0-preview

# Windows PowerShell
./eng/publish.ps1 -Runtime win-x64 -Version 0.21.0-preview
```

Result: `artifacts/publish/<rid>/` plus a generated build manifest. Source archives and `artifacts/` outputs are distinct: this repository ZIP contains source and required runtime content, **not** a prebuilt executable.

## Releasing on GitHub

The tag-triggered `Publish` workflow builds Windows/Linux binary archives, computes SHA-256 hashes and, if all jobs pass, attaches them to a GitHub Release. Use tags such as `v0.21.0-preview`. Check the [release checklist](RELEASE_CHECKLIST.md) before creating a public tag. To preview CI artifacts without creating a public release, run the workflow manually.

## Common issues

See [Troubleshooting](TROUBLESHOOTING.md). Avoid committing `bin/`, `obj/`, `artifacts/`, `.packages/`, saves, runtime logs or local screenshots. The SoundFont and authored JSON content **must** stay in the repository.
