using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.World.Structures;

namespace Roentgen.Physics.Tests;

public sealed class WallContactRegressionTests
{
    private static readonly StableId Id = new(1, 1);
    private static StructureCollisionData Wall => new(Id, new[] {
        new StaticStructureCollider(Id, Id, Id, new StructureBox(new(100, 0, -1000), new(120, 300, 1000)), StructurePrimitiveKind.Wall, true, true) });

    [Fact]
    public void SweepStopsAtNearWallFaceEvenAtHighSpeed()
    {
        Assert.True(StructureCollisionQueries.SweepCapsule(Wall, new(0, 89, 0), new(500, 89, 0), 34, 89, out var hit));
        Assert.Equal(66, hit.TravelCentimeters);
        Assert.Equal(-1, hit.Normal.X);
    }

    [Theory]
    [InlineData(66)] // exactly on expanded boundary
    [InlineData(67)] // quantization/streaming overlap
    [InlineData(105)] // inside physical wall
    public void InitialContactBlocksInwardButAllowsEscapeAndSliding(int x)
    {
        Assert.True(StructureCollisionQueries.SweepCapsule(Wall, new(x, 89, 0), new(x + 5, 89, 0), 34, 89, out var hit));
        Assert.Equal(0, hit.TravelCentimeters);
        Assert.Equal(-1, hit.Normal.X);
        Assert.False(StructureCollisionQueries.SweepCapsule(Wall, new(x, 89, 0), new(x - 5, 89, 0), 34, 89, out _));
        Assert.False(StructureCollisionQueries.SweepCapsule(Wall, new(x, 89, 0), new(x, 89, 5), 34, 89, out _));
    }

    [Fact]
    public void DiagonalInputSlidesAlongWallOverManyTicks()
    {
        var state = new PlayerState(Id, new(640, 0, 0));
        var motor = new KinematicPlayerMotor();
        var world = new FlatWallWorld();
        var intent = new PlayerIntentFrame(short.MaxValue, short.MaxValue, 0, 0, PlayerIntentButtons.None, default);
        for (int i = 0; i < 90; i++) motor.Tick(state, intent, world, 60);
        Assert.InRange(state.FeetPosition.XMillimeters, 600, 660);
        Assert.True(state.FeetPosition.ZMillimeters < -2000, $"Failed to slide: {state.FeetPosition}");
        var escape = intent with { MoveX = -short.MaxValue, MoveY = 0 };
        for (int i = 0; i < 30; i++) motor.Tick(state, escape, world, 60);
        Assert.True(state.FeetPosition.XMillimeters < 0);
    }

    private sealed class FlatWallWorld : IPlayerCollisionWorld
    {
        private readonly PlayerCollisionWorld world;
        public FlatWallWorld()
        {
            var structures = new StructureCollisionRegistry();
            structures.Register(Wall);
            world = new(new TerrainCollisionRegistry(6400), structures, new DynamicDoorCollisionRegistry());
        }
        public bool CastCapsule(FixedVector3 start, FixedVector3 end, int radius, int height, CollisionQueryMask mask, out PlayerCollisionHit hit) => world.CastCapsule(start, end, radius, height, mask, out hit);
        public GroundContact ProbeGround(FixedVector3 feet, int rise, int drop) => new(true, 0, 0, 1, 0, default, true);
        public bool Raycast(FixedVector3 origin, FixedVector3 direction, int distance, CollisionQueryMask mask, out PlayerCollisionHit hit) => world.Raycast(origin, direction, distance, mask, out hit);
        public bool HasStandingClearance(FixedVector3 feet, int radius, int height) => world.HasStandingClearance(feet, radius, height);
    }
}
