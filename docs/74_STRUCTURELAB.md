# StructureLab

Run StructureLab with:

```powershell
dotnet run --project tools/Roentgen.StructureLab -- help
```

Commands:

- `generate` writes a settlement map, per-structure floor plans, JSON inspection reports and a manifest;
- `settlement` exports the district, street and parcel plan as SVG;
- `floorplan` exports a selected structure as a multi-floor SVG;
- `inspect` writes complete rooms, doors, damage, navigation, validation and provenance data;
- `validate` validates the permanent regression campaign;
- `validate-seeds` performs deterministic batch validation;
- `stream-smoke` stresses bounded layer streaming and unloads;
- `collision-smoke` compiles and queries all static structure collision.

Examples:

```powershell
dotnet run --project tools/Roentgen.StructureLab -- generate --seed 5931035436993955150 --output artifacts/structurelab
dotnet run --project tools/Roentgen.StructureLab -- floorplan --structure blackwater --output artifacts/blackwater.svg
dotnet run --project tools/Roentgen.StructureLab -- validate-seeds --count 1000
```

Every output includes deterministic IDs and checksums so a bug report can be reproduced from one seed and one structure name.
