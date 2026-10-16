# Society State and Persistence

The Phase 15 persistence payload begins with schema version 15 and canonically writes every collection in stable-ID order. It includes the strategic day and revisions after all state.

`SocietyWorldSnapshotSerializer` wraps the payload with:

- magic `RGSOC15`;
- format version;
- payload length;
- SHA-256 payload digest;
- atomic temporary-file replacement.

`SocietyCommandProcessor` requires the expected world revision, captures a canonical rollback image, applies one typed command, touches the world revision, and restores the prior image if validation throws. This prevents partial control transfers, route changes, crimes, reputation evidence, leadership vacancies, and conflict creation.

Future schema changes must add explicit migration. They must not reinterpret an existing campaign through new generator behavior.
