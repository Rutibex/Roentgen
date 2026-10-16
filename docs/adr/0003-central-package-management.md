# ADR 0003 — Central package management

**Status:** Accepted

## Decision

All NuGet package versions are declared in `Directory.Packages.props`; individual projects declare package identities only.

## Consequences

- Framework and test package versions remain consistent across the solution.
- Renovation and security review occur in one location.
- Project-level package versions are rejected by static validation.
