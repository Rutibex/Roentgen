# Phase 10 — Real-time Combat

## Purpose

Phase 10 makes the permanent generated micro-region support a complete combat encounter without Roentgen Focus. It establishes the one authoritative combat model that Phase 11 will pause, inspect, preview and queue rather than replace.

## Authority model

`CombatEncounterRuntime` owns deterministic combat state. It contains actors, anatomical parts, layered armor, weapons, projectiles, pending actions, statuses and bounded feedback. The runtime is platform-neutral and does not depend on MonoGame graphics classes.

The graphical application shares each generated character's `CharacterState` with its `CombatActorState`. The combat runtime owns gameplay timing and consequences; the Phase 9 pose runtime renders the resulting action, locomotion and injury presentation. Hostile characters are removed from the Phase 9 ambient task runner when combat authority is installed.

For headless or production simulation, `CombatSimulationInstaller` registers:

- Attack, reload, block and equip command handlers.
- The fixed-step real-time combat system.
- Entity/component synchronization.
- Canonical combat-state persistence and restore notification.
- Deterministic feedback events.

## Stable state contracts

The Phase 10 state model includes:

- `CombatActorState`: health, stamina, morale, role, faction, target, equipment, action timing, block/retreat/death state, body parts, armor and statuses.
- `BodyPartRuntimeState`: anatomical definition, maximum/current integrity, trauma and functional state.
- `ArmorRuntimeState`: stable identity, layer, coverage, per-damage-type threshold/resistance, condition and revision.
- `WeaponRuntimeState`: magazine, reserve, condition, heat, recoil, jam state, readiness and attack sequence.
- `ProjectileState`: owner, weapon, definition, transform, radius, lifetime, fuse and explosive state.
- `PendingCombatAction`: immutable committed origin, direction, weapon, target, body part, impact/end ticks, sequence and impact-resolution marker.
- `CombatFeedbackEntry`: bounded, ordered explanations for visible combat outcomes.

All collections use stable ordering for canonical encoding. The entire combat state can be checksummed, stored in `SimulationStateStore`, restored from a simulation snapshot and replayed from deterministic commands.

## Weapon timelines

Every attack is scheduled as a fixed-tick action:

1. Validate actor, equipment, readiness, status and ammunition.
2. Capture weapon, origin, normalized direction, intended target and deterministic attack sequence.
3. Enter wind-up presentation.
4. Resolve exactly once at or after the impact tick.
5. Apply recovery and next-ready timing.
6. Return to idle or block unless death or another authoritative state overrides it.

Reloads use the same pending-action mechanism. A serialized pending action records whether impact has resolved, so a save or delayed update cannot duplicate or lose the committed effect.

## Ballistics and projectiles

Hitscan weapons use deterministic recoil/spread and body-part ray tests. Static terrain, structures and doors are queried through `ICombatOcclusionProvider`; the nearest environment hit prevents an actor hit behind it.

Persistent projectiles use fixed-tick swept movement. Their lifetime is bounded from range and velocity. They can contact body parts, contact environment geometry, expire, or detonate by fuse.

The Phase 10 weapon catalog supplies representative implementation profiles:

- Service sidearm.
- Industrial rifle.
- Scrap impact tool.
- Fragmentation charge.
- Industrial maw creature attack.

These are regression profiles, not the full launch weapon library.

## Melee

Melee uses a swept capsule-like query from the committed origin to the reach endpoint. Body-part spheres are enlarged by the weapon sweep radius, which prevents a valid strike from being lost between simulation samples. Environment collision can stop the sweep before an actor.

Melee consumes stamina, supports blocking and applies the same damage, armor and injury resolver used by firearms and explosives.

## Explosions

Explosions evaluate the nearest body part of every living actor within radius. Damage uses distance falloff. A static obstruction reduces exposure to one third rather than producing an all-or-nothing result. The owner is not excluded, so careless explosive use can hurt the player or allies.

## Damage and armor

The resolver supports kinetic, cutting, blunt, thermal, electrical, radiological, chemical, biological and sonic damage.

For each component:

1. Scale incoming damage.
2. Apply block reduction where relevant.
3. Process every covering armor layer in stable order.
4. Apply condition-scaled threshold.
5. Apply condition-scaled percentage resistance.
6. Degrade armor according to absorbed damage.
7. Damage body-part integrity and whole-actor health.
8. Update trauma, injury presentation, morale and statuses.
9. Enter stagger, dying or death when thresholds require it.

Part consequences are represented in simulation. Arm impairment reduces weapon handling. Leg impairment reduces AI movement. Head trauma reduces AI detection range. Torso trauma reduces stamina recovery. Destroyed vital parts are lethal.

## Status effects

Phase 10 defines bleeding, burning, shock, poison, irradiation, stun, suppression and cripple status contracts. Bleeding, burning, poison and irradiation apply deterministic once-per-second health damage. Shock and stun drain stamina and prevent attacks while active. Crippled parts create persistent status state.

## Basic enemy combat AI

`BasicEnemyCombatController` is intentionally smaller than the full Phase 14 AI. It can:

- Detect and track the player.
- Approach to weapon-specific range.
- Back away when a ranged actor is too close.
- Fire, bite, reload and respect action commitments.
- Slow when leg-injured.
- Lose detection range when head-injured.
- Retreat at low health and morale.

It operates only on deterministic combat state and therefore runs in CombatLab and tests without graphics.

## Feedback and interface

The HUD displays health, weapon, ammunition, condition, recoil and recent combat explanations. Debug mode draws recent attack/impact lines and active projectile markers.

Feedback differentiates firing, misses, environment impacts, armor stops, body-part damage, injuries, ongoing status damage, jams, reloads, explosions, retreat and death. This data is the source that Phase 11 will use for diagnostic predictions and the later final UI will present more elegantly.

## Permanent regression encounter

The Phase 9 micro-region remains the permanent vertical slice. Phase 10 converts its hostile humanoid and industrial quadruped into active enemies. The player receives a sidearm, rifle, scrap impact tool, fragmentation charges and field armor.

The encounter exercises outdoor and interior occlusion, humanoid and creature anatomy, ranged and melee behavior, armor, locational injury, explosives, death presentation, deterministic feedback and the existing first-person exploration loop.

## Deferred to later phases

Phase 10 does not implement Roentgen Focus, the final RPG skill formulas, the full item/economy layer, full navigation/cover AI, all launch weapon/armor families, companion command UI, final sound/music or the final interface suite. The combat contracts are designed so these phases extend the same model.
