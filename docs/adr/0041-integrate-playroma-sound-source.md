# ADR 0041 — Integrate Playroma Sound Source

**Status:** Accepted

Playroma Sound is copied into `src/Playroma.Sound.*`, licensed and versioned with Roentgen. Runtime builds may not depend on an external Playroma Sound repository, NuGet package, NAudio host, or WinForms editor. This guarantees reproducible source-level integration and permits Roentgen-specific realtime upgrades.
