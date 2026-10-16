# Phase 4 Shader and Material Pipeline

## Effect sources

`src/Roentgen.App/Content/Content.mgcb` defines five custom effects:

- `ForwardLit.fx` — textured and vertex-colored forward illumination, directional sunlight, emissive contribution, distance fog, and shadow sampling.
- `ShadowDepth.fx` — depth encoding for the directional shadow map.
- `PostProcess.fx` — exponential exposure mapping, gamma correction, and adjustable vignette.
- `CharacterSkinning.fx` — GPU skinning for generated characters.
- `CharacterShadowSkinning.fx` — GPU-skinned shadow-map rendering.

## Default build mode

Custom-effect compilation is deliberately **optional**. `Roentgen.App.csproj` sets
`RoentgenCompileCustomEffects` to `false` unless the caller explicitly overrides it.
This keeps normal solution builds independent of MGCB and the platform-specific effect
compiler.

This is safe because `RendererFoundation` was already designed with fallback paths:

- opaque and character geometry use `BasicEffect` when the custom effects are absent;
- the shadow pass is skipped when its effect is unavailable;
- `SpriteBatch` presents the scene without the post-process effect.

The application therefore remains buildable and runnable while the custom shader sources
remain available for later pipeline work.

## Opt-in custom-effect build

On a machine with a working MonoGame 3.8.5 MGCB/effect toolchain, enable the custom
pipeline explicitly:

```powershell
dotnet tool restore
dotnet build -p:RoentgenCompileCustomEffects=true
```

The repository-local `.config/dotnet-tools.json` pins `dotnet-mgcb` to the same version
as the MonoGame framework and content-builder packages.
