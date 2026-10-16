# Renderer Smoke Scene

The application now opens a generated 3D scene rather than a clear-color window.

The scene includes:

- A 64 × 64-cell generated terrain grid.
- Eighteen transformed ruin blocks sharing one box mesh.
- A generated cylindrical relay tower and emissive beacon.
- Generated material definitions.
- Directional sunlight and fog.
- Shadow casting and reception.
- Optional grid and world-axis debug lines.
- Live draw-call, triangle, culling, and CPU timing telemetry in the window title.

## Acceptance use

The smoke scene is deliberately permanent. It provides a low-cost regression surface for:

- Content compilation.
- Graphics-device initialization.
- Buffer upload.
- Culling.
- Shader parameter binding.
- Render targets.
- Resize handling.
- Resource disposal.

It is not a game level and will later be moved into a dedicated renderer laboratory when Phase 5 introduces tool applications.
