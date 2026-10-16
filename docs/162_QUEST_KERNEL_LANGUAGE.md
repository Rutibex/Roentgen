# Phase 16 Quest Kernel Language

A kernel defines semantic roles and causal structure rather than fixed names.

## Kernel parts

- Metadata and themes
- Typed slots and tag constraints
- Objectives
- Quest graph nodes
- Multi-route solutions
- Recovery policies
- Dialogue intent nodes and choices
- Consequences
- Follow-up hooks

## Binding

`QuestBinder` performs stable-ID-ordered candidate collection, hard constraint filtering, thematic scoring, deterministic tie-breaking, validation, and bounded repair. Unrelated content cannot reshuffle an existing binding.

## Conditions

The serializable condition language covers facts, knowledge, actor state, items, reachability, services, relationships, reputation, crime, disguise belief, quest/objective state, skills, behavior tags, time, and Boolean composition.

## Effects

Effects cover facts, knowledge, objectives, quests, reputation, crimes, items, locations, services, trade, encounters, relationships, statuses, experience, recipes, and follow-ups. External effects use prepare/commit receipts; partial cross-domain consequences are not accepted.

## Major-quest route rule

Every major kernel must expose at least three viable route families selected from combat, stealth, technical, scientific, medical, social, economic, faction leverage, and exploration.
