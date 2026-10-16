# ADR 0045 — Integrate Playroma Music source

## Decision

Roentgen owns the selected Playroma Music Core, Generators and SoundFont runtime source directly under `src/`. No external repository is required.

## Consequences

Roentgen can version, test and release the adaptive score as one product. Workstation, CLI, FluidSynth, MIDI-analysis and legacy MonoGame streaming projects remain excluded.
