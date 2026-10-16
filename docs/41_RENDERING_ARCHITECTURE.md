# Rendering Architecture

## Ownership

`RendererFoundation` is created and disposed on the MonoGame thread. `RenderThreadGuard` rejects graphics-resource access from any other thread. CPU generation can occur anywhere, but GPU upload is represented as a queued action and drained at a bounded count per frame.

## Frame flow

1. The application obtains an immutable presentation snapshot.
2. The renderer drains a bounded number of resource uploads.
3. The camera constructs view, projection, and frustum data.
4. The queue builder removes disjoint bounds and applies stable opaque sorting.
5. Shadow-casting objects render into a depth-encoded color target.
6. Visible opaque objects render into the scene color target.
7. Debug geometry renders after opaque geometry.
8. Post-processing maps the scene target to the backbuffer.
9. Renderer telemetry is published.

## Presentation snapshot rule

A snapshot contains values and presentation-resource references only. It does not expose mutable simulation stores. A frame can be skipped without losing simulation state, and the renderer cannot advance the simulation.

## Resource lifecycle

- `MeshData` is CPU-owned immutable input.
- `GpuMesh` owns one vertex buffer and one 32-bit index buffer.
- Scene objects may share meshes and materials.
- `RendererFoundation` owns render targets, shader effects, fallback effects, rasterizer states, sprite batch, and the white fallback texture.
- Scene or cache owners dispose uploaded meshes.

## Determinism

The renderer is not part of authoritative checksums. Nevertheless, opaque queue order is stable by material ID, mesh name, and render-item ID to make captures and profiling repeatable.
