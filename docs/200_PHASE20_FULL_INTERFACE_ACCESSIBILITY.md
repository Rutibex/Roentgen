# Phase 20 — Full Interface and Accessibility

Phase 20 converts the cumulative developer-facing vertical slice into a player-operable product shell. The existing deterministic simulation, combat, Roentgen Focus, progression, items, AI, factions, quests, campaign director, Playroma Sound runtime, and adaptive SoundFont music remain authoritative.

## Implemented product flow

- Boot into a main menu instead of entering the regression campaign directly.
- Continue, new campaign, load, settings, accessibility, tutorials, credits/notices, and quit flows.
- Deterministic campaign seed selection and random secure seed generation.
- Character creation for attributes, three specialized skills, background, two traits, review, validation, and campaign start.
- Explicit screen stack for nested menus and confirmations.
- Pause menu with durable save/load and return-to-menu confirmation.
- Ending and deterministic world-state epilogue presentation.

## Implemented in-game screens

Inventory, layered equipment, character, skills, perks, regional map, quest journal, faction/settlement summaries, crafting, merchant trade, companion commands, dialogue, settings, accessibility, input bindings, tutorials, credits, save/load, Roentgen Focus, ending, and epilogue.

Screens read immutable or presentation-oriented models from authoritative runtime state. UI actions call application commands; they do not directly invent simulation state.

## Input and navigation

`Phase20InputRouter` maps keyboard and gamepad inputs into semantic `UiAction` values. Mouse hit testing is supplied by the retained UI renderer. Menus support directional navigation, selection, cancellation, tabs, scrolling, and pointer selection. Prompts are based on the binding profile rather than gameplay mutations tied to a screen widget.

## Accessibility

Implemented persisted settings include interface and subtitle scale, high contrast, color-vision preset, camera-shake disable, head-bob disable, flash reduction, motion reduction, persistent reticle, pause behavior, Focus overlay/flash intensity, cinematic Focus camera, and hold/toggle behaviors for aim, crouch, and sprint. Field of view, mouse sensitivity, gamepad sensitivity, inversion, autosaves, and tutorial behavior are also persisted.

## Roentgen Focus

`Phase20FocusRuntime` integrates the Phase 11 Focus coordinator into the playable application: full pause, target/part selection, forecast explanation, queue management, Focus-resource accounting, deterministic commit seeds, slow-time execution, cancellation, completion, and accessible presentation.

## InterfaceLab

`Roentgen.InterfaceLab` performs CPU-only layout-budget validation across 720p, 1080p, 1440p, ultrawide, narrow-window, and maximum-scale profiles, including pseudo-localized content and long scrolling lists. It complements—not replaces—GPU and human interaction tests.

## SoundFont decision

The bundled GeneralUser GS SoundFont, MeltySynth backend, adaptive two-deck music runtime, stems, bridges, motifs, and Playroma Sound master stream are intentionally retained unchanged. Phase 20 treats the fixed SoundFont as the chosen quality-oriented music renderer.
