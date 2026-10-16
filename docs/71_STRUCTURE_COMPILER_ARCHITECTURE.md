# Structure Compiler Architecture

## Coordinate model

Structure plans use integer centimetres. Floor plans snap to a 50-centimetre grid. A blueprint stores a world origin plus local footprint, room, primitive and reservation coordinates. This separates deterministic local generation from placement in the regional world.

## Grammar catalog

The initial catalog contains ten purposes:

- Prospect Clinic;
- small residence;
- boarding house;
- salvage workshop;
- warehouse;
- market shelter;
- guard post;
- Blackwater Intake;
- Meridian Relay;
- conservative generic shelter.

Each grammar declares footprint and floor limits, structural system, required rooms, semantic adjacency, navigation dimensions, damage severity and hard generation budgets.

## Topology-first compilation

The compiler emits room nodes and semantic connections before structural primitives. A shared central circulation room guarantees a conservative baseline route. Required rooms receive deterministic door portals. Multi-floor structures receive a stable stair or ladder link.

## Physical compilation

The compiler emits foundations, floors, roofs, exterior walls, interior corridor walls and vertical traversal primitives. Geometry is grouped into exterior-shell and critical-interior streaming cells. Phase 7 deliberately uses orthogonal construction so collision, navigation and repair stay predictable.

## Damage

Damage events include fire, flooding, wall breach, looting, fatigue and partial repair. Damage may remove noncritical wall or roof primitives; it may not remove required floor or stair traversal. Quest-critical doors and reservations are protected by validation and repair.

## Provenance

Each blueprint records its parent reservation, campaign and domain seeds, generator ID and schema version, validation decisions and repair history. Every subobject has a stable ID derived from its semantic parent rather than runtime order.
