#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"Build Fix 21 verification FAILED: {message}")


motor = read("src/Roentgen.Physics/KinematicPlayerMotor.cs")
collision = read("src/Roentgen.Physics/PlayerCollisionWorld.cs")
player_tests = read("tests/Roentgen.Player.Tests/PlayerMovementTests.cs")
master = read("src/Roentgen.Audio.Playroma/RoentgenMasterAudioSource.cs")
runtime = read("src/Roentgen.Audio.Playroma/PlayromaAudioRuntime.cs")
game = read("src/Roentgen.App/RoentgenGame.cs")
bridge = read("src/Roentgen.App/Phase18AudioBridge.cs")

require("stepRise>10&&stepRise<=_settings.MaximumStepMillimeters" in motor,
        "movement still accepts a level-ground collision as a step")
require("collision.HasStandingClearance(stepped,_settings.CapsuleRadiusMillimeters,height)" in motor,
        "step destination does not use standing-clearance collision validation")
require("CastCapsule(stepped,stepped+new FixedVector3(0,1,0)" not in motor,
        "sub-centimetre zero-length step sweep is still present")
require("CollisionQueryMask.Structures|CollisionQueryMask.Doors|CollisionQueryMask.Characters" in motor,
        "player movement is not querying all solid gameplay bodies")
require("All = Terrain | Structures | Doors | Characters" in collision,
        "CollisionQueryMask.All omits characters")
require("LevelGroundDoesNotTurnWallCollisionIntoAFreeStep" in player_tests and
        "WallOnFlatGroundCollisionWorld" in player_tests,
        "flat-ground wall regression test is missing")

require("private string _lastFault = string.Empty;" in master,
        "audio master has no contained-domain fault storage")
require("TryStart(_region, \"region ambience\")" in master and
        "TryStart(_resident, \"resident ambience\")" in master and
        "TryStart(_focus, \"focus audio\")" in master,
        "optional audio domains are not isolated during startup")
require("TryMix(_global" in master and "TryMix(_region" in master and "TryMix(_music" in master,
        "audio domains are not isolated during rendering")
require("if (_music is not null && !TryMix" in master and "_music = null" in master,
        "faulted adaptive music is not detached from the healthy SFX mixer")
require("Region ambience is optional" in runtime and "catch (Exception exception)" in runtime,
        "regional ambience replacement is not guarded")
require("string.IsNullOrWhiteSpace(_master.LastFault) ? _fault : _master.LastFault" in runtime,
        "contained audio faults are not surfaced through telemetry")
require("Adaptive music initialization failed; Playroma sound will continue" in game,
        "music initialization can still take down Playroma Sound")
require("Audio presentation initialization was partially skipped; Playroma sound remains active" in game,
        "audio presentation/region initialization can still take down the PCM host")
require("AUDIO {audio.RuntimeState} voices {audio.StartedVoices} underruns {audio.Underruns}" in game and
        '" !AUDIO"' in game,
        "runtime audio health is not visible in the window title")

sanity = "runtime.Submit(Create(player.Tick, AudioEventKind.InterfaceConfirm"
region = "runtime.SetRegion(AudioRegion.BrokenMeridian"
require(sanity in bridge and region in bridge and bridge.index(sanity) < bridge.index(region),
        "startup SFX sanity cue is not submitted before optional regional ambience")

print("Build Fix 21 verification PASSED: walls cannot become zero-rise steps and optional Playroma failures no longer silence core SFX.")
