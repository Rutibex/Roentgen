# Phase 4 — 3D Renderer Foundation

## Status

Implemented in repository version `0.4.0-phase4`.

## Purpose

Phase 4 establishes the first production graphics layer while preserving the Phase 3 rule that authoritative simulation remains headless and presentation-neutral. The renderer consumes immutable presentation snapshots, owns every MonoGame graphics resource on the render thread, and provides a bounded upload path for CPU-generated assets.

## Delivered systems

- HiDef MonoGame DesktopGL graphics host.
- Perspective camera and free-flight smoke-scene controller.
- Immutable `RenderFrameSnapshot` presentation boundary.
- CPU mesh representation and procedural box, cylinder, and terrain generators.
- Render-thread guard and bounded resource-upload queue.
- GPU vertex/index buffer ownership through `GpuMesh`.
- Stable opaque render queue with frustum culling.
- Forward-lit material shader with vertex color, texture, sunlight, emissive response, fog, and shadow reception.
- Directional shadow-map pass.
- Render-target lifecycle management.
- Tone mapping, gamma correction, and vignette post-process.
- BasicEffect fallback when custom content effects are unavailable.
- Debug line renderer.
- Frame telemetry and window-title diagnostics.
- Renderer smoke scene built entirely from generated geometry.

## Architectural boundary

Simulation never references MonoGame. Future simulation systems publish plain presentation descriptions that a presentation adapter converts to `RenderFrameSnapshot`. The renderer may interpolate or omit presentation data, but it may never mutate authoritative state.

## Smoke-scene controls

- `WASD`: horizontal movement.
- `Space` / `Left Control`: vertical movement.
- `Left Shift`: faster movement.
- `Tab`: toggle captured mouse look.
- `F1`: toggle debug grid and axes.
- `Escape`: exit.

## Deferred to later phases

- Procedural asset DSL and atlas compiler: Phase 5.
- Terrain chunk streaming: Phase 6.
- Interior portals and settlement structures: Phase 7.
- Character skinning and animation rendering: Phase 9.
- Roentgen Focus diagnostic passes: Phase 11.
- Final cascaded shadows, decals, particles, water, and production optimization remain incremental renderer work.
