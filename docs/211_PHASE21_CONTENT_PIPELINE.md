# Phase 21 Content Pipeline

## Authoring source

The authoritative source is `content/roentgen/phase21-launch-content.json`. Its raw file checksum is stored beside it. Every persistent content record has a stable ID and is ordered canonically before fingerprinting or compilation.

## Loading

`ContentPackLoader.LoadDefault` searches the application directory and repository ancestry, verifies the SHA-256 sidecar, and deserializes the pack. `Phase21ContentRuntime.CreateDefault` immediately validates the complete graph and refuses to expose invalid content.

## Compilation

`ContentPackCompiler` emits:

- canonical JSON;
- an `RCPACK21` binary containing the canonical payload, content fingerprint, and payload SHA-256;
- a manifest containing package identity, counts, and fingerprint.

Compilation is intentionally separate from game startup. Release builds may precompile the pack, while developers retain readable source control diffs.

## ContentLab

`Roentgen.ContentLab` supports:

- `audit` / `validate`
- `coverage`
- `list --category <name>`
- `compile --output <directory>`
- `distribution`
- `repetition`
- `fingerprint`

Shell launchers are provided in `eng/contentlab.sh` and `eng/contentlab.ps1`.

## Compatibility

The content version, schema version, fingerprint, and source checksum are distinct values. A schema version describes structure, a content version describes the authored library, and the fingerprint describes exact normalized contents. Save migration can therefore distinguish compatible catalog growth from structural incompatibility.
