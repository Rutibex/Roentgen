# ADR 0042 — One Final PCM Stream

**Status:** Accepted

Playroma owns synthesis, buses, voices, spatialization, occlusion, reverb, and limiting. MonoGame receives one final stereo PCM16 stream through `DynamicSoundEffectInstance`. Applying a second MonoGame spatial system is prohibited.
