# Phase 9 — Characters and Procedural Animation

Phase 9 replaces empty-world placeholders with deterministic generated characters and one complete nonhuman body-plan prototype. No imported character model, skeleton, skinning data or animation clip is required.

## Authoritative pipeline

1. Derive independent body, face, appearance, clothing, equipment and gait seed domains.
2. Generate a semantic `CharacterAppearanceDescriptor`.
3. Compile a compact skeleton with stable bone IDs and attachment sockets.
4. Build low-detail body and clothing meshes from tapered procedural segments and ellipsoids.
5. Generate four-influence skin weights and three character LODs.
6. Emit anatomical target-part mappings for later combat and Roentgen Focus.
7. Evaluate pose graphs from semantic action state, locomotion phase, attention and injury parameters.
8. Validate and apply bounded deterministic repair before accepting the generated product.
9. Render through GPU skinning when the character effects are available, with a deterministic CPU-skinning fallback.

## Implemented humanoid presentation

- Height, build, age presentation, posture, skin, hair/head-covering, clothing and marking variation.
- Compact humanoid skeleton with root, spine, head, arms, legs, hands, feet, jaw, eyes and coat-tail secondary bones.
- Generated body, broad facial landmarks, hair/head coverings, clothing, utility packs and first-person arm products.
- Idle breathing, walk, run, sprint, crouch, turn, aim, reload, melee, block, stagger, fall, sleep, climb, sit, converse, terminal and repair poses.
- Distance-driven gait phase.
- Additive aiming and look-at distribution.
- Foot placement inputs and pelvis adjustment.
- Injury-dependent arm guarding, limping and torso posture.
- Sidearm, rifle, tool, unarmed and one-handed melee pose profiles with deterministic timeline markers and visible equipment sockets.

## Creature prototype

The Blackwater industrial quadruped proves that the system is not limited to humanoids. It has:

- An axial spine chain.
- Four procedural support-limb chains.
- Head, jaw, sensory and exposed-growth controls.
- Quadruped gait generation.
- Tail and spine secondary motion.
- Eight targetable components.

## Permanent regression cast

The Phase 6–8 micro-region now contains:

- Prospect Clinic caretaker.
- Prospect Clinic guard.
- Workshop technician.
- Settlement resident.
- Blackwater scavenger.
- Blackwater hostile presentation actor.
- Blackwater industrial quadruped.
- Meridian Relay technician.

Each actor has a fixed stable ID, generation seed, task sequence and animation profile.

## Runtime boundaries

Simulation and actor tasks own world position and action progress. Animation never owns root motion. Rendering consumes immutable generated products and semantic pose inputs. Character GPU resources are created and destroyed only through the render-thread registry.

## Run

```powershell
dotnet run --project src/Roentgen.App
```

## CreatureLab

```powershell
dotnet run --project tools/Roentgen.CreatureLab -- humanoid
dotnet run --project tools/Roentgen.CreatureLab -- creature
dotnet run --project tools/Roentgen.CreatureLab -- animate
dotnet run --project tools/Roentgen.CreatureLab -- validate-seeds --count 10000
dotnet run --project tools/Roentgen.CreatureLab -- skin-weights
dotnet run --project tools/Roentgen.CreatureLab -- target-parts --creature
dotnet run --project tools/Roentgen.CreatureLab -- injury
dotnet run --project tools/Roentgen.CreatureLab -- equipment
dotnet run --project tools/Roentgen.CreatureLab -- lod
dotnet run --project tools/Roentgen.CreatureLab -- export-report --out phase9-character.json
```
