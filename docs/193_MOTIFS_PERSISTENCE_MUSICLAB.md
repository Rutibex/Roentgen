# Motifs, persistence and MusicLab

Campaign motifs are stable generated records attached to semantic owners. Their fingerprints do not depend on audio-device timing or operating-system paths.

`MusicSnapshotV19` restores the nearest safe musical tick after regenerating the cue from its deterministic identity. It verifies the SoundFont hash and style-library fingerprint, restores motif records and stem targets, and discards stale transient audio.

MusicLab commands:

- `inspect-font`
- `list-presets`
- `generate-cue`
- `render-cue`
- `test-transition`
- `compare-seeds`
- `stress`
- `validate`
