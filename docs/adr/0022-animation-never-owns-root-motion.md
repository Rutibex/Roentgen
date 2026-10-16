# ADR 0022 — Animation Never Owns Root Motion

Accepted.

Authoritative character position is owned by simulation and navigation. Procedural animation adapts gait phase and body pose to the displacement already produced by simulation. Animation may emit markers but may not secretly translate the persistent entity.
