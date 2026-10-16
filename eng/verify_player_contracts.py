from pathlib import Path
root=Path(__file__).resolve().parents[1]
required=[
'src/Roentgen.Contracts/PlayerInteractionContracts.cs','src/Roentgen.Entities/PlayerState.cs','src/Roentgen.Physics/PlayerCollisionWorld.cs','src/Roentgen.Physics/KinematicPlayerMotor.cs','src/Roentgen.Simulation/PlayerSimulationRuntime.cs','src/Roentgen.App/Phase8InputAdapter.cs','src/Roentgen.App/Phase8WorldScene.cs','src/Roentgen.Rendering/FirstPersonCameraRig.cs','src/Roentgen.UI/Phase8HudRenderer.cs','tests/Roentgen.Player.Tests/PlayerMovementTests.cs']
missing=[p for p in required if not (root/p).exists()]
if missing: raise SystemExit('missing Phase 8 files: '+', '.join(missing))
checks={
'src/Roentgen.Entities/PlayerState.cs':['FixedVector3','ComputeChecksum','PlayerCarryLedger'],
'src/Roentgen.Physics/KinematicPlayerMotor.cs':['CastCapsule','ProbeGround','TraversalState.Mantle','BeginLadder'],
'src/Roentgen.Simulation/PlayerSimulationRuntime.cs':['SelectTarget','ToggleDoor','TransferAll','ComputeChecksum'],
'src/Roentgen.App/Phase8WorldScene.cs':['StructureStreamingCoordinator','DynamicInteractionRendering'],
}
for p,needles in checks.items():
    text=(root/p).read_text()
    for needle in needles:
        if needle not in text: raise SystemExit(f'{p} missing {needle}')
print('Phase 8 contracts verified.')
