# ADR 0024 — GPU Skinning with CPU Reference Fallback

Accepted.

Character products retain platform-neutral bind vertices and skin weights. The preferred renderer uploads these to a GPU skinning effect. A CPU reference skinner and dynamic-vertex fallback remain available for validation, diagnostics and platforms where the effect cannot be loaded.
