# ADR 0012 — Platform-neutral procedural asset compilation

## Decision

Procedural asset recipes and compilers live in `Roentgen.Assets`, which may not reference MonoGame or any GPU API. `Roentgen.Rendering` owns the conversion to graphics resources.

## Rationale

- Enables deterministic headless tests.
- Allows world generation workers to compile CPU data.
- Prevents graphics-device ownership from leaking into content generation.
- Supports command-line authoring and server-side validation.
- Keeps generated content reconstructible from recipes.

## Consequences

The project defines its own small vector and color records. The renderer must explicitly convert those values to MonoGame equivalents.
