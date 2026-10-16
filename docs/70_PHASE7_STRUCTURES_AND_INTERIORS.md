# Phase 7 — Structures and Interiors

Phase 7 turns the permanent Phase 6 micro-region into a deterministic built environment. The authoritative compiler operates in this order:

1. landmark reservation;
2. settlement physical program;
3. district, street and parcel plan;
4. building grammar;
5. semantic room and door graph;
6. 50-centimetre spatial packing;
7. structural shell, openings and vertical links;
8. historical damage and salvage;
9. quest, loot and occupant reservations;
10. collision, navigation and streaming products;
11. validation and bounded deterministic repair.

Room topology is finalized before geometry. Damage is applied only after an intact structure is valid. Decorations never own gameplay topology.

## Implemented regression world

The fixed campaign seed generates:

- Prospect Clinic settlement with medical, market, workshop, residential and defensive parcels;
- Prospect Clinic main building and supporting settlement structures;
- Blackwater Intake, a multi-level damaged industrial ruin with alternate access;
- Meridian Relay, a compact utility structure with vertical access.

These sites remain the permanent vertical-slice foundation for Phase 8 and later gameplay systems.

## Runtime boundaries

`Roentgen.World.Structures` owns all authoritative plans and repairs. `Roentgen.Navigation` consumes the blueprint and emits immutable interior traversal products. `Roentgen.Physics` compiles static collision from the same structural primitives. `Roentgen.Rendering` batches immutable structure products and uploads them only on the render thread. `Roentgen.StructureLab` exposes seed reproduction, SVG plans, validation and soak commands.

## Exit gate

The phase gate is satisfied only when native CI can build and test the exact package, StructureLab can reproduce every failure, and the 10,000-structure batch has no critical accessibility or quest-reservation failures after bounded repair.
