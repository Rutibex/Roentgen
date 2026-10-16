# Phase 16 — Quest and Dialogue Engine

Phase 16 adds the deterministic narrative authority that binds world facts, faction objectives, resources, relationships, hidden information, locations, deadlines, and consequences into playable quest graphs.

## Delivered systems

- Typed fact database with truth, scope, provenance, expiration, and stable IDs.
- Per-actor and per-faction knowledge claims separated from objective world truth.
- Idempotent semantic-event ingestion and reconciliation after load.
- Data-driven quest kernels, typed slots, deterministic binding, graph nodes, objectives, routes, recovery policies, and consequences.
- Serializable condition and effect languages.
- Deterministic skill attempts with explicit retry policy.
- Dialogue intent graphs, grammatical templates, speaker profiles, claims, knowledge filtering, and skill branches.
- Rumors with transmission chains, confidence, distortion, correction, and expiration.
- Narrative validation, bounded recovery, six-build abstract solving, and QuestLab.
- Thirty initial quest kernels and the permanent **The Pale Manifest** regression quest.
- Atomic Phase 16 campaign snapshot containing item, society, and narrative sections.

## Authority rules

1. `NarrativeWorldState` owns facts, claims, quest state, conversations, rumors, consequences, recovery records, and consumed event IDs.
2. Narrative never directly mutates item, faction, combat, AI, or world state. It emits typed effect requests through `INarrativeExternalEffectRouter`; `Phase16DomainEffectRouter` validates and transactionally commits supported consequences into authoritative item and society state.
3. Truth, belief, and willingness to disclose are different states.
4. Every generated quest plan is immutable once accepted by a campaign.
5. Every effect and semantic event is idempotent by stable ID.
6. Every major quest kernel provides at least three solution routes and at least one noncombat route.
7. Failure changes the world or activates recovery; it does not silently dead-end required content.

## Regression controls

- `Q`: open or close dialogue with the Prospect Clinic caretaker.
- `J`: open or close the quest journal.
- `N`: development shortcut that discovers the Pale Manifest evidence.
- `P`: development shortcut that resolves the relay crisis.
- `F5` / `F9`: save and restore the atomic Phase 16 campaign snapshot.

The development shortcuts are regression aids only; the quest engine itself uses facts, conditions, events, dialogue choices, skill checks, and effect transactions.
