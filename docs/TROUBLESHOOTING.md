# Troubleshooting and known limitations

## Build/restore

**`dotnet` not found:** install .NET 10 SDK and restart the terminal. Check `dotnet --info` and the pinned version in `global.json`.

**NuGet fails:** confirm internet access and repository package sources in `NuGet.Config`. Package caches are local and intentionally omitted from the ZIP.

**Content/MonoGame effect tools fail:** normal builds do not require optional `.fx` compilation. If `RoentgenCompileCustomEffects=true`, restore `dotnet-mgcb` via `dotnet tool restore` and ensure the native shader compilation dependencies work on your OS.

**Out-of-memory compiler:** limit MSBuild parallelism with `-m:1 -p:UseSharedCompilation=false`.

**OpenGL/SDL initialization error:** use a graphical desktop with working GPU drivers. Headless CI/container environments frequently cannot create a MonoGame DesktopGL graphics device. This is not proof of an app logic failure.

## Runtime

**No music:** verify the checked-in SoundFont exists in `content/music/soundfonts/default.sf2` in source or copied app output. The startup content checks may silence music on checksum mismatch.

**Old save won't load:** generation and save contracts have changed. Start a new campaign and keep your old files; don't try to force incompatible topology state into the new build.

**Missing/unfinished interaction:** several quest and economic routes are partially integrated. NPC dialogue/trade can be gated by proximity, visibility and actor state; crafting depends on nearby valid stations.

**Missing textures / rough rendering:** most assets are procedurally generated and newer material/rendering changes require on-device visual QA. Do not assume all renderers/effects have been tested.

## Release-readiness caveats

The upstream handoff reported a successful application build and small targeted structure/player test runs. This cleanup pass has access to the source archive but **does not have the .NET 10 SDK, a graphics device, or an audio device**. It cannot certify successful gameplay, full test-suite completion, accessible interaction, balance, or performance. Verify those on actual hardware before promoting a release beyond preview.

Planned/unfinished areas include truly varied structure grammar families, complete 8 km populated-world materialization, underground locations, complete campaign/quest resolution, interior map navigation, cross-building collision/AI traversal testing, art quality, full player UI polish and save compatibility.
