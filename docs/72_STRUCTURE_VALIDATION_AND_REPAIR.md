# Structure Validation and Repair

Validation is layered so a generated failure names the stage that must fix it.

## Invariants

- all persistent IDs are nonempty and unique;
- every room lies inside its footprint and floor;
- ordinary rooms do not overlap;
- at least one usable exterior entrance exists;
- all rooms and protected reservations are reachable;
- alternate exterior access exists when required;
- doors and vertical links reference valid rooms;
- required floor and stair primitives survive damage;
- every primitive belongs to a streaming cell;
- an exterior shell and critical cell exist for every floor.

## Typed repair

Validation issues expose candidate repairs. The engine chooses the lowest-cost deterministic repair first:

1. restore an absent critical door;
2. restore a traversal primitive;
3. reconnect an unreachable room;
4. add an exterior passage;
5. relocate an invalid reservation;
6. apply the conservative safe fallback only after bounded attempts.

Every repair records its attempt number, subject, summary and before/after checksums. The repair engine rebuilds only the affected immutable collections and retains the original blueprint ID and generation ancestry.

## Batch validation

`StructureBatchValidator` advances through deterministic campaign seeds until the requested structure count is reached. It records generated and valid counts, fallback use, critical failures, room and primitive totals and an aggregate checksum. The release target is 10,000 structures.
