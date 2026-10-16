# Material Recipe Language

## Output channels

Every material compile produces three RGBA images:

### Albedo

Visible base color and opacity.

### Surface

Packed channels:

- Red: roughness.
- Green: metallic response.
- Blue: height or relief hint.
- Alpha: reserved and currently written as 255.

### Emissive

RGB glow contribution and alpha.

Phase 4's renderer consumes albedo and scalar roughness, metallic, and emissive values. Later shader work can consume the complete packed maps without changing the material recipe.

## Layer evaluation

Layers execute in declared order. Each layer computes a deterministic mask and blends into one target channel.

Supported masks:

- Solid.
- Vertical gradient.
- Checker.
- Smooth value noise.
- Four-octave rust spots.
- Directional scratches.
- Warning stripes.
- Edge wear.
- Seeded speckles.

Each layer declares:

- Stable layer ID.
- Kind.
- Target channel.
- Primary and secondary colors.
- Opacity.
- Scale.
- Threshold.
- Angle.
- Seed.

## Noise implementation

The compiler uses an explicitly implemented 64-bit integer hash, smooth interpolation, and a fixed four-octave accumulation. It does not use `System.Random`, SIMD-dependent noise libraries, GPU shaders, or platform image APIs.

Tileable materials wrap lattice coordinates when the layer scale produces a meaningful period. The same pixel coordinates and seed therefore produce identical bytes across supported systems.

## Resolution policy

Width and height must:

- Be powers of two.
- Be between 4 and 2048.

The initial catalog uses 64×64 and 128×128 textures. Higher resolutions are permitted for exceptional assets, but validation warns above one megapixel.

## Atlas packing

The Phase 5 atlas builder uses deterministic shelf packing:

1. Sort materials by ordinal ID.
2. Place left to right.
3. Begin a new row only when the next tile plus padding exceeds maximum width.
4. Duplicate edge pixels into padding.
5. Round final dimensions to powers of two.

The builder produces matching albedo, surface, and emissive atlases plus normalized UV regions.

## JSON

The public schema is `schemas/material-recipe.schema.json`. Unknown fields are rejected. Layer order is preserved exactly.
