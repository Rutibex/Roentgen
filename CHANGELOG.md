# Changelog

All notable public-facing changes are summarized here. The codebase predates this GitHub packaging; this file is a concise preview changelog, **not** a guarantee that every subsystem has passed playtesting.

## [Unreleased]

- Final on-device rendering, audio, campaign traversal and full cross-platform test verification remain outstanding.

## [0.21.0-preview] — 2026-10-15

### Added and integrated

- Source-level player UI, accessibility options, character creation and save/load shell.
- Deterministic campaign/content subsystems and a 1,499-record launch catalog.
- Procedural terrain, structures, navigation, AI, RPG, combat, faction, quest, music and sound modules.
- Source upgrade for structure geometry, generated textures, camera movement, atlas navigation and story-world interactions.
- Documentation, issue templates, contribution and security guidance, third-party attribution and a public repository release workflow.

### Repository maintenance

- Removed phase handoff files, incremental build-fix notes and local verification logs.
- Preserved runtime content, technical specs, tests, lockfiles and the GeneralUser GS bank.
- Updated source manifest and static validators to match the current source graph.

### Limitations

- This version is an **unverified preview** in this packaging environment. Do not describe it as a complete game or stable release.
- New campaigns are recommended; old save compatibility is not guaranteed.
