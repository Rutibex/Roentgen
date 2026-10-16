from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")

def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"Build Fix 17 verification FAILED: {message}")

physics = read("src/Roentgen.Physics/PlayerCollisionWorld.cs")
motor = read("src/Roentgen.Physics/KinematicPlayerMotor.cs")
world = read("src/Roentgen.App/Phase8WorldScene.cs")
scene = read("src/Roentgen.App/Phase9WorldScene.cs")
player = read("src/Roentgen.Simulation/PlayerSimulationRuntime.cs")
render = read("src/Roentgen.Rendering/DynamicInteractionRendering.cs")
game = read("src/Roentgen.App/RoentgenGame.cs")

require("Characters = 8" in physics, "character collision mask is missing")
require("DynamicCharacterCollisionRegistry" in physics, "dynamic character collision registry is missing")
require("CollisionQueryMask.Structures|CollisionQueryMask.Doors|CollisionQueryMask.Characters" in motor, "player motor does not collide with characters")
require("foreach(StructureBlueprint structure in _structureWorld.Structures)_structureCollision.Register" in world, "structure collision is not primed independently of render streaming")
require("_structureCollision.Remove(structure.Id)" not in world, "structure collision can still disappear with render LOD")
require("BuildActorInteractionTargets" in scene and "RefreshCharacterCollision" in scene, "moving character interaction/collision synchronization is missing")
require("TryRayAabb" in player and "SyncActorTargets" in player, "crosshair actor targeting is missing")
require("interactive-highlight" in render and "highlightedTargetId" in render, "focused object highlight material is missing")
require("HandleDirectActorInteraction" in game, "direct NPC interaction routing is missing")

print("Build Fix 17 verification PASSED: solid structures/doors/NPCs and crosshair interaction highlighting are wired.")
