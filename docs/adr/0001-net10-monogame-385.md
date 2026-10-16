# ADR 0001 — .NET 10 and MonoGame 3.8.5 baseline

**Status:** Accepted

## Decision

Roentgen runtime projects target .NET 10 and C# 14. MonoGame DesktopGL, content packages, and local tools are pinned together at 3.8.5.

## Consequences

- The game can use the current language/runtime baseline already required by Playroma Sound.
- MonoGame package/tool mismatches are prevented by repository validation.
- Contributors need a .NET 10 SDK.
- Dependency upgrades require explicit compatibility testing rather than floating restore behavior.
