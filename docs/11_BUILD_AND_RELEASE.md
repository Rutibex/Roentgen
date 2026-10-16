# Build and Release Guide

## Local validation

```bash
python3 eng/validate_repository.py
```

This performs repository-level checks before invoking MSBuild: JSON/XML syntax, project declaration, graph cycles, package policy, MonoGame boundaries, solution membership, external commit pinning, and accidental build output.

## Local build

```bash
./eng/build.sh
```

or:

```powershell
./eng/build.ps1
```

The scripts restore local tools, restore packages, compile Release, and execute tests.

## Strict Playroma build

```bash
./eng/bootstrap.sh
./eng/build.sh --require-playroma
```

The bootstrap process is intentionally destructive only when `--force` is explicitly supplied. Dirty external worktrees stop the process.

## Publishing

```bash
./eng/publish.sh linux-x64 0.1.0-phase1
```

```powershell
./eng/publish.ps1 -Runtime win-x64 -Version 0.1.0-phase1
```

Published files are written under `artifacts/publish/<runtime-id>/`. `build-manifest.json` records the version, runtime, file sizes, and SHA-256 hashes.

## Release policy

- Publish from a clean commit.
- Do not edit generated publish directories.
- Archive the complete directory containing `build-manifest.json`.
- Release tags use `v<semantic-version>`.
- A release candidate must pass ordinary CI, strict Playroma integration, dependency review, CodeQL, and clean-machine launch testing.

## Phase 3 headless executable

Run directly:

```bash
dotnet run --project src/Roentgen.Headless -- help
```

Publish a self-contained headless package:

```bash
./eng/publish-headless.sh linux-x64 0.3.0-phase3
```

```powershell
./eng/publish-headless.ps1 -Runtime win-x64 -Version 0.3.0-phase3
```

The headless publisher is separate from the MonoGame client publisher so automated simulation hosts do not ship graphics or content-pipeline dependencies unnecessarily.
