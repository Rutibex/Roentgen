using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Navigation;
using Roentgen.World;

namespace Roentgen.AI;

public interface INavigationMovementQuery
{
    bool TryMove(CharacterState character, FixedVector3 desiredStep, NavigationAgentProfile profile, out FixedVector3 actualStep);
}

public sealed class UnconstrainedNavigationMovementQuery : INavigationMovementQuery
{
    public static UnconstrainedNavigationMovementQuery Instance { get; } = new();
    public bool TryMove(CharacterState character, FixedVector3 desiredStep, NavigationAgentProfile profile, out FixedVector3 actualStep) { actualStep = desiredStep; return true; }
}

public enum NavigationInteractionStatus
{
    Completed = 0,
    Pending = 1,
    Failed = 2
}

public readonly record struct NavigationInteractionResult(NavigationInteractionStatus Status, string Message)
{
    public static NavigationInteractionResult Completed(string message = "interaction complete") => new(NavigationInteractionStatus.Completed, message);
    public static NavigationInteractionResult Pending(string message = "interaction pending") => new(NavigationInteractionStatus.Pending, message);
    public static NavigationInteractionResult Failed(string message) => new(NavigationInteractionStatus.Failed, message);
}

public interface INavigationInteractionHandler
{
    NavigationInteractionResult Execute(StableId agentId, NavigationRouteStep step, NavigationAgentProfile profile, ulong tick);
}

public sealed class AssumedNavigationInteractionHandler : INavigationInteractionHandler
{
    public static AssumedNavigationInteractionHandler Instance { get; } = new();
    public NavigationInteractionResult Execute(StableId agentId, NavigationRouteStep step, NavigationAgentProfile profile, ulong tick) => NavigationInteractionResult.Completed();
}

public sealed record NavigationMotorStatus(bool HasRoute, bool ReachedDestination, bool IsStuck, int StepIndex, StableId RequestId, NavigationFailureReason Failure, string Message);

public sealed class NavigationMotor
{
    private readonly HierarchicalNavigationService navigation;
    private readonly INavigationMovementQuery movement;
    private readonly INavigationInteractionHandler interactions;
    private NavigationRoute? route;
    private NavigationLocation destination;
    private int stepIndex;
    private int stuckTicks;
    private FixedVector3 lastPosition;
    private ulong nextReplanTick;

    public NavigationMotor(
        HierarchicalNavigationService navigation,
        INavigationMovementQuery? movement = null,
        INavigationInteractionHandler? interactions = null)
    {
        this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        this.movement = movement ?? UnconstrainedNavigationMovementQuery.Instance;
        this.interactions = interactions ?? AssumedNavigationInteractionHandler.Instance;
    }

    public NavigationMotorStatus Status { get; private set; } = new(false, false, false, 0, StableId.Empty, NavigationFailureReason.None, "idle");
    public NavigationRoute? CurrentRoute => route;

    public NavigationMotorStatus SetDestination(CharacterState character, NavigationLocation target, NavigationAgentProfile profile, ulong tick, bool force = false)
    {
        if (!force && route is not null && target == destination && route.NavigationRevision == navigation.Revision) return Status;
        destination = target;
        WorldPoint startPoint = new(checked((int)(character.FeetPosition.XMillimeters / 10)), checked((int)(character.FeetPosition.ZMillimeters / 10)));
        NavigationLocation start = navigation.ResolveLocation(startPoint, checked((int)(character.FeetPosition.YMillimeters / 10)));
        StableId requestId = StableIdFactory.CreateChild(character.Id, "navigation-request", $"{tick}:{target.WorldPosition.XCentimeters}:{target.WorldPosition.ZCentimeters}:{target.StructureId}:{target.CellId}");
        var request = new NavigationRequest(requestId, character.Id, start, target, profile, navigation.Revision, AllowPartialRoute: true);
        route = navigation.Find(request);
        stepIndex = 0; stuckTicks = 0; lastPosition = character.FeetPosition; nextReplanTick = tick + 30;
        Status = new(route.Success, false, false, 0, requestId, route.Failure, route.Message);
        return Status;
    }

    public NavigationMotorStatus Tick(CharacterState character, NavigationAgentProfile profile, ulong tick, bool run)
    {
        if (route is null || !route.Success || route.Steps.Count == 0) return Status;
        if (route.NavigationRevision != navigation.Revision && tick >= nextReplanTick) return SetDestination(character, destination, profile, tick, true);
        while (stepIndex < route.Steps.Count)
        {
            NavigationRouteStep routeStep = route.Steps[stepIndex];
            FixedVector3 target = new((long)routeStep.Location.WorldPosition.XCentimeters * 10, (long)routeStep.Location.ElevationCentimeters * 10, (long)routeStep.Location.WorldPosition.ZCentimeters * 10);
            FixedVector3 delta = target - character.FeetPosition;
            double horizontal = Math.Sqrt((double)delta.XMillimeters * delta.XMillimeters + (double)delta.ZMillimeters * delta.ZMillimeters);
            int arrivalRange = Math.Max(250, profile.RadiusMillimeters);

            if (RequiresInteraction(routeStep) && horizontal <= Math.Max(1_200, profile.RadiusMillimeters * 3))
            {
                NavigationInteractionResult interaction = interactions.Execute(character.Id, routeStep, profile, tick);
                if (interaction.Status == NavigationInteractionStatus.Failed)
                {
                    route = null;
                    Status = new(false, false, false, stepIndex, Status.RequestId, NavigationFailureReason.CapabilityMissing, interaction.Message);
                    return Status;
                }
                if (interaction.Status == NavigationInteractionStatus.Pending)
                {
                    character.Velocity = FixedVector3.Zero;
                    character.Action = CharacterActionKind.Idle;
                    character.Revision++;
                    Status = new(true, false, false, stepIndex, route.RequestId, NavigationFailureReason.None, interaction.Message);
                    return Status;
                }
            }

            if (horizontal <= arrivalRange && IsVerticalTransition(routeStep.Kind) && Math.Abs(delta.YMillimeters) > 80)
            {
                TickVerticalTransition(character, routeStep.Kind, delta.YMillimeters);
                Status = new(true, false, false, stepIndex, route.RequestId, NavigationFailureReason.None, "traversing vertical link");
                return Status;
            }

            if (horizontal <= arrivalRange && Math.Abs(delta.YMillimeters) <= Math.Max(120, profile.MaximumStepMillimeters))
            {
                stepIndex++;
                continue;
            }

            int speed = run && profile.Has(NavigationCapability.Run) ? profile.RunSpeedMillimetersPerSecond : profile.WalkSpeedMillimetersPerSecond;
            double scale = speed / 60.0 / Math.Max(1.0, horizontal);
            var desired = new FixedVector3((long)Math.Round(delta.XMillimeters * scale), 0, (long)Math.Round(delta.ZMillimeters * scale));
            if (!movement.TryMove(character, desired, profile, out FixedVector3 actual)) actual = FixedVector3.Zero;
            character.FeetPosition += actual;
            character.Velocity = new FixedVector3(actual.XMillimeters * 60, actual.YMillimeters * 60, actual.ZMillimeters * 60);
            character.Action = run ? CharacterActionKind.Run : CharacterActionKind.Walk;
            Face(character, delta);
            character.LocomotionPhasePermille = (character.LocomotionPhasePermille + Math.Max(1, speed / 150)) % 1000;
            character.Revision++;
            if (DistanceSquared(character.FeetPosition, lastPosition) < 25L * 25L) stuckTicks++; else stuckTicks = 0;
            lastPosition = character.FeetPosition;
            if (stuckTicks >= 60 && tick >= nextReplanTick)
            {
                nextReplanTick = tick + 60;
                NavigationMotorStatus replanned = SetDestination(character, destination, profile, tick, true);
                Status = replanned with { IsStuck = !replanned.HasRoute, Message = replanned.HasRoute ? "route replanned" : "stuck and replan failed" };
                return Status;
            }
            Status = new(true, false, stuckTicks >= 30, stepIndex, route.RequestId, NavigationFailureReason.None, "following route");
            return Status;
        }
        character.Velocity = FixedVector3.Zero;
        character.Action = CharacterActionKind.Idle;
        character.Revision++;
        Status = new(true, true, false, stepIndex, route.RequestId, NavigationFailureReason.None, "destination reached");
        return Status;
    }

    public void Stop(CharacterState character)
    {
        route = null; stepIndex = 0; stuckTicks = 0;
        character.Velocity = FixedVector3.Zero; character.Action = CharacterActionKind.Idle; character.Revision++;
        Status = new(false, false, false, 0, StableId.Empty, NavigationFailureReason.None, "stopped");
    }

    private static bool RequiresInteraction(NavigationRouteStep step) => !step.InteractionTargetId.IsEmpty && step.Kind is NavigationStepKind.OpenDoor or NavigationStepKind.UnlockDoor or NavigationStepKind.ForceDoor;
    private static bool IsVerticalTransition(NavigationStepKind kind) => kind is NavigationStepKind.ClimbLadder or NavigationStepKind.UseStairs or NavigationStepKind.Drop;

    private static void TickVerticalTransition(CharacterState character, NavigationStepKind kind, long verticalDelta)
    {
        int rate = kind switch
        {
            NavigationStepKind.Drop => 150,
            NavigationStepKind.ClimbLadder => 55,
            _ => 40
        };
        long step = Math.Clamp(verticalDelta, -(long)rate, (long)rate);
        character.FeetPosition += new FixedVector3(0, step, 0);
        character.Velocity = new FixedVector3(0, step * 60, 0);
        character.Action = kind == NavigationStepKind.Drop ? CharacterActionKind.Fall : kind == NavigationStepKind.ClimbLadder ? CharacterActionKind.Climb : CharacterActionKind.Walk;
        character.LocomotionPhasePermille = (character.LocomotionPhasePermille + 18) % 1000;
        character.Revision++;
    }

    private static void Face(CharacterState character, FixedVector3 direction)
    {
        if (direction.XMillimeters == 0 && direction.ZMillimeters == 0) return;
        character.FacingYawUnits = (int)Math.Round(Math.Atan2(direction.XMillimeters, direction.ZMillimeters) * 10000.0, MidpointRounding.AwayFromZero);
    }

    private static long DistanceSquared(FixedVector3 left, FixedVector3 right)
    {
        long dx = left.XMillimeters - right.XMillimeters; long dz = left.ZMillimeters - right.ZMillimeters;
        return checked(dx * dx + dz * dz);
    }
}

/// <summary>Uses the same kinematic capsule queries as the player controller.</summary>
public sealed class CollisionNavigationMovementQuery(Roentgen.Physics.IPlayerCollisionWorld collisionWorld) : INavigationMovementQuery
{
    public bool TryMove(CharacterState character, FixedVector3 desiredStep, NavigationAgentProfile profile, out FixedVector3 actualStep)
    {
        FixedVector3 requested = character.FeetPosition + desiredStep;
        if (collisionWorld.CastCapsule(character.FeetPosition, requested, profile.RadiusMillimeters, profile.StandingHeightMillimeters, Roentgen.Physics.CollisionQueryMask.All, out Roentgen.Physics.PlayerCollisionHit hit))
        {
            long length = (long)Math.Round(Math.Sqrt((double)desiredStep.XMillimeters * desiredStep.XMillimeters + (double)desiredStep.ZMillimeters * desiredStep.ZMillimeters), MidpointRounding.AwayFromZero);
            long allowed = Math.Max(0, hit.TravelMillimeters - Math.Max(25, profile.RadiusMillimeters / 4));
            if (length <= 0 || allowed <= 0) { actualStep = FixedVector3.Zero; return false; }
            double scale = Math.Min(1.0, allowed / (double)length);
            actualStep = new FixedVector3((long)Math.Round(desiredStep.XMillimeters * scale), 0, (long)Math.Round(desiredStep.ZMillimeters * scale));
        }
        else actualStep = desiredStep;
        FixedVector3 next = character.FeetPosition + actualStep;
        Roentgen.Physics.GroundContact ground = collisionWorld.ProbeGround(next, profile.MaximumStepMillimeters, profile.MaximumDropMillimeters);
        if (ground.Valid) actualStep = new FixedVector3(actualStep.XMillimeters, ground.HeightMillimeters - character.FeetPosition.YMillimeters, actualStep.ZMillimeters);
        return actualStep != FixedVector3.Zero;
    }
}
