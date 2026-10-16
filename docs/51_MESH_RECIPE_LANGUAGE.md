# Mesh Recipe Language

## Recipe identity

Every mesh recipe contains:

- A stable lowercase asset ID.
- A schema version.
- One or more uniquely named parts.
- A collision-proxy policy.
- A requested LOD count.
- String metadata.

The recipe ID identifies content semantically. The fingerprint identifies the exact canonical recipe state. The compilation key combines that recipe with the mesh compiler identity, preventing stale derived-cache reuse after compiler changes.

## Part model

Each part contains a primitive, primitive parameters, transform, color, material slot, detail requirement, UV policy, and ordered modifiers.

`minimumLod` is a **detail tier**, not a distance index:

- `0`: present in every generated LOD.
- `1`: omitted from the coarsest LOD.
- `2`: omitted from the two coarsest levels; in a three-level recipe it is present only in LOD 0.

This makes small sights, knobs, wires, and fasteners disappear predictably as detail decreases.

## Primitive parameters

### Box and wedge

Use `size`. All dimensions must be positive.

### Cylinder and cone

Use `radius`, `height`, and `segments`. Cone is a cylinder whose upper radius is zero.

### UV sphere

Use `radius`, `height`, `segments`, and `rings`. Different radius and height values create ellipsoids.

### Lathe

Use a profile of `(radius, height)` points and a segment count. The compiler rotates the profile around the Y axis.

### Extrusion

Use a counter-clockwise convex XZ polygon and a positive height. Phase 5 triangulates caps with a fan; concave polygons are intentionally rejected from the supported contract.

### Tube

Use a 3D path, radius, and segment count. Rings are oriented from the path tangent using a deterministic reference-axis rule.

## Modifier order

Modifiers run in declared order before the part transform. This order is semantic and contributes to the recipe fingerprint.

1. Translation.
2. Component scale.
3. Euler rotation.
4. Taper.
5. Twist.
6. Bend.
7. Seeded noise displacement.
8. Clip below.
9. Clip above.

Any sequence is accepted, but changing order may produce a substantially different mesh.

## Transform order

The final part transform is:

```text
Scale × RotationX × RotationY × RotationZ × Translation
```

The same order is used on every platform.

## Normals and UVs

Primitive UVs are generated during primitive construction. An explicit projection policy can overwrite them after modifiers and transformation.

Normals are recalculated from triangle topology after deformation. Degenerate triangle contributions are ignored. Vertices that receive no valid face normal fall back to positive Y to prevent non-finite GPU data.

## LOD compilation

For LOD level `L`, segment and ring counts are divided by `2^L`, with hard minimums of three segments and two rings. Detail-tier parts are omitted according to the recipe's total LOD count.

LODs are separately compiled assets with their own topology and bounds. They share a recipe fingerprint and one parent content checksum.

## Collision proxies

Phase 5 emits conservative proxies:

- `none` — no collision.
- `axisAlignedBox` — complete mesh bounds.
- `boundingSphere` — radius derived from horizontal extent.
- `capsuleY` — radius and height derived from bounds.
- `compoundBoxes` — transformed part bounds with boxes contained by larger boxes removed.

Collision proxies are deterministic CPU data and contain no physics-engine objects.

## Budgets

Default compilation limits:

- 1,000,000 vertices.
- 3,000,000 indices.
- Five LOD levels.
- 256 recipe parts before a validation warning.
- 65,535 material slots.

Recipes exceeding configured topology limits fail rather than silently truncating data.

## JSON

The public schema is `schemas/mesh-recipe.schema.json`. Unknown fields are rejected by the C# deserializer and schema validator. This prevents misspelled authoring fields from being silently ignored.
