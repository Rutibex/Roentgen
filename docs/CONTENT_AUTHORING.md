# Content authoring and tools

## Source of truth

Canonical authored content lives in `content/roentgen/phase21-launch-content.json` with JSON Schemas in `schemas/`. This source pack contains 1,499 budgeted entries including region/settlement/structure grammars, factions, NPCs, creatures, robots, equipment, crafting, perks, backgrounds, quests, campaigns, dialogue, tutorials, audio profiles and music bindings. The existing catalog ID and reference validation should be preserved when adding content.

When changing IDs or cross-references, validate dependencies rather than editing isolated records blindly. Keep determinism and replay fixture compatibility in mind; changing generator behavior is a deliberate compatibility change.

## Inspect the catalog

```sh
# View available subcommands
dotnet run --project tools/Roentgen.ContentLab -- --help

# Examine the catalog
dotnet run --project tools/Roentgen.ContentLab -- audit

# Check static code/content invariants
python eng/verify_phase21_contracts.py
python eng/verify_phase21_vectors.py
```

Different CLI utilities may have their own argument syntax; consult `tools/<name>/Program.cs` when `--help` is not supported.

## Laboratories

| Tool | Scope |
|---|---|
| `ContentLab` | Content audit, completeness, reference inspection |
| `AssetLab` | Procedural meshes and materials |
| `WorldLab` / `StructureLab` | Terrain and structures |
| `CreatureLab` / `AgentLab` | Characters and AI/navigation |
| `CombatLab` / `BalanceLab` / `ItemLab` | Damage, progression, items and economy |
| `FactionLab` / `QuestLab` / `CampaignLab` | Factions, quests and campaign systems |
| `AudioLab` / `MusicLab` | Procedural sound and music |
| `InterfaceLab` / `FocusLab` | UI geometry and tactical Focus budgets |

Every tool is a separate console project in `tools/`; the game does not require these development executables to run.

## Asset boundaries

Do **not** delete `content/music/soundfonts/default.sf2`, the JSON catalog, or generated-but-checked-in fixture vectors merely because they were built by generation tools: the project uses these as runtime inputs and reproducibility references. Do not commit generated executables, logs, private settings, save files, or cache output.
