# Phase 14 — Navigation and Full AI

Phase 14 replaces direct-to-target combat movement and radius omniscience with deterministic navigation, evidence-based perception, utility decisions, morale, surrender, schedules, simulation tiers, and companion orders.

## Navigation contracts

`Roentgen.Navigation` defines typed agent capabilities, navigation locations, requests, route steps, failures, traversal costs, dynamic obstacles, and canonical route checksums. Equal-cost searches use stable-ID tie breaking.

Outdoor navigation supports regional road graphs and chunk-emitted traversal graphs. Routes connect arbitrary endpoints to the hierarchical graph and return the requested final destination. Dynamic blockers and cost volumes increment the navigation revision and invalidate stale routes.

Interior navigation consumes the structure generator's room cells, portals, doors, stairs, ladders, drops, restricted areas, cover points, and flanking links. `HierarchicalNavigationService` joins exterior and interior paths through emitted structure portals.

## Movement

`NavigationMotor` turns routes into authoritative fixed-point character movement. It uses the shared capsule-cast and ground-probe collision world, obeys profile speed and clearance, applies deterministic stable-ID local avoidance against nearby agents, detects lack of progress, and performs bounded deterministic replanning. Door steps invoke the same `PlayerSimulationRuntime` door authority used by the player; closed, locked, jammed, barricaded, opening, destroyed, and missing states therefore alter path validity and execution consistently. Ladder, stair, ramp, and collapsed-drop steps perform typed vertical movement rather than horizontal teleportation.

## Perception and memory

Agents receive visual evidence only inside range, field of view, lighting, concealment, and line of sight. Semantic stimuli represent footsteps, gunshots, explosions, doors, alarms, speech, machinery, creature calls, and broken objects. Awareness stores confirmed targets, last-known and last-heard positions, confidence, suspicion, evidence age, and alert sharing.

## Utility AI

Typed actions include attack, reload, advance, investigate, alarm, cover, heal, retreat, surrender, flee, follow, hold, and idle. Scores use awareness, health, morale, fear, needs, duty, distance, ammunition, readiness, escape routes, numbers, and orders. Action commitment, cooldowns, switching penalties, emergency overrides, and stable string ordering prevent oscillation and nondeterministic ties.

## Morale and surrender

Injury, isolation, nearby force balance, fear, duty, and mobility influence behavior. Retreat and flee require a viable escape. Surrender is an explicit persistent brain state that disables hostile action, movement, blocking, and armed presentation rather than acting as delayed death.

## Schedules and simulation tiers

Full simulation uses navigation, perception, utility AI, combat, animation, and detailed state. Reduced simulation advances individual schedules, travel interpolation, needs, and activities. Strategic simulation preserves semantic location, performs coarse deterministic travel, resolves explicitly active abstract combat pressure, and applies bounded recovery during sleep, rest, or medical treatment. Promotion and demotion retain location, health, activity, needs, awareness, inventory authority, schedule progress, fear, and disposition.

## Companion commands

The command service supports follow, stay, move, regroup, attack, avoid combat, weapon preference, cover, healing, interaction, and waiting. Commands have stable IDs, expiry, acknowledgement, rejection, completion, and cancellation histories.

## Runtime regression day

The micro-region now drives settlement schedules while hostile and creature actors use full AI. One allied actor is promoted to companion-capable full AI. Actors can travel, investigate sound, pursue, reload, fight, retreat or surrender, leave full simulation, continue abstract travel, and return at their preserved state.


## Playable companion controls

- `F2`: follow.
- `F3`: stay.
- `F4`: attack the nearest living hostile.
- `F6`: regroup.
- `F7`: take cover.
- `F8`: avoid combat.

Commands pass through `CompanionCommandService`, retain stable command IDs, and report acknowledgement, completion, rejection, cancellation, or expiry to the playable HUD.
