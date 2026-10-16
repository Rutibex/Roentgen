# WorldLab

WorldLab is the Phase 6 deterministic topology and terrain diagnostic executable.

## Commands

```powershell
dotnet run --project tools/Roentgen.WorldLab -- summary --seed 0x524F454E5447454E
dotnet run --project tools/Roentgen.WorldLab -- export-map --layer regions --output artifacts/worldlab/regions.ppm
dotnet run --project tools/Roentgen.WorldLab -- export-map --layer elevation --output artifacts/worldlab/elevation.ppm
dotnet run --project tools/Roentgen.WorldLab -- inspect-chunk --x 0 --z 0
dotnet run --project tools/Roentgen.WorldLab -- compile-region --output artifacts/worldlab/chunks
dotnet run --project tools/Roentgen.WorldLab -- validate-seeds --count 20 --start 1
```

`summary` reports the topology ID and checksum, field dimensions, region distribution, roads and reservations.

`export-map` writes a dependency-free binary PPM diagnostic map. Roads and reservations are overlaid on every layer.

`inspect-chunk` reports stable identity, checksum, memory estimate, height ranges and surface distribution for every LOD.

`compile-region` compiles every chunk and writes a deterministic manifest rather than serializing the full generated height arrays.

`validate-seeds` validates topology and representative seams across a seed batch. A nonzero exit code is returned on failure for CI use.
