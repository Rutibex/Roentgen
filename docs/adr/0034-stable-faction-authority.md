# ADR 0034: Stable faction authority

## Decision

All persistent political identities are `StableId` values owned by `SocietyWorldState`. Actual faction, apparent faction, employer, settlement controller, jurisdiction, and diplomatic participants are distinct identities. String labels are presentation data or explicit legacy-import inputs only.

## Consequences

AI allegiance, alert sharing, combat, crimes, disguises, merchants, territory, persistence, and future narrative facts can refer to one canonical authority without name matching.
