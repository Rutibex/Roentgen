# Phase 5 — Procedural Asset Language

## Status

Implemented repository baseline for deterministic, code-generated visual assets.

## Objective

Phase 5 establishes the asset-authoring layer that replaces conventional model and texture libraries. It gives Roentgen a platform-neutral language for describing geometry and materials, deterministic compilers that turn those descriptions into runtime data, and tooling for validation, preview export, caching, and regression testing.

The authoritative input is a **recipe**. Compiled meshes, textures, atlases, collision proxies, and GPU resources are derived products and may be discarded and reconstructed.

## Delivered projects

### `Roentgen.Assets`

Platform-neutral library containing:

- Recipe data contracts.
- Canonical recipe fingerprints and compiler-versioned compilation keys.
- Recipe validation reports.
- Mesh compiler.
- Material compiler.
- LOD generation.
- Collision-proxy generation.
- Material atlas packing.
- JSON serialization.
- Canonical binary payloads.
- Memory and disk caches.
- Built-in Phase 5 catalog.
- OBJ and PPM diagnostic exporters.

It references no MonoGame types. CPU generation is therefore usable by headless tests, command-line tools, workers, world generation, and future dedicated servers.

### `Roentgen.AssetLab`

Command-line authoring and validation tool. It can:

- Enumerate the built-in catalog.
- Generate editable JSON recipes.
- Validate mesh and material recipes.
- Compile canonical `.rgasset` payloads.
- Export mesh previews as OBJ.
- Export texture previews as PPM.
- Batch-build the complete catalog and its material atlas.

### `Roentgen.Assets.Tests`

Tests deterministic compilation, LOD reduction, atlas stability, JSON round trips, cache eviction, and corruption detection.

## Compiler flow

```text
MeshRecipe / MaterialRecipe
        ↓
Schema and semantic validation
        ↓
Canonical recipe fingerprint
Compiler identity and compilation key
        ↓
Deterministic CPU compilation
        ↓
Topology / pixel / budget validation
        ↓
Collision, LOD, atlas, and metadata derivation
        ↓
Canonical content checksum
        ↓
Memory cache and optional disk cache
        ↓
Render-thread GPU upload
```

## Determinism requirements

1. Part ordering is canonicalized by ordinal ID before mesh compilation.
2. Metadata is canonicalized by ordinal key.
3. Material layers retain declared order because layering is semantically significant.
4. Every stochastic modifier or material layer has an explicit unsigned seed.
5. No global random generator, wall clock, runtime hash code, GUID, or filesystem order may affect compiled content.
6. The recipe fingerprint, compiler-versioned compilation key, and compiled-content checksum are separate values.
7. Cache hits must not change output bytes.
8. LODs compile from the recipe, not by depending on GPU driver behavior.
9. Atlas inputs are sorted by material ID before packing.
10. Binary cache payloads are verified by SHA-256 before use.

## Mesh features

Phase 5 supports:

- Box.
- Cylinder.
- Cone.
- UV sphere.
- Wedge.
- Lathed profile.
- Polygon extrusion.
- Tube along a path.
- Translation, rotation, and scale.
- Taper.
- Twist.
- Bend.
- Seeded noise displacement.
- Vertical clipping.
- Primitive, planar, cylindrical, and spherical UV projection.
- Automatic normals.
- Per-vertex color.
- Material-slot annotation.
- One to five deterministic LODs.
- AABB, sphere, capsule, or compound-box collision proxies.

## Material features

Phase 5 supports deterministic RGBA generation for:

- Solid fills.
- Vertical gradients.
- Checker patterns.
- Value noise.
- Multi-octave rust masks.
- Directional scratches.
- Warning stripes.
- Edge wear.
- Speckles and aggregate.

Layers may target albedo, roughness, metallic, emissive, or height channels. Albedo, packed surface, and emissive textures are produced together.

## Runtime bridge

`Roentgen.Rendering` now accepts:

- `CompiledMeshAsset` and an LOD level.
- `CompiledMaterialAsset`.
- `CompiledTextureChannel`.

The bridge converts platform-neutral vertices to `RoentgenVertex`, creates MonoGame buffers and textures on the render thread, and returns disposable GPU ownership objects. The asset compiler itself never sees a `GraphicsDevice`.

## Built-in catalog

The initial catalog includes:

- Scrap rifle.
- Diagnostic terminal.
- Water tower.
- Concrete barrier.
- Dead tree.
- Rusted steel.
- Weathered concrete.
- Diagnostic phosphor.
- Aged wood.

These are representative compiler fixtures, not the final game content budget. Later phases expand the language and catalog without replacing the underlying contracts.

## Phase boundary

Phase 5 does not provide:

- Character skeleton generation.
- Procedural animation.
- Settlement grammars.
- Building room graphs.
- Destructible boolean geometry.
- Runtime terrain chunks.
- Full PBR shading of surface and emissive maps.
- A graphical node editor.

Those belong to later phases. Phase 5 provides the stable asset language they will consume.
