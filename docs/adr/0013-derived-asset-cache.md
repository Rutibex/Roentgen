# ADR 0013 — Compiled assets are disposable derived cache data

## Decision

Recipes are authoritative. Compiled assets are addressed by canonical recipe fingerprints and verified content checksums. Disk cache entries may be deleted at any time without changing campaign state.

## Rationale

This preserves the small distribution model, makes cache corruption recoverable, and permits compiler improvements through explicit schema/version migrations.

## Consequences

Save files may reference stable recipe IDs and versions but must never require an opaque cached GPU or compiler object to exist.
