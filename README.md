# Roentgen

**Procedural post-apocalyptic first-person RPG prototype** built with C# and MonoGame. Explore generated regions and buildings, experiment with combat and targeted-action Focus, interact with factions and quests, and test deterministic campaign systems.

> **Development preview, not a finished game.** This repository is a source distribution of the current playable prototype. Many domain systems, content catalogs and editor tools exist, but the large-world campaign and complete end-to-end player experience have not been visually or playthrough-verified. See [Current status](#current-status).

## Highlights

- **Generated world:** deterministic regional terrain, roads, buildings, interiors, structural damage and streamed content.
- **Character and combat:** first-person movement, inventory/equipment, skills, perks, body-part combat and a tactical Roentgen Focus action queue.
- **Living systems:** factions, settlements, trade, dialogue, companions, quests and a procedural campaign director.
- **Player interface:** title menu, character creation, settings, accessibility, map, journal, crafting and save/load screens.
- **Audio:** integrated Playroma Sound, adaptive Playroma Music and a bundled GeneralUser GS SoundFont.
- **Modular tooling:** headless simulation, repeatable seeds and specialized content/asset/world/combat/quest testing tools.

## Quick start

**Requirements:** Windows or Linux desktop with compatible OpenGL drivers, the **.NET 10 SDK** specified in [`global.json`](global.json), and access to NuGet for the first restore. A graphics/audio-capable machine is required to evaluate actual play. MonoGame DesktopGL is restored through NuGet.

From the repository root:

```powershell
dotnet restore Roentgen.sln
dotnet build src/Roentgen.App/Roentgen.App.csproj -c Release
dotnet run --project src/Roentgen.App/Roentgen.App.csproj -c Release --no-build
```

On Linux/macOS, use the same `dotnet` commands in a shell. macOS has not been verified by this packaging pass. **Start a new campaign:** changes to generated world geometry can make older save files incompatible; the game should reject rather than migrate those older files. Keep an older build to open older saves.

If the SDK is not installed, download it from [Microsoft's .NET website](https://dotnet.microsoft.com/download/dotnet/10.0). For platform issues see [Troubleshooting](docs/TROUBLESHOOTING.md).

## Controls

Default keyboard and mouse bindings (configurable in Settings):

| Action | Control | Action | Control |
|---|---|---|---|
| Move | W A S D | Look | Mouse |
| Interact | E | Jump | Space |
| Sprint | Left Shift | Crouch | Ctrl / C |
| Attack / aim | Left / Right Mouse | Reload | R |
| Weapons | 1 / 2 / 3 | Inventory / equipment | I / O |
| Character / skills / perks | P / K / L | Regional map / journal | M / J |
| Factions / crafting | F / B | Dialogue / trade | Q / T |
| Companions | H | Roentgen Focus | V |
| Quick save / load | F5 / F9 | Pause / menu | Escape |

Some actions are context-sensitive and can be unavailable outside a valid interaction range. See [Player guide](docs/PLAYER_GUIDE.md).

## Current status

The underlying repository has extensive systems and a large data-driven catalog (including an 8 × 8 km launch-world profile), but **the currently playable world is still the smaller regression/micro-region**. The expanded launch profile is not proof of a populated production-sized world. The newest upgrade improves generated structures, camera movement, material rendering, menu text, input and quest evidence integration; architectural variety, complete campaign routes, in-game visual quality and performance still need verification. The structure tests and app build were reported as passing in the supplied handoff; this packaging environment cannot independently compile/run MonoGame because the .NET SDK and graphics device are unavailable here.

A newly restored checkout should be viewed as an **early access source project** rather than an installer or promised complete RPG.

## Building, testing and utilities

```powershell
# Application build
dotnet build Roentgen.sln -c Release

# Tests (the full suite is extensive)
dotnet test Roentgen.sln -c Release

# Static repository checks (requires Python 3 + PyYAML + jsonschema)
python eng/validate_repository.py
python eng/verify_source_manifest.py

# Example authoring/inspection utility
dotnet run --project tools/Roentgen.ContentLab -- audit
```

Source projects and shared runtime packages live in `src/`, test projects and fixtures in `tests/`, command-line laboratories in `tools/`, build/check scripts in `eng/`, authoring records in `content/`, schemas in `schemas/`, and specifications in `docs/`. See [Build and test](docs/BUILDING.md) and [Architecture](docs/ARCHITECTURE.md).

## Documentation

- [Player guide / controls](docs/PLAYER_GUIDE.md)
- [Build, test and publishing](docs/BUILDING.md)
- [Architecture and project structure](docs/ARCHITECTURE.md)
- [Content authoring and tools](docs/CONTENT_AUTHORING.md)
- [Troubleshooting and known limitations](docs/TROUBLESHOOTING.md)
- [Contributing](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md)
- [Release notes](CHANGELOG.md) / [release checklist](docs/RELEASE_CHECKLIST.md)

Detailed subsystem specifications and architecture decision records are retained in `docs/` and `docs/adr/` for developers; earlier interim acceptance and repair reports are intentionally excluded.

## License and credits

MIT license © 2026 Michael Robinson. See [`LICENSE`](LICENSE). Source code for Playroma Sound and Playroma Music includes their original MIT license notices. The bundled GeneralUser GS SoundFont and DejaVu-derived font assets have **separate terms**; read [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) before redistribution.

Roentgen is an independent project and is not affiliated with any other RPG franchise.
