# Architecture and repository map

Roentgen is a modular C#/.NET 10 solution. Most gameplay calculations live in platform-neutral libraries; `Roentgen.App` is the MonoGame DesktopGL presentation and input host.

## Top-level directories

| Path | Role |
|---|---|
| `src/Roentgen.App` | Desktop game entry, rendering, input, player UI integration |
| `src/Roentgen.Core`, `Roentgen.Contracts`, `Roentgen.Simulation`, `Roentgen.Headless`, `Roentgen.Persistence` | Stable identity, deterministic data contracts, authoritative state, replay/save tools |
| `src/Roentgen.World`, `Structures` (within World), `Navigation`, `Physics`, `Generation`, `Assets` | Regional topology, structure generation, collision, streamed and generated assets |
| `src/Roentgen.Entities`, `Animation`, `AI`, `Combat`, `Rpg`, `Items`, `Focus` | Creatures, animation, combat, progression, inventories, tactical action queue |
| `src/Roentgen.Factions`, `Narrative`, `Campaign`, `Content` | Society simulation, evidence/dialogue/quests, campaign progression, canonical content |
| `src/Roentgen.Audio*`, `Music*`, `Playroma.*`, `PlayromaMusic.*` | Semantic audio, music synthesis, SoundFont playback and MonoGame audio bridge |
| `src/Roentgen.UI`, `Rendering` | Menu/presentation state and rendering bridges |
| `content/`, `schemas/`, `samples/` | Shipping assets, authored content, schema definitions, test/example source records |
| `tests/`, `tools/`, `eng/` | Automated tests, diagnostic/authoring CLI programs, build/integrity scripts |
| `docs/`, `docs/adr/` | Technical documentation and architecture decisions |

The authoritative simulation is designed for stable IDs, seeded randomness, ordered events, deterministic regeneration and checked persistence. UI and audio bridge read snapshots/events rather than owning gameplay state. GPU effects are optional; the default app can use MonoGame fallback rendering.

## Content pipeline

`content/roentgen/phase21-launch-content.json` holds the canonical catalog. `Roentgen.Content` validates IDs and references and loads/compiles records for domain-specific adapters (RPG, items, world, factions, narrative, campaign, audio, music). See [`210_PHASE21_CONTENT_COMPLETION.md`](210_PHASE21_CONTENT_COMPLETION.md) and [`211_PHASE21_CONTENT_PIPELINE.md`](211_PHASE21_CONTENT_PIPELINE.md).

## World generation versus playable world

The launch profile in code spans 8,192 m per side, but the current interactive regression campaign uses a **smaller micro-region**. Both are useful for developers: the launch profile demonstrates capacity/configuration, while the micro-region is the current interactive smoke environment. Neither the presence of a content record nor a passing unit test implies that its full player journey is implemented.

## Design sources

- [`12_ARCHITECTURE_BOUNDARIES.md`](12_ARCHITECTURE_BOUNDARIES.md): library boundaries.
- [`21_DETERMINISM_PROTOCOL.md`](21_DETERMINISM_PROTOCOL.md): seeded randomness and canonical hashing.
- [`41_RENDERING_ARCHITECTURE.md`](41_RENDERING_ARCHITECTURE.md): renderer layout and GPU ownership.
- [`200_PHASE20_FULL_INTERFACE_ACCESSIBILITY.md`](200_PHASE20_FULL_INTERFACE_ACCESSIBILITY.md): UI shell.
- [`201_PHASE20_SAVE_AND_RECOVERY.md`](201_PHASE20_SAVE_AND_RECOVERY.md): save format and recovery.
- [`adr/`](adr/): maintained architectural decision context.

## Compatibility policy

Avoid silently migrating generator-dependent saves. New generator versions can change topology and player collision contexts, so loading incompatible saves must fail clearly without deleting prior user data.
