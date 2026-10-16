# ADR 0044 — Bounded Realtime Audio Path

**Status:** Accepted

Control queues, voice pools, lifecycle queues, PCM rings, and device buffers are bounded. Graph generation and scene compilation are forbidden on the render worker. Queue pressure rejects commands predictably rather than allocating or blocking.
