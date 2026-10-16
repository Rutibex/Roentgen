# Terrain Streaming and Collision

## Chunk products

Each compiled chunk contains three deterministic LODs:

| LOD | Samples | Approximate spacing |
|---:|---:|---:|
| 0 | 65 × 65 | 2 m |
| 1 | 33 × 33 | 4 m |
| 2 | 17 × 17 | 8 m |

All LODs include the final edge sample and are derived from the same global height evaluator.

## Lifecycle

The coordinator uses these states:

`Unrequested → Requested → GeneratingCpu → AwaitingUpload → Resident → Dormant → Unloading`

`Cancelled` and `Failed` are explicit recovery states. A request carries an epoch. CPU results and GPU uploads must still match the current entry before they can become resident.

## Back-pressure

CPU generation is constrained by worker count, pending request count and resident CPU bytes. GPU work is constrained independently by pending operation count, pending bytes, per-frame operations, per-frame bytes and per-frame milliseconds.

The queue prioritizes near terrain above general world geometry and decoration. A full queue rejects new work rather than growing without bound. The app retains rejected products in a small retry backlog.

## Collision

Collision tiles are built from the exact LOD 0 height and surface arrays. The registry supports:

- bilinear height sampling;
- deterministic surface lookup;
- finite-difference surface normals;
- downward raycasts;
- kinematic capsule ground correction;
- shared-edge fallback while only one adjacent tile is resident.

Overhangs, caves, tunnels and structure geometry remain separate Phase 7 collision products.

## Telemetry

`TerrainStreamingSnapshot` exposes the epoch, focus chunk, state counts, pending queue and CPU resident bytes. Renderer telemetry exposes completed, skipped, failed, rejected and pending uploads plus byte and time budgets.

## Cancellation and stale work

Each CPU request owns a generation ticket and cancellation source. Moving beyond the dormant radius cancels the ticket, removes its residency entry, and permits a replacement request without waiting for the obsolete compiler job. The compiler checks cancellation before every LOD and every eighth row. A worker publishes output only when the entry still points to the same ticket and epoch.

## Mixed-LOD boundaries

All LODs retain byte-identical shared corner and edge samples. Render meshes also append a 2.5-metre deterministic skirt around each boundary. The shared samples preserve authoritative continuity; the skirt hides T-junction gaps when a dense edge meets a coarser resident neighbor.
