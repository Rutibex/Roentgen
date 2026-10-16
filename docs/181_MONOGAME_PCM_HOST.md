# MonoGame PCM Host

The game submits one interleaved stereo PCM16 stream at 48 kHz. A dedicated worker renders Playroma float blocks, applies a final limiter, converts into pooled PCM blocks, and writes to a single-producer/single-consumer ring. The main thread drains the ring and calls `DynamicSoundEffectInstance.SubmitBuffer`.

`BufferNeeded` only increments an atomic demand counter. It performs no rendering, allocation, logging, disk access, scene compilation, or game-state locking.

Default budgets are 512 render frames, 1,024 submitted frames, four queued device buffers, and twelve ring blocks. Device initialization failure falls back to `SilentAudioRuntime`, preserving game execution.
