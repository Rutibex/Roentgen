# Semantic Audio and Acoustic Identities

Every sound request carries a stable event ID, simulation tick, source, target, item, location, emitter state, and typed parameter map. `AudioIdentityFactory` maps semantic kinds to procedural assets and derives family and variation seeds from stable IDs.

A weapon therefore keeps one recognizable family identity while shot variation is selected by the event. Footsteps bind surface and gait. Region ambience binds region ID and region seed. No generated PCM is saved; persistent identities and ambient scene state are reconstructed.
