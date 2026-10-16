# Regional History and Campaign Premise

## History-first generation

`RegionalHistoryCompiler` creates evidence-bearing causal history before campaign selection. Every one of Roentgen's six region families receives:

- pre-collapse infrastructure;
- a collapse exposure or diagnostic failure;
- abandonment or repurposing;
- migration and faction formation;
- a later conflict or resource failure;
- a present crisis;
- representative locations, participants, evidence, tags, and public visibility.

Cross-region events connect the local histories into one world. Historical facts are projected into `NarrativeWorldState`, while faction knowledge receives only the facts and disclosure permissions justified by event visibility.

## Event contract

A `HistoricalEventRecord` carries a stable ID, event kind, day, region, location, participants, causes, effects, generated facts, evidence IDs, public visibility, tags, and summary. This allows the campaign premise and later revelations to cite real generated evidence rather than inventing disconnected lore.

## Premise selection

Seven premise kernels are supplied:

- resource conflict;
- diagnostic failure;
- buried system;
- founding myth;
- transport directive;
- medical transformation;
- convergent cause.

Selection first enforces hard event, faction, and region requirements. Eligible kernels are scored from historical kind matches, preferred semantic tags, and concealed causes. Equal scores use a named deterministic tie-break stream. The selected premise binds a primary region, two factions, a location, and the hidden historical cause.

## Validation invariants

- All six regions exist exactly once.
- Every region has a founding event, current crisis, and representative location.
- All cause/effect references target generated events.
- Every premise requirement is satisfied by generated history.
- The hidden cause is a real event with evidence and narrative facts.
