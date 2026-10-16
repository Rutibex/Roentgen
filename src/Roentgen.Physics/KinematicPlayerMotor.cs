using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.Physics;

public sealed record PlayerMotorSettings(
    int CapsuleRadiusMillimeters=340,
    int StandingHeightMillimeters=1780,
    int CrouchedHeightMillimeters=1160,
    int StandingEyeHeightMillimeters=1630,
    int CrouchedEyeHeightMillimeters=1030,
    int MaximumStepMillimeters=350,
    int MaximumGroundDropMillimeters=520,
    int WalkSpeedMillimetersPerSecond=2000,
    int RunSpeedMillimetersPerSecond=4200,
    int SprintSpeedMillimetersPerSecond=6000,
    int CrouchSpeedMillimetersPerSecond=1700,
    int GroundAccelerationMillimetersPerSecondSquared=18000,
    int AirAccelerationMillimetersPerSecondSquared=5000,
    int GravityMillimetersPerSecondSquared=15500,
    int JumpSpeedMillimetersPerSecond=5200,
    int MaximumSlopeDegrees=46,
    int MantleMaximumHeightMillimeters=1250,
    int CoyoteTicks=5,
    int JumpBufferTicks=6)
{
    public void Validate(){if(CapsuleRadiusMillimeters<=0||StandingHeightMillimeters<=CrouchedHeightMillimeters||CrouchedHeightMillimeters<=CapsuleRadiusMillimeters*2||MaximumStepMillimeters<0)throw new InvalidOperationException("Player motor settings are invalid.");}
}

public sealed record PlayerMotorTelemetry(FixedVector3 DesiredVelocity,FixedVector3 ResolvedVelocity,GroundContact Ground,PlayerCollisionHit? LastHit,bool StepAttempted,bool MantleAvailable);

public sealed class KinematicPlayerMotor
{
    private readonly PlayerMotorSettings _settings;private int _ticksSinceGrounded=int.MaxValue;private int _jumpBuffer;
    public KinematicPlayerMotor(PlayerMotorSettings? settings=null){_settings=settings??new PlayerMotorSettings();_settings.Validate();}
    public PlayerMotorSettings Settings=>_settings;public PlayerMotorTelemetry Telemetry{get;private set;}=new(FixedVector3.Zero,FixedVector3.Zero,default,null,false,false);
    public void Tick(PlayerState state,PlayerIntentFrame intent,IPlayerCollisionWorld collision,int ticksPerSecond)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(collision);if(ticksPerSecond<=0)throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        if(state.Traversal!=TraversalState.None){TickTraversal(state);return;}
        state.BodyYawUnits=Wrap(state.BodyYawUnits+intent.LookYawDelta);state.ViewPitchUnits=Math.Clamp(state.ViewPitchUnits+intent.LookPitchDelta,-850000,850000);
        if(intent.Buttons.HasFlag(PlayerIntentButtons.CrouchPressed))
        {
            if(state.Stance==PlayerStance.Standing)state.Stance=PlayerStance.Crouched;
            else if(collision.HasStandingClearance(state.FeetPosition,_settings.CapsuleRadiusMillimeters,_settings.StandingHeightMillimeters))state.Stance=PlayerStance.Standing;
        }
        if(intent.Buttons.HasFlag(PlayerIntentButtons.JumpPressed))_jumpBuffer=_settings.JumpBufferTicks;else if(_jumpBuffer>0)_jumpBuffer--;
        GroundContact ground=collision.ProbeGround(state.FeetPosition,_settings.MaximumStepMillimeters,_settings.MaximumGroundDropMillimeters);
        if(!ground.StreamingReady){state.Velocity=FixedVector3.Zero;state.Locomotion=LocomotionState.Idle;Telemetry=new PlayerMotorTelemetry(FixedVector3.Zero,FixedVector3.Zero,ground,null,false,false);return;}
        double slopeDegrees=ground.Valid?Math.Acos(Math.Clamp(ground.NormalY,-1d,1d))*180d/Math.PI:90d;
        bool walkableSlope=slopeDegrees<=_settings.MaximumSlopeDegrees;
        if(ground.Valid&&walkableSlope&&state.Velocity.YMillimeters<=0){state.FeetPosition=new FixedVector3(state.FeetPosition.XMillimeters,ground.HeightMillimeters,state.FeetPosition.ZMillimeters);state.Grounded=true;state.GroundEntityId=ground.EntityId;_ticksSinceGrounded=0;}else{state.Grounded=false;state.GroundEntityId=StableId.Empty;_ticksSinceGrounded=_ticksSinceGrounded == int.MaxValue ? int.MaxValue : _ticksSinceGrounded+1;if(ground.Valid&&!walkableSlope){state.Velocity=new FixedVector3(state.Velocity.XMillimeters+(long)Math.Round(ground.NormalX*-180),state.Velocity.YMillimeters,state.Velocity.ZMillimeters+(long)Math.Round(ground.NormalZ*-180));state.Locomotion=LocomotionState.Sliding;}}
        if(_jumpBuffer>0&&_ticksSinceGrounded<=_settings.CoyoteTicks){state.Velocity=new FixedVector3(state.Velocity.XMillimeters,_settings.JumpSpeedMillimetersPerSecond,state.Velocity.ZMillimeters);state.Grounded=false;_jumpBuffer=0;}
        double yaw=state.BodyYawUnits/10000.0*Math.PI/180.0;double forwardX=Math.Sin(yaw),forwardZ=-Math.Cos(yaw),rightX=Math.Cos(yaw),rightZ=Math.Sin(yaw);double inputX=intent.NormalizedMoveX,inputY=intent.NormalizedMoveY;double length=Math.Sqrt(inputX*inputX+inputY*inputY);if(length>1){inputX/=length;inputY/=length;}
        int speed=state.Stance==PlayerStance.Crouched?_settings.CrouchSpeedMillimetersPerSecond:intent.Buttons.HasFlag(PlayerIntentButtons.SprintHeld)?_settings.SprintSpeedMillimetersPerSecond:intent.Buttons.HasFlag(PlayerIntentButtons.WalkHeld)?_settings.WalkSpeedMillimetersPerSecond:_settings.RunSpeedMillimetersPerSecond;
        long desiredX=(long)Math.Round((rightX*inputX+forwardX*inputY)*speed);long desiredZ=(long)Math.Round((rightZ*inputX+forwardZ*inputY)*speed);int acceleration=state.Grounded?_settings.GroundAccelerationMillimetersPerSecondSquared:_settings.AirAccelerationMillimetersPerSecondSquared;
        long vx=Approach(state.Velocity.XMillimeters,desiredX,acceleration/ticksPerSecond);long vz=Approach(state.Velocity.ZMillimeters,desiredZ,acceleration/ticksPerSecond);long vy=state.Velocity.YMillimeters;if(!state.Grounded)vy-= _settings.GravityMillimetersPerSecondSquared/ticksPerSecond;else if(vy<0)vy=0;
        state.Velocity=new FixedVector3(vx,vy,vz);FixedVector3 displacement=new(vx/ticksPerSecond,vy/ticksPerSecond,vz/ticksPerSecond);FixedVector3 target=state.FeetPosition+displacement;int height=state.Stance==PlayerStance.Standing?_settings.StandingHeightMillimeters:_settings.CrouchedHeightMillimeters;PlayerCollisionHit? lastHit=null;bool step=false;bool mantle=false;
        if(collision.CastCapsule(state.FeetPosition,target,_settings.CapsuleRadiusMillimeters,height,CollisionQueryMask.Structures|CollisionQueryMask.Doors|CollisionQueryMask.Characters,out PlayerCollisionHit hit))
        {
            lastHit=hit;long moveLength=Math.Max(1,(long)Math.Sqrt(displacement.XMillimeters*displacement.XMillimeters+displacement.YMillimeters*displacement.YMillimeters+displacement.ZMillimeters*displacement.ZMillimeters));double fraction=Math.Clamp((hit.TravelMillimeters-12)/(double)moveLength,0,1);FixedVector3 partial=new((long)Math.Round(displacement.XMillimeters*fraction),(long)Math.Round(displacement.YMillimeters*fraction),(long)Math.Round(displacement.ZMillimeters*fraction));state.FeetPosition+=partial;
            if(state.Grounded)
            {
                // A step must actually raise the player's feet.  Previously any collision while
                // standing on level terrain was treated as a valid step because ProbeGround(target)
                // returned the same terrain height.  The zero-length capsule probe that followed
                // could not detect the wall/door, so the motor teleported to the far side.
                GroundContact raised=collision.ProbeGround(target,_settings.MaximumStepMillimeters,20);
                long stepRise=raised.Valid?(long)raised.HeightMillimeters-state.FeetPosition.YMillimeters:long.MinValue;
                if(raised.Valid&&stepRise>10&&stepRise<=_settings.MaximumStepMillimeters)
                {
                    FixedVector3 stepped=new(target.XMillimeters,raised.HeightMillimeters,target.ZMillimeters);
                    if(collision.HasStandingClearance(stepped,_settings.CapsuleRadiusMillimeters,height))
                    {
                        state.FeetPosition=stepped;
                        step=true;
                    }
                }
                int top=hit.SurfaceTopMillimeters;if(intent.Buttons.HasFlag(PlayerIntentButtons.JumpPressed)&&top>state.FeetPosition.YMillimeters&&top-state.FeetPosition.YMillimeters<=_settings.MantleMaximumHeightMillimeters){FixedVector3 mantleEnd=new(target.XMillimeters,top+20,target.ZMillimeters);if(collision.HasStandingClearance(mantleEnd,_settings.CapsuleRadiusMillimeters,height)){state.Traversal=TraversalState.Mantle;state.TraversalStart=state.FeetPosition;state.TraversalEnd=mantleEnd;state.TraversalProgressPermille=0;state.ControlMode=PlayerControlMode.Traversal;state.Locomotion=LocomotionState.Mantling;mantle=true;}}}
            if(!step&&!mantle)
            {
                // Spend the remaining tick moving along the surface. Merely changing
                // velocity left diagonal input pinned against the same wall every tick.
                FixedVector3 remaining = displacement - partial;
                PlayerCollisionHit contact = hit;
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    remaining = Slide(remaining, contact);
                    state.Velocity = Slide(state.Velocity, contact);
                    double remainingLength = Math.Sqrt((double)remaining.XMillimeters * remaining.XMillimeters + (double)remaining.YMillimeters * remaining.YMillimeters + (double)remaining.ZMillimeters * remaining.ZMillimeters);
                    if (remainingLength < 1) break;
                    if (!collision.CastCapsule(state.FeetPosition, state.FeetPosition + remaining, _settings.CapsuleRadiusMillimeters, height,
                        CollisionQueryMask.Structures | CollisionQueryMask.Doors | CollisionQueryMask.Characters, out contact))
                    {
                        state.FeetPosition += remaining;
                        break;
                    }
                    double travelFraction = Math.Clamp((contact.TravelMillimeters - 12) / remainingLength, 0, 1);
                    FixedVector3 advance = new((long)Math.Round(remaining.XMillimeters * travelFraction), (long)Math.Round(remaining.YMillimeters * travelFraction), (long)Math.Round(remaining.ZMillimeters * travelFraction));
                    state.FeetPosition += advance;
                    remaining -= advance;
                    lastHit = contact;
                }
            }
        }
        else state.FeetPosition=target;
        GroundContact after=collision.ProbeGround(state.FeetPosition,_settings.MaximumStepMillimeters,_settings.MaximumGroundDropMillimeters);if(after.Valid&&state.Velocity.YMillimeters<=0&&state.FeetPosition.YMillimeters-after.HeightMillimeters<=_settings.MaximumGroundDropMillimeters){state.FeetPosition=new FixedVector3(state.FeetPosition.XMillimeters,after.HeightMillimeters,state.FeetPosition.ZMillimeters);state.Velocity=new FixedVector3(state.Velocity.XMillimeters,0,state.Velocity.ZMillimeters);state.Grounded=true;ground=after;}
        state.Locomotion=state.Traversal!=TraversalState.None?state.Locomotion:!state.Grounded?LocomotionState.Airborne:state.Stance==PlayerStance.Crouched&&length>0.05?LocomotionState.Crouching:length<0.05?LocomotionState.Idle:intent.Buttons.HasFlag(PlayerIntentButtons.SprintHeld)?LocomotionState.Sprinting:intent.Buttons.HasFlag(PlayerIntentButtons.WalkHeld)?LocomotionState.Walking:LocomotionState.Running;state.Revision++;
        Telemetry=new PlayerMotorTelemetry(new FixedVector3(desiredX,vy,desiredZ),state.Velocity,ground,lastHit,step,mantle);
    }
    public void BeginLadder(PlayerState state,FixedVector3 end){state.Traversal=TraversalState.Ladder;state.TraversalStart=state.FeetPosition;state.TraversalEnd=end;state.TraversalProgressPermille=0;state.ControlMode=PlayerControlMode.Traversal;state.Locomotion=LocomotionState.Ladder;state.Velocity=FixedVector3.Zero;}
    private static void TickTraversal(PlayerState state){int increment=state.Traversal==TraversalState.Mantle?90:55;state.TraversalProgressPermille=Math.Min(1000,state.TraversalProgressPermille+increment);double t=state.TraversalProgressPermille/1000.0;state.FeetPosition=new FixedVector3(Lerp(state.TraversalStart.XMillimeters,state.TraversalEnd.XMillimeters,t),Lerp(state.TraversalStart.YMillimeters,state.TraversalEnd.YMillimeters,t),Lerp(state.TraversalStart.ZMillimeters,state.TraversalEnd.ZMillimeters,t));if(state.TraversalProgressPermille>=1000){state.Traversal=TraversalState.None;state.ControlMode=PlayerControlMode.Exploration;state.Locomotion=LocomotionState.Idle;state.Grounded=true;}state.Revision++;}
    private static FixedVector3 Slide(FixedVector3 value, PlayerCollisionHit hit)
    {
        double dot = value.XMillimeters * hit.NormalX + value.YMillimeters * hit.NormalY + value.ZMillimeters * hit.NormalZ;
        if (dot >= 0) return value;
        return new FixedVector3((long)Math.Round(value.XMillimeters - dot * hit.NormalX),
            (long)Math.Round(value.YMillimeters - dot * hit.NormalY), (long)Math.Round(value.ZMillimeters - dot * hit.NormalZ));
    }
    private static long Lerp(long a,long b,double t)=>(long)Math.Round(a+(b-a)*t);
    private static long Approach(long value,long target,long amount)=>value<target?Math.Min(target,value+amount):Math.Max(target,value-amount);
    private static int Wrap(int value){const int full=3600000;value%=full;return value<0?value+full:value;}
}
