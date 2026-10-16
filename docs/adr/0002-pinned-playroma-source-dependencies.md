# ADR 0002 — Pinned Playroma source dependencies

**Status:** Accepted

## Decision

Playroma Sound and Playroma Music are integrated as source repositories at exact full commit SHAs. They are optional for ordinary builds and mandatory for strict integration builds.

## Consequences

- Roentgen can evolve adapters against exact source APIs.
- Game builds are not silently changed by movement on a default branch.
- Contributors working outside audio can build without cloning both repositories.
- A dedicated integration workflow detects incompatibility.
