using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Physics;

namespace Roentgen.Player.Tests;

public sealed class PlayerMovementTests
{
    [Fact]
    public void IntentAxesAreStable()
    {
        var frame = new PlayerIntentFrame(32767, -32767, 10, -20, PlayerIntentButtons.SprintHeld, InputDeviceKind.KeyboardMouse);
        Assert.Equal(1d, frame.NormalizedMoveX, 6);
        Assert.Equal(-1d, frame.NormalizedMoveY, 6);
    }

    [Fact]
    public void PlayerChecksumChangesWithMovement()
    {
        var player = new PlayerState(StableIdFactory.Create("test", "player"), new FixedVector3(0, 0, 0));
        StateChecksum before = player.ComputeChecksum();
        player.FeetPosition = new FixedVector3(1, 0, 0);
        Assert.NotEqual(before, player.ComputeChecksum());
    }

    [Fact]
    public void PlayerGoldenChecksumIsStable()
    {
        var player = new PlayerState(StableIdFactory.Create("player", "phase8-regression"), new FixedVector3(250000, 18200, -125000))
        {
            Velocity = new FixedVector3(4200, 0, 0),
            BodyYawUnits = 900000,
            Stance = PlayerStance.Standing,
            Locomotion = LocomotionState.Running,
            Grounded = true,
            Revision = 120
        };
        Assert.Equal("a0571b26160b1139c8ead1a1e00b23df80b057d07418a21097b4ebad66b4b661", player.ComputeChecksum().ToString());
    }

    [Fact]
    public void CarryLedgerRejectsDuplicateOwnership()
    {
        var ledger = new PlayerCarryLedger();
        StableId id = StableIdFactory.Create("test", "object");
        Assert.True(ledger.Add(id));
        Assert.False(ledger.Add(id));
        Assert.Single(ledger.ObjectIds);
    }

    [Fact]
    public void FixedStepMovementIsRepeatable()
    {
        StateChecksum first = RunStraightLine();
        StateChecksum second = RunStraightLine();
        Assert.Equal(first, second);
    }

    [Fact]
    public void CrouchChangesCapsuleStance()
    {
        var player = CreatePlayer();
        var motor = new KinematicPlayerMotor();
        motor.Tick(player, new PlayerIntentFrame(0, 0, 0, 0, PlayerIntentButtons.CrouchPressed, InputDeviceKind.KeyboardMouse), new FlatCollisionWorld(), 60);
        Assert.Equal(PlayerStance.Crouched, player.Stance);
    }

    [Fact]
    public void JumpLeavesTheGround()
    {
        var player = CreatePlayer();
        var motor = new KinematicPlayerMotor();
        motor.Tick(player, new PlayerIntentFrame(0, 0, 0, 0, PlayerIntentButtons.None, InputDeviceKind.KeyboardMouse), new FlatCollisionWorld(), 60);
        motor.Tick(player, new PlayerIntentFrame(0, 0, 0, 0, PlayerIntentButtons.JumpPressed, InputDeviceKind.KeyboardMouse), new FlatCollisionWorld(), 60);
        Assert.False(player.Grounded);
        Assert.True(player.Velocity.YMillimeters > 0);
    }

    [Fact]
    public void LookPitchCanReachUpperFloors()
    {
        var player=CreatePlayer();var motor=new KinematicPlayerMotor();
        for(int i=0;i<25;i++)motor.Tick(player,new PlayerIntentFrame(0,0,0,30000,PlayerIntentButtons.None,InputDeviceKind.KeyboardMouse),new FlatCollisionWorld(),60);
        Assert.InRange(player.ViewPitchUnits,700000,850000);
    }


    [Fact]
    public void LevelGroundDoesNotTurnWallCollisionIntoAFreeStep()
    {
        var player = CreatePlayer();
        var motor = new KinematicPlayerMotor();
        var collision = new WallOnFlatGroundCollisionWorld();
        var intent = new PlayerIntentFrame(0, PlayerIntentFrame.AxisMaximum, 0, 0, PlayerIntentButtons.None, InputDeviceKind.KeyboardMouse);

        for (int tick = 0; tick < 90; tick++) motor.Tick(player, intent, collision, 60);

        Assert.True(player.FeetPosition.ZMillimeters > -100, $"Player tunneled through the wall to Z={player.FeetPosition.ZMillimeters}mm.");
        Assert.False(motor.Telemetry.StepAttempted);
    }

    private static StateChecksum RunStraightLine()
    {
        var player = CreatePlayer();
        var motor = new KinematicPlayerMotor();
        var collision = new FlatCollisionWorld();
        var intent = new PlayerIntentFrame(0, PlayerIntentFrame.AxisMaximum, 0, 0, PlayerIntentButtons.None, InputDeviceKind.KeyboardMouse);
        for (int tick = 0; tick < 180; tick++) motor.Tick(player, intent, collision, 60);
        return player.ComputeChecksum();
    }

    private static PlayerState CreatePlayer() => new(StableIdFactory.Create("test", "motor-player"), new FixedVector3(0, 0, 0));


    private sealed class WallOnFlatGroundCollisionWorld : IPlayerCollisionWorld
    {
        public bool CastCapsule(FixedVector3 feetStart, FixedVector3 feetEnd, int radiusMillimeters, int heightMillimeters, CollisionQueryMask mask, out PlayerCollisionHit hit)
        {
            bool movingHorizontally = feetEnd.XMillimeters != feetStart.XMillimeters || feetEnd.ZMillimeters != feetStart.ZMillimeters;
            if (movingHorizontally)
            {
                hit = new PlayerCollisionHit(
                    PlayerCollisionKind.Structure,
                    StableIdFactory.Create("test", "flat-ground-wall"),
                    feetStart,
                    0, 0, 1,
                    1,
                    2800);
                return true;
            }
            hit = default;
            return false;
        }

        public GroundContact ProbeGround(FixedVector3 feetPosition, int maximumRiseMillimeters, int maximumDropMillimeters) =>
            new(true, 0, 0, 1, 0, StableId.Empty, true);

        public bool Raycast(FixedVector3 origin, FixedVector3 direction, int maximumDistanceMillimeters, CollisionQueryMask mask, out PlayerCollisionHit hit)
        {
            hit = default;
            return false;
        }

        public bool HasStandingClearance(FixedVector3 feetPosition, int radiusMillimeters, int heightMillimeters) => true;
    }

    private sealed class FlatCollisionWorld : IPlayerCollisionWorld
    {
        public bool CastCapsule(FixedVector3 feetStart, FixedVector3 feetEnd, int radiusMillimeters, int heightMillimeters, CollisionQueryMask mask, out PlayerCollisionHit hit)
        {
            hit = default;
            return false;
        }

        public GroundContact ProbeGround(FixedVector3 feetPosition, int maximumRiseMillimeters, int maximumDropMillimeters) =>
            new(true, 0, 0, 1, 0, StableId.Empty, true);

        public bool Raycast(FixedVector3 origin, FixedVector3 direction, int maximumDistanceMillimeters, CollisionQueryMask mask, out PlayerCollisionHit hit)
        {
            hit = default;
            return false;
        }

        public bool HasStandingClearance(FixedVector3 feetPosition, int radiusMillimeters, int heightMillimeters) => true;
    }
}
