#!/usr/bin/env python3
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]

def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")

def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"Build Fix 18 verification FAILED: {message}")

bridge = read("src/Roentgen.App/Phase18AudioBridge.cs")
game = read("src/Roentgen.App/RoentgenGame.cs")
identity = read("src/Roentgen.Audio/AudioIdentity.cs")
runtime = read("src/Roentgen.Audio.Playroma/PlayromaAudioRuntime.cs")
catalog = read("src/Roentgen.Audio.Playroma/IntegratedSoundCatalog.cs")
scene = read("src/Roentgen.Audio.Playroma/PlayromaSceneFactory.cs")
music_bridge = read("src/Roentgen.App/Phase19MusicBridge.cs")
cue = read("src/Roentgen.Music.Playroma/RoentgenCueGenerator.cs")
adaptive = read("src/Roentgen.Music.Playroma/AdaptiveMusicRuntime.cs")

for token in ("ProjectInteractions", "AudioEventKind.DoorOpen", "AudioEventKind.DoorClose", "AudioEventKind.ContainerOpen", "AudioEventKind.ContainerClose", "AudioEventKind.Pickup", "AudioEventKind.TerminalUse"):
    require(token in bridge, f"interaction audio projection is missing {token}")
require("audioBridge.Update(audioRuntime, snapshot, combat.Presentation, state, interfaceOpen, player)" in game, "game does not pass authoritative player runtime to the audio bridge")
require('AudioEventKind.DoorOpen' in identity and '=> "foley"' in identity, "interaction sounds are not routed through the foley bus")
require("AudioEventKind.TerminalUse => MathF.Sqrt(_settings.InterfaceVolume * _settings.FoleyVolume)" in runtime, "terminal gain is not tied to interface/foley user volume")
require('C(seed, "interaction.terminal", quality, 7)' in catalog and "InterfaceSoundGenerator" in catalog, "terminal sound is not procedurally generated as a retro electronic cue")
require("MergeAmbientOverlay" in scene and 'GetRequired("playroma.scene.wind")' in scene and "TuneBrokenMeridianIndustrial" in scene, "Broken Meridian does not combine sparse industrial ambience with wind")
require('StableIdFactory.Create("music-region", "broken-meridian")' in music_bridge, "music context is not bound to Broken Meridian")
require("ExplorationStyle(context)" in cue and 'return "roentgen-broken-meridian"' in cue, "regional exploration style selection is missing")
require("_requestedCueSeed" in adaptive and "seed == _requestedCueSeed" in adaptive, "same-situation context changes cannot request a new regional cue")
require("context.LocationKind" in adaptive and "context.Weather" in adaptive, "cue seed does not distinguish location/weather context")

style = json.loads((ROOT / "content/music/styles/204-roentgen-broken-meridian.style.json").read_text(encoding="utf-8"))
require(style["tempo"]["maximum"] <= 72, "Broken Meridian music is too fast for sparse environmental exploration")
roles = {role["id"]: role for role in style["roles"]}
require(roles["drums"].get("enabled") is False, "Broken Meridian environmental music still enables a continuous drum part")
require(roles["melody"].get("generalMidiProgram") == 25, "Broken Meridian lead is not biased toward dry steel-string guitar")
require(roles["texture"].get("density", 100) <= 35, "Broken Meridian texture is too dense")

font = ROOT / "content/music/soundfonts/default.sf2"
require(font.is_file(), "default.sf2 is missing")
sha = hashlib.sha256(font.read_bytes()).hexdigest()
require(sha == "9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe", "bundled SoundFont fingerprint changed unexpectedly")

print("Build Fix 18 verification PASSED: Playroma interaction SFX, sparse regional ambience, regional adaptive music, and supplied SoundFont are wired.")
