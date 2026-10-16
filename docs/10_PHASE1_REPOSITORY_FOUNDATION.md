# Phase 1 — Repository and Build Foundation

**Status:** Implemented foundation baseline  
**Version:** 0.1.0-phase1  
**Governing document:** `docs/constitution/00_PRODUCT_CONSTITUTION.md`

## 1. Purpose

Phase 1 establishes the repository as a reliable product-development system. Its deliverable is not gameplay content; it is the buildable, testable, publishable structure on which every later Roentgen phase must depend.

## 2. Binding decisions

- Runtime projects target `net10.0` and C# 14.
- MonoGame DesktopGL and MonoGame content tools are pinned together at 3.8.5.
- Windows x64 and Linux x64 are the first release runtimes.
- The application simulation target is 60 fixed ticks per second.
- Central package management owns all NuGet versions.
- Build output is isolated under `artifacts/`.
- Warnings are errors for Roentgen projects.
- Nullable reference types and deterministic compilation are mandatory.
- Platform-neutral simulation and generation code may not reference MonoGame.
- External source integrations are optional in an ordinary checkout and mandatory in strict integration CI.

## 3. Project layers

### Foundation

`Roentgen.Contracts`, `Roentgen.Core`, `Roentgen.Diagnostics`, `Roentgen.Simulation`, and `Roentgen.Generation` define canonical data, product identity, diagnostics, fixed-step timing, and deterministic seed domains.

### Simulation domains

World, entities, physics, navigation, items, RPG, combat, factions, narrative, and AI are separate assemblies. This prevents a monolithic game project and allows future headless tests.

### Presentation

Animation remains platform-neutral. Rendering and UI are the only reusable libraries allowed to consume MonoGame. `Roentgen.App` is the executable composition root.

### Audio and music

Roentgen-owned abstractions live in `Roentgen.Audio` and `Roentgen.Music`. Conditional source bridges link the pinned Playroma projects without making ordinary repository validation depend on external checkouts.

### Persistence

Persistence depends on simulation-domain state but presentation does not own persistence. Save formats can therefore be tested without opening a graphics device.

## 4. Build modes

### Ordinary mode

The solution builds without the external repositories. Playroma bridge assemblies report that source integration is dormant. This supports contributors working on unrelated systems and makes archival builds reproducible even if an external host is unavailable.

### Strict Playroma mode

`RequirePlayroma=true` requires both source trees. `eng/bootstrap.py` checks out exact revisions, verifies the expected projects, and refuses to overwrite dirty working trees. CI builds and tests the combined graph.

## 5. Application shell

The Phase 1 executable:

- Opens a resizable 1280×720 DesktopGL window.
- Uses a 60 Hz fixed timestep.
- Registers the domain-module catalog.
- Reports optional integration status.
- Exits through Escape or a controller Back button.
- Contains no conventional game assets.

It is intentionally small. Rendering, world generation, and gameplay are introduced in later phases without replacing the composition root.

## 6. Build and release flow

1. Validate repository structure.
2. Restore local MonoGame tools.
3. Restore central NuGet dependencies.
4. Build the complete solution in Release.
5. Run test projects and coverage collection.
6. Publish self-contained `win-x64` and `linux-x64` packages.
7. Generate a SHA-256 build manifest for every published file.

## 7. Phase 1 completion gate

Phase 1 is complete when:

- Every project is declared and present in the solution.
- The runtime dependency graph is acyclic.
- Static repository validation passes.
- The ordinary solution builds and tests on Windows and Linux CI.
- The pinned Playroma source graph builds in strict CI.
- Publish workflows produce self-contained packages and file manifests.
- The application shell starts without requiring game content.
- Phase 0 documents and schemas remain present and authoritative.
