# Phase 18 — Integrated Playroma Sound Runtime

Phase 18 vendors the platform-neutral Playroma Sound source directly under `src/Playroma.Sound.*`. Roentgen no longer clones or references an external Playroma Sound checkout. The copied MIT-licensed source is part of the game solution and has been upgraded for dynamic voices, acoustic zones, portal filtering, lifecycle reporting, and host-level control.

## Runtime path

`SemanticAudioEvent` → `PlayromaAudioRuntime` → four prepared streaming-scene domains → `RoentgenMasterAudioSource` → render worker → bounded PCM16 ring → MonoGame `DynamicSoundEffectInstance`.

The game owns the audio device only through `Roentgen.Audio.MonoGame`; simulation and gameplay assemblies never reference MonoGame or Playroma classes.

## Scene domains

- Global: weapons, impacts, footsteps, interactions, and interface sounds.
- Region: deterministic long-running region ambience and weather texture.
- Resident: creatures and machinery for the loaded world.
- Focus: protected diagnostic interface sounds.
- Music: a reserved silent gain domain for Phase 19.

## Integrated Playroma upgrades

- `StreamingSceneHost.TryPlaySound`, stop, spatial update, acoustic update, portal update, and lifecycle dequeue.
- Hosted voice handles include scene generation, preserving correctness during crossfades.
- Dynamic voices carry acoustic zone IDs.
- Voice lifecycle events report started, rejected, completed, stolen, stopped, and faulted states through a bounded queue.
- Per-voice one-pole low-pass filtering applies obstruction, occlusion, and portal low-pass transmission.
- Runtime portal matrices can be swapped without compiling or allocating on the render thread.

## Authority rule

Audio consumes immutable semantic events and presentation snapshots. It may not mutate simulation, publish gameplay outcomes, or participate in deterministic campaign checksums.
