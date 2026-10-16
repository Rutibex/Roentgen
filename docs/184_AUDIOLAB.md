# AudioLab

`Roentgen.AudioLab` validates the integrated procedural catalog without MonoGame or an audio device.

Commands:

- `catalog [seed]` — list deterministic asset IDs.
- `render [seed] [path]` — render a diagnostic sequence to WAV.
- `stress [seed] [seconds]` — render a bounded voice storm and report telemetry.
- `compare-seeds [seed-a] [seed-b]` — compare deterministic catalog fingerprints.
- `validate [seed]` — generate every catalog asset and render a non-finite-free block.

The tool is the supported route for headless CI and Linux smoke validation.
