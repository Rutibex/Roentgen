# Continuous Integration and Delivery

## CI

Runs ordinary Release builds and tests on Windows and Ubuntu. It validates the repository before restore and collects cross-platform code coverage.

## Playroma integration

Runs for pull requests, manually, and weekly. It clones the exact dependency revisions and builds with `RequirePlayroma=true`. This catches target-framework or API drift without making every local build download both repositories.

## Dependency review

Pull requests fail when newly introduced dependencies contain high-severity known vulnerabilities.

## CodeQL

C# static security analysis runs on main, pull requests, and a scheduled cadence.

## Publish

Manual runs and version tags create self-contained Windows and Linux packages. Each package contains a generated build manifest. Uploading to a store or creating a public GitHub release is intentionally not automated in Phase 1, preserving a final human release gate.


## Phase 2 determinism workflow

`.github/workflows/determinism.yml` runs the independent golden vectors and repeats the dedicated deterministic-core test assembly ten times on Windows and Linux. It runs on pull requests, pushes to `main`, manual dispatch, and a weekly schedule.

## Headless simulation workflow

`.github/workflows/headless-simulation.yml` builds and tests the headless project on Windows and Linux, executes a 10,000-tick smoke run, verifies the produced replay, uploads snapshots and telemetry, and runs a repeated multi-seed Linux soak.


## Renderer foundation workflow

`.github/workflows/renderer-foundation.yml` restores tools, compiles the application and MGCB effects, runs renderer tests, and runs repository validation on Windows and Linux.
