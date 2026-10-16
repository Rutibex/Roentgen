# Structure Streaming, Navigation and Collision

## Interior streaming

`StructureStreamingCoordinator` uses three priority-banded bounded request channels sharing one global request-capacity semaphore, plus a bounded result channel. Priorities of 100 or more are critical prefetch, nonnegative priorities are normal traversal work, and negative priorities are background decoration. Workers always dequeue the highest available band. Requests are keyed by structure, layer and epoch; newer epochs supersede stale products without allowing a rejected request to invalidate accepted work. Resident CPU memory is budgeted. Products are split into exterior shell, critical interior and decoration layers.

The current application eagerly requests the shell and critical layers for the permanent regression sites. The same contracts support portal-driven prefetch in Phase 8.

## Navigation emission

The structure generator directly emits semantic navigation rather than reconstructing it from rendered triangles. The navigation product contains cells, door portals, vertical links, restricted areas, cover points and flanking links. Route validation tests every quest-critical room from the exterior under the declared traversal conditions.

## Collision

Static collision is compiled from the same authoritative structure primitives used by rendering. Ordinary walls, floors and roofs become local boxes. Doors retain separate semantic state. The registry supports raycasts and capsule sweeps across all resident structures. Full rigid-body simulation remains outside Phase 7.

## Rendering

Structure primitives are batched by streaming layer and material. GPU resource creation and disposal occurs through the existing bounded render-thread queue. Exterior and interior products remain separately addressable so later portal visibility can suppress unseen interior cells.
