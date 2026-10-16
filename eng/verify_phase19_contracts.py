#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_FONT = "9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe"

REQUIRED = [
    "src/PlayromaMusic.Core/PlayromaMusic.Core.csproj",
    "src/PlayromaMusic.Generators/PlayromaMusic.Generators.csproj",
    "src/PlayromaMusic.SoundFont/PlayromaMusic.SoundFont.csproj",
    "src/Roentgen.Music/MusicContracts.cs",
    "src/Roentgen.Music/MusicDirector.cs",
    "src/Roentgen.Music/MotifRegistry.cs",
    "src/Roentgen.Music/MusicSnapshotSerializer.cs",
    "src/Roentgen.Music.Playroma/AdaptiveMusicRuntime.cs",
    "src/Roentgen.Music.Playroma/SoundFontDeck.cs",
    "src/Roentgen.Music.Playroma/RoentgenCueGenerator.cs",
    "src/Roentgen.Music.Playroma/MusicChannelAllocator.cs",
    "src/Roentgen.Music.Playroma/TransitionBridgeGenerator.cs",
    "src/Roentgen.App/Phase19MusicBridge.cs",
    "content/music/soundfonts/default.sf2",
    "content/music/soundfonts/default.sf2.sha256",
    "content/music/soundfonts/GeneralUser-GS-LICENSE.txt",
    "content/music/catalogs/generaluser-gs-roentgen.catalog.json",
    "tools/Roentgen.MusicLab/Roentgen.MusicLab.csproj",
    "tests/Roentgen.Music.Tests/Roentgen.Music.Tests.csproj",
    "tests/Roentgen.Music.Playroma.Tests/Roentgen.Music.Playroma.Tests.csproj",
    "tests/Roentgen.Music.StressTests/Roentgen.Music.StressTests.csproj",
]


def main() -> int:
    failures: list[str] = []
    for item in REQUIRED:
        if not (ROOT / item).is_file():
            failures.append(f"missing {item}")

    font = ROOT / "content/music/soundfonts/default.sf2"
    if font.is_file():
        header = font.read_bytes()[:12]
        if header[:4] != b"RIFF" or header[8:12] != b"sfbk":
            failures.append("default.sf2 is not a RIFF sfbk SoundFont")
        actual = hashlib.sha256(font.read_bytes()).hexdigest()
        if actual != EXPECTED_FONT:
            failures.append(f"default.sf2 SHA-256 is {actual}, expected {EXPECTED_FONT}")

    catalog_path = ROOT / "content/music/catalogs/generaluser-gs-roentgen.catalog.json"
    if catalog_path.is_file():
        catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        presets = catalog.get("presets", [])
        if catalog.get("presetCount") != 287 or len(presets) != 287:
            failures.append("reviewed SoundFont catalog must contain exactly 287 presets")
        if catalog.get("sha256") != EXPECTED_FONT:
            failures.append("reviewed catalog does not identify the bundled SoundFont")
        excluded = {str(p.get("name", "")).lower() for p in presets if p.get("excludedByDefault")}
        for marker in ("explosion", "machine gun", "telephone", "door"):
            if not any(marker in name for name in excluded):
                failures.append(f"reviewed catalog must exclude SoundFont SFX preset containing {marker!r}")

    styles = list((ROOT / "content/music/styles").glob("*-roentgen-*.style.json"))
    if len(styles) < 24:
        failures.append(f"expected at least 24 Roentgen style profiles, found {len(styles)}")

    music_projects = [
        ROOT / "src/PlayromaMusic.Core/PlayromaMusic.Core.csproj",
        ROOT / "src/PlayromaMusic.Generators/PlayromaMusic.Generators.csproj",
        ROOT / "src/PlayromaMusic.SoundFont/PlayromaMusic.SoundFont.csproj",
        ROOT / "src/Roentgen.Music/Roentgen.Music.csproj",
        ROOT / "src/Roentgen.Music.Playroma/Roentgen.Music.Playroma.csproj",
    ]
    combined = "\n".join(p.read_text(encoding="utf-8") for p in music_projects if p.is_file())
    for forbidden in ("NAudio", "FluidSynth", "MonoGame.Framework"):
        if forbidden in combined:
            failures.append(f"Phase 19 runtime projects must not reference {forbidden}")

    master = (ROOT / "src/Roentgen.Audio.Playroma/RoentgenMasterAudioSource.cs").read_text(encoding="utf-8")
    for marker in ("SetMusicSource", "AudioSceneDomain.Music", "Mix(_music"):
        if marker not in master:
            failures.append(f"master mixer missing music integration marker {marker!r}")

    runtime = (ROOT / "src/Roentgen.Music.Playroma/AdaptiveMusicRuntime.cs").read_text(encoding="utf-8")
    for marker in ("SoundFontDeck _deckA", "SoundFontDeck _deckB", "MusicPreparationWorker", "MusicSnapshotV19", "IStreamingAudioSource"):
        if marker not in runtime:
            failures.append(f"adaptive runtime missing contract marker {marker!r}")

    allocator = (ROOT / "src/Roentgen.Music.Playroma/MusicChannelAllocator.cs").read_text(encoding="utf-8")
    if "used[9] = true" not in allocator or "track.MidiChannel = 9" not in allocator:
        failures.append("MIDI channel allocator must reserve channel 9 for percussion")

    lock = json.loads((ROOT / "eng/dependencies.lock.json").read_text(encoding="utf-8"))
    if lock.get("dependencies"):
        failures.append("Phase 19 must have no external source dependencies")
    integrated = {item.get("id") for item in lock.get("integratedSources", [])}
    if not {"playroma-sound", "playroma-music"}.issubset(integrated):
        failures.append("Playroma Sound and Playroma Music must both be integrated sources")

    if failures:
        print("Phase 19 contract verification FAILED:")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("Phase 19 contract verification PASSED.")
    print("  Bundled SoundFont, 287-preset catalog, integrated source, two-deck runtime and one-stream boundary verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
