# Phase 12 — Character Progression

Phase 12 makes `Roentgen.Rpg` the authoritative source of character capability. It implements seven attributes, fourteen skills, derived statistics, transactional character creation, backgrounds, traits, deterministic skill checks, experience, levels 1–50, a typed perk framework, forty initial perks, NPC profiles, a character-screen model, and BalanceLab.

## Authority boundary

`CharacterProgressionState` owns permanent RPG state. Combat, Focus, interaction, and UI consume immutable capability snapshots. Presentation and animation never own attributes, skills, XP, or perk ranks.

## Canonical state

The entity component type is `rpg.character-progression.v1`. Canonical ordering is explicit for attributes, skills, traits, perks, history, familiarity sources, and award records. Duplicate XP award IDs are rejected.

## Completion gate

The six-build Prospect Clinic fixture must pass through six distinct routes: ballistics, melee, stealth/security, engineering/science, medicine/fieldcraft, and influence/commerce.
