# Phase 19 — Integrated Playroma Music and Fixed SoundFont Runtime

Phase 19 integrates the engine-independent Playroma Music model and generators directly into Roentgen. The game has no external Playroma checkout and uses the supplied `default.sf2` as its fixed release instrument bank.

## Delivered architecture

- `PlayromaMusic.Core` owns the project, song, section, track, clip, note, tempo, meter, orchestration, motif and validation models.
- `PlayromaMusic.Generators` owns deterministic structured composition and the embedded style library.
- `PlayromaMusic.SoundFont` owns the selected MeltySynth backend, SoundFont inspection and sample-accurate MIDI-event scheduling.
- `Roentgen.Music` owns game-facing contexts, situations, cues, stems, transitions, motifs, settings, telemetry and `MusicSnapshotV19`.
- `Roentgen.Music.Playroma` owns cue preparation, reviewed preset selection, MIDI-channel repair, the two-deck synthesizer, transition scheduling and the streaming music source.
- `Roentgen.Audio.Playroma` remains the one final game mixer. Music is mixed into its reserved music domain before the Phase 18 limiter and PCM host.

No music project opens an audio device. Roentgen still owns exactly one `DynamicSoundEffectInstance` through `Roentgen.Audio.MonoGame`.

## Fixed SoundFont

The release asset is `content/music/soundfonts/default.sf2`.

- Internal name: GeneralUser GS 2.0.3 BETA
- SHA-256: `9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe`
- Presets: 287
- Runtime sample rate: 48,000 Hz stereo

Startup verifies the RIFF `sfbk` header and the exact SHA-256 value. A corrupt or absent bank causes silent music fallback while effects and gameplay continue.

The complete embedded license text is retained beside the bank. The reviewed catalog excludes non-musical sound-effect presets from automatic orchestration.

## Runtime behavior

The music director evaluates typed game context rather than UI strings. It uses hysteresis, minimum dwell time and transition cooldowns so small state changes do not restart the score.

Prepared cues include:

- deterministic identity and fingerprint;
- primary loop region;
- bar, phrase and section transition points;
- foundation, pulse, percussion, melody, texture and danger stems;
- key, scale, tempo and meter profile;
- intensity profile;
- validated MIDI-channel allocation.

Two independent MeltySynth decks permit a replacement cue to be prepared off-thread and entered on a selected musical boundary. The inactive deck starts before the active deck is retired, allowing bounded crossfades without a second device or unbounded queue. A third small deck plays stingers.

## Campaign continuity

`CampaignMotifRegistry` creates stable motifs for campaign, region, faction and companion identities. Motifs store contour, rhythm, harmonic color, preferred families and a deterministic fingerprint. `MusicSnapshotV19` persists motif state, cue identity, musical tick, stem gains, pending transition and stinger cooldowns; it never serializes PCM or synthesizer internals.

## Development tools

`Roentgen.MusicLab` can inspect the bank, list reviewed presets, generate and render cues, exercise transitions, compare seeds, stress the runtime and validate the complete Phase 19 content path.
