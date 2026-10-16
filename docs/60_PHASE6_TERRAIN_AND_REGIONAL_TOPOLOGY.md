# Phase 6 — Terrain and Regional Topology

Phase 6 converts the Phase 5 procedural asset showcase into the first permanently explorable generated Roentgen micro-region. The authoritative world pipeline remains CPU-only and deterministic; MonoGame receives immutable terrain products through the rendering bridge.

## Delivered scope

- 2 × 2 kilometre deterministic micro-region profile.
- Whole-region topology fields for elevation, moisture, contamination, industrial disturbance, settlement suitability and region identity.
- Three strongly differentiated prototype identities: Glass Basin, Broken Meridian and Blackwater Works, while retaining all six canonical region-family identifiers.
- Regional A* road/trail graph connecting a settlement, ruin and relay reservation.
- Stable landmark reservations with target elevation, clearance, slope and access requirements.
- 128 metre terrain chunks with 65×65, 33×33 and 17×17 LODs.
- Shared global integer height sampling, byte-identical seams and collision derived from LOD 0.
- Bounded asynchronous CPU generation and bounded prioritized render-thread uploads.
- Chunk lifecycle telemetry, cancellation epochs, stale upload rejection, dormancy and unloading.
- Distant regional mesh, streamed near terrain, road/reservation debug overlays and generated Phase 5 props.
- WorldLab command-line diagnostics, schemas, samples, seed validation and CI contract checks.

## Repository ownership

`Roentgen.World` owns generation profiles, coordinates, scalar fields, regional topology, roads, reservations, chunk compilation, validation and streaming coordination. It has no MonoGame dependency.

`Roentgen.Physics` owns terrain height, normal, surface, downward-ray and capsule-ground queries.

`Roentgen.Rendering` owns terrain mesh conversion, GPU residency and the bounded upload queue. It never generates authoritative terrain.

`Roentgen.App` composes the micro-region scene and presents diagnostics.

`Roentgen.WorldLab` generates summaries and maps, inspects chunks, compiles region manifests and validates seed batches.

## Deterministic generation order

1. Validate `WorldGenerationProfile`.
2. Derive the `world.topology` seed domain.
3. Generate low-resolution scalar fields over the complete region.
4. Classify region families and settlement suitability.
5. select stable anchor nodes and landmark reservations.
6. Route roads and trails over topology costs.
7. Validate topology connectivity and reservations.
8. Compile requested terrain chunks from the shared global evaluator.
9. Validate chunk shape and seams.
10. hand immutable CPU products to collision and rendering consumers.

Chunk request order, worker count and LOD selection do not alter authoritative heights.

## Runtime controls

- `WASD`, `Space`, `Left Control`: fly through the region.
- `Left Shift`: fast movement.
- `Tab`: toggle mouse look.
- `F1`: toggle roads, reservations and chunk-state diagnostics.
- `Escape`: exit.

## Acceptance gate

Phase 6 is complete when a deterministic outdoor micro-region can be generated, inspected, loaded, traversed, unloaded and regenerated from its seed; terrain, collision, roads, masks and reservations remain consistent across chunk boundaries and generation order; and the streaming system remains within fixed CPU, GPU-upload and queue budgets.
