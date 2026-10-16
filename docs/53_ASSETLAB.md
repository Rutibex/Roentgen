# AssetLab

AssetLab is the Phase 5 command-line authoring, validation, and diagnostic export tool.

## List the built-in catalog

```powershell
dotnet run --project tools/Roentgen.AssetLab -- catalog
```

This prints recipe part or layer counts and canonical fingerprints.

## Generate editable recipes

```powershell
dotnet run --project tools/Roentgen.AssetLab -- generate-samples --output samples/assets/generated
```

The resulting JSON files can be edited in any text editor and validated against the shipped schemas.

## Validate a recipe

```powershell
dotnet run --project tools/Roentgen.AssetLab -- validate-mesh --input samples/assets/scrap-rifle.mesh.json
dotnet run --project tools/Roentgen.AssetLab -- validate-material --input samples/assets/rusted-steel.material.json
```

Exit code `0` means valid. Exit code `2` means semantic validation errors were reported.

## Compile a mesh

```powershell
dotnet run --project tools/Roentgen.AssetLab -- compile-mesh `
  --input samples/assets/scrap-rifle.mesh.json `
  --output artifacts/assetlab/scrap-rifle.rgasset `
  --obj artifacts/assetlab/scrap-rifle.obj
```

The `.rgasset` file is the canonical compiler payload. OBJ is a diagnostic interoperability preview and is not a runtime dependency.

## Compile a material

```powershell
dotnet run --project tools/Roentgen.AssetLab -- compile-material `
  --input samples/assets/rusted-steel.material.json `
  --output artifacts/assetlab/rusted-steel.rgasset `
  --ppm artifacts/assetlab/rusted-steel.ppm
```

PPM is used because it requires no image library and preserves deterministic raw color bytes.

## Compile the built-in catalog

```powershell
./eng/assetlab.ps1
```

or:

```bash
./eng/assetlab.sh
```

The output includes:

- Canonical mesh payloads.
- Canonical material payloads.
- OBJ previews.
- PPM previews.
- A packed catalog atlas.

## Interactive game showcase

Run `Roentgen.App` to inspect the generated catalog through the Phase 4 renderer:

```powershell
dotnet run --project src/Roentgen.App
```

Controls remain:

- WASD: move.
- Space/Ctrl: vertical movement.
- Shift: faster movement.
- Tab: mouse look.
- F1: debug grid.
- Escape: release mouse or exit.

The showcase is the graphical preview foundation. A dedicated node-based visual editor remains future work; Phase 5 deliberately freezes the recipe and compiler contracts first.
