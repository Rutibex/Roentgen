# SoundFont and reviewed preset catalog

Roentgen uses the user-supplied GeneralUser GS bank unchanged. The runtime does not generate, modify or replace the SoundFont.

`generaluser-gs-roentgen.catalog.json` is a committed review layer over the 287 bank presets. It records bank, program, family, pitch range, role suitability, region affinities and exclusion status. Presets whose names represent doors, engines, gunshots, explosions, screams, telephones or other sound effects are excluded from ordinary scoring. The catalog fingerprint and SoundFont fingerprint are part of cue provenance.

MIDI channel 9 is reserved for percussion. Independent melodic tracks receive unique channels. When generation exceeds the fifteen-channel melodic budget, the channel allocator deterministically reassigns or mutes optional tracks and reports every repair.
