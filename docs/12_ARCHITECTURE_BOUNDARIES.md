# Architecture Boundaries

## Dependency direction

Dependencies point toward contracts and core services, never toward the executable. `Roentgen.App` composes the system but contains no reusable game rules.

## MonoGame boundary

Only these projects may reference MonoGame:

- `Roentgen.App`
- `Roentgen.Rendering`
- `Roentgen.UI`

Physics, navigation, simulation, generation, combat, narrative, persistence, audio abstractions, and animation models remain ordinary .NET libraries. This preserves headless testing and prevents graphics-device ownership from leaking into gameplay.

## External integration boundary

Roentgen projects must not reference Playroma WinForms, NAudio hosts, workstation UI, or SoundFont playback projects. The Phase 1 bridges admit only the platform-neutral source libraries declared in `eng/dependencies.lock.json`.

## Randomness boundary

All future procedural behavior must derive a named `DomainSeed` from a campaign seed. A global mutable random generator is prohibited. Domain names are stable serialized API and require migration planning when renamed.

## Persistence boundary

Persistence serializes canonical state and version metadata. It must not serialize MonoGame graphics resources, transient caches, delegates, threads, or external audio renderer objects.

## Thread ownership baseline

- Main thread: MonoGame window, graphics-device operations, input collection, composition.
- Simulation worker or fixed-step loop: authoritative gameplay state.
- Generation workers: CPU-only procedural data.
- Audio worker: realtime Playroma rendering in later phases.
- Render upload queue: bounded transfer of generated CPU data to GPU resources.

## Phase 3 authoritative boundary

`Roentgen.Headless`, `Roentgen.Simulation`, `Roentgen.Entities`, `Roentgen.Core`, and `Roentgen.Persistence` must remain free of MonoGame dependencies. The future `Roentgen.App` client may enqueue commands and consume immutable presentation snapshots, but it must not become the owner of gameplay state.


## Phase 4 rendering boundary

`Roentgen.Rendering` and `Roentgen.App` may reference MonoGame. Authoritative simulation projects must not. The renderer accepts immutable presentation snapshots and owns GPU resources exclusively on the render thread. CPU generation jobs cross into graphics through a bounded upload queue.

## Procedural asset boundary

`Roentgen.Assets` owns platform-neutral recipes, compiled CPU geometry, texture bytes, LODs, collision proxies, atlas data, fingerprints, and derived caches. It may reference deterministic core and generation libraries but must not reference MonoGame.

`Roentgen.Rendering` is the only runtime library that converts compiled assets into MonoGame buffers and textures. GPU resources are never serialized, checksummed as authoritative state, or returned to generation workers.
