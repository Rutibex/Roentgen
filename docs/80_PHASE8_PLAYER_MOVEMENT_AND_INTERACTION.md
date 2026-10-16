# Phase 8 — Player Movement and Interaction

Phase 8 replaces the renderer-owned fly camera with an authoritative fixed-step player shell. Physical input is normalized by the application and submitted as `PlayerIntentFrame` values. The simulation owns player position, stance, locomotion, interactions, door state, portable-object ownership, containers, and traversal state.

## Implemented

- Integer-millimetre player state and canonical checksums.
- Device-independent keyboard/mouse and gamepad action adapter.
- Kinematic capsule motor with acceleration, sprinting, walking, crouching, jumping, slopes, steps, mantling, ladders, ground snapping and depenetration safeguards.
- Unified terrain, structure and dynamic-door collision facade.
- Dynamic doors with fixed-point open progress, collision and rendering.
- Interaction target selection with reach, view cone, line-of-sight and hysteresis.
- Containers, direct pickups, temporary carry ledger and deterministic ownership transfer.
- First-person camera derived from authoritative player state.
- Context prompts, container panel, pause state and diagnostics HUD.
- Distance-driven terrain and structure residency used by actual traversal.
- Player movement and ownership tests, schemas and default binding sample.

## Controls

- W/A/S/D or left stick: move
- Mouse or right stick: look
- Left Shift or left-stick press: sprint
- Left Alt: quiet walk
- C / Left Control or gamepad B: crouch
- Space or gamepad A: jump or mantle
- E or gamepad X: interact
- R or gamepad Y: take all from a container
- Escape or gamepad Back: pause, cancel, or close
- F1: diagnostics

## Authority rule

The camera never owns world position. Consequential interactions are revalidated by stable target ID at the simulation tick where they execute. Missing collision is treated as a blocked streaming frontier rather than empty space.
