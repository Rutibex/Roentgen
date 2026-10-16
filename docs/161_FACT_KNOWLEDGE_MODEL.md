# Phase 16 Fact and Knowledge Model

## World facts

`FactRecord` describes an objective proposition:

- Stable fact ID
- Predicate ID
- Subject ID
- Typed `NarrativeValue`
- Truth state
- Scope
- Provenance ID
- Establishment tick
- Optional expiration tick
- Revision

The fact database indexes by ID, predicate, and subject. Quest evaluation never scans unindexed arbitrary strings.

## Knowledge claims

`KnowledgeClaim` records what one knower believes about a fact. It includes confidence, source trust, secrecy, contradiction, acquisition time, confirmation time, and disclosure permission.

A character may therefore:

- know a true fact;
- believe a false fact;
- possess only a rumor;
- hold an outdated claim;
- distrust the source;
- refuse to disclose a known fact.

The omniscient simulation state never automatically enters NPC dialogue.

## Provenance

Knowledge sources include observation, witness report, diagnostic analysis, faction communication, rumor, dialogue, documents, inference, quest consequences, strategic events, and world generation.

The narrative validator rejects claims that reference missing facts and dialogue requirements that cannot be sourced.
