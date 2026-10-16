# Contributing to Roentgen

Thanks for considering improvements. This is an experimental game with deterministic simulation contracts, large authored catalogs and multiple independently tested subsystems.

1. Open an issue describing the proposed bug fix or feature and a reproducible seed/scenario where relevant.
2. Keep gameplay authority in the appropriate domain library; avoid introducing MonoGame dependencies into platform-neutral libraries.
3. Include focused tests, update schemas/golden vectors when intentional formats change, and disclose save-compatibility impacts.
4. Run `python eng/validate_repository.py`, `dotnet build Roentgen.sln -c Release` and `dotnet test Roentgen.sln -c Release` when your environment supports them.
5. Regenerate `eng/source-manifest.sha256` with `python eng/write_source_manifest.py` only after all intended changes are complete.
6. Add third-party assets only with a clear origin and redistribution permission. Do not include proprietary code or sample banks without appropriate rights.

Keep pull requests scoped, explain runtime side effects and cite any test evidence. By submitting a contribution, you agree it is offered under this repository's MIT license unless a file indicates different pre-existing terms. Do not submit secrets or production personal data.

Report exploitable security issues privately as described in [SECURITY.md](SECURITY.md), not as public issues.
