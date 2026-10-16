# Technical Budgets

## 1. Platform baseline

- Language: C#.
- Framework: MonoGame DesktopGL.
- Initial runtime target: .NET 10, subject to Phase 1 compatibility verification.
- Primary release: Windows x64.
- Secondary target: Linux x64.
- Rendering API: platform path provided by MonoGame DesktopGL.

## 2. Frame and simulation budgets

- Fixed simulation: 60 Hz.
- Target display frame rate: 60 FPS at 1080p on the reference system.
- Simulation budget: 6 ms average, 10 ms 99th percentile.
- Render submission and CPU preparation: 6 ms average, 10 ms 99th percentile.
- GPU frame: 14 ms target, 16.6 ms hard 60 FPS envelope.
- Streaming work on main thread: less than 1 ms average per frame.
- Individual generation upload batch: bounded to prevent visible hitching.

## 3. Memory budgets

- Typical total process memory: below 2 GB.
- Main world resident CPU generation data: below 512 MB.
- GPU textures and render targets: below 768 MB at 1080p default settings.
- Generated mesh cache: adaptive, with eviction.
- Audio buffers and graphs: below 256 MB under peak combat and music load.
- Save load working memory: below 256 MB.

## 4. World streaming budgets

- Critical terrain/collision ready before chunk activation.
- Decorative content may arrive later.
- No player fall-through during generation.
- No blocking disk read on the render thread.
- No synchronous full-settlement generation during ordinary movement.
- Neighboring chunks requested before the player reaches the boundary.

## 5. Generation budgets

Representative targets on the reference system:

- Terrain chunk CPU generation: under 8 ms average on a worker.
- Terrain collision proxy: under 4 ms average.
- Ordinary prop mesh: under 2 ms uncached.
- Weapon recipe: under 5 ms uncached.
- Humanoid mesh and equipment assembly: under 20 ms uncached.
- Ordinary building structure: under 40 ms.
- Major structure: under 150 ms with staged generation.
- Settlement layout: under 500 ms off-thread, with progressive content generation.

Budgets are measured independently from GPU upload.

## 6. Audio budgets

- Realtime render block must allocate no managed memory in steady state.
- No graph compilation on the audio render path.
- Control queues must be bounded.
- Audio underrun count: zero in release validation.
- Peak active physical voices: configured by quality profile.
- Virtualized voices preserve semantic state without full rendering.
- Audio CPU target: below 10% of one reference CPU core average, below 20% peak.

## 7. Save budgets

- Typical campaign save: below 5 MB compressed.
- Large late-game save warning threshold: 20 MB.
- Save operation: target below 500 ms without blocking presentation.
- Autosave must use snapshot or staged serialization.
- Minimum rolling backups: 3.
- Atomic replacement required.

## 8. Distribution budgets

- Procedural recipes, templates, and data: under 25 MB compressed.
- Self-contained Windows package target: under 150 MB.
- No required conventional model library.
- No required conventional texture library.
- No required recorded SFX library.
- No required SoundFont.
- Generated cache is outside the installation and user-configurable.

## 9. Determinism budgets

- Same build, seed, and command log: identical gameplay checksum.
- Cross-platform deterministic gameplay target: identical logical state where floating-point differences are excluded or normalized.
- Rendering is not required to be pixel-identical across GPUs.
- Audio output may differ at the final device layer, but semantic cue identity and composition must remain deterministic.

## 10. Test budgets

### Per commit

- Unit tests under 5 minutes.
- Representative determinism seeds: at least 20.
- Save round trips.
- Core generator smoke tests.

### Nightly

- Hundreds of worlds.
- Thousands of structures.
- Thousands of combat encounters.
- Full quest-graph validation.
- Long audio stress run.
- Navigation traversal agents.

### Release suite

- Tens of thousands of world seeds.
- Full campaign auto-completion suite.
- Save migration matrix.
- 12-hour soak tests.
- Clean-install packaging tests.
- Hardware compatibility matrix.

## 11. Diagnostics budgets

Every critical failure report should include:

- Build version.
- Constitution version.
- Campaign seed.
- Generator versions.
- Stable object IDs.
- Domain seeds.
- Recent command log.
- World checksum.
- Validation and repair history.
