using Microsoft.Xna.Framework;
using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.Rendering;

public sealed record FirstPersonCameraSettings(float FieldOfViewDegrees=75f,float NearPlane=0.04f,float FarPlane=1800f,float HeadBobIntensity=0.35f,float LandingMotionIntensity=0.25f);

public sealed class FirstPersonCameraRig
{
    private readonly FirstPersonCameraSettings _settings;private Vector3 _smoothedPosition;private bool _initialized;private double _bobPhase;
    public FirstPersonCameraRig(FirstPersonCameraSettings? settings=null)=>_settings=settings??new FirstPersonCameraSettings();
    public CameraState Build(PlayerState player,float interpolationAlpha,float aspectRatio,int standingEyeMm=1630,int crouchedEyeMm=1030)
    {
        _=interpolationAlpha;_ = aspectRatio;int eye=player.Stance==PlayerStance.Standing?standingEyeMm:crouchedEyeMm;Vector3 desired=new(player.FeetPosition.XMillimeters/1000f,(player.FeetPosition.YMillimeters+eye)/1000f,player.FeetPosition.ZMillimeters/1000f);
        float horizontalSpeed=MathF.Sqrt(player.Velocity.XMillimeters*player.Velocity.XMillimeters+player.Velocity.ZMillimeters*player.Velocity.ZMillimeters)/1000f;if(player.Grounded&&horizontalSpeed>0.2f)_bobPhase+=horizontalSpeed*0.055;float bob=(float)Math.Sin(_bobPhase)*0.025f*_settings.HeadBobIntensity*Math.Clamp(horizontalSpeed/4f,0,1);desired.Y+=bob;
        if(!_initialized){_smoothedPosition=desired;_initialized=true;}else _smoothedPosition=new Vector3(desired.X,MathHelper.Lerp(_smoothedPosition.Y,desired.Y,0.65f),desired.Z);
        float yaw=MathHelper.ToRadians(player.BodyYawUnits/10000f);float pitch=MathHelper.ToRadians(player.ViewPitchUnits/10000f);Vector3 forward=Vector3.Normalize(new Vector3(MathF.Sin(yaw)*MathF.Cos(pitch),MathF.Sin(pitch),-MathF.Cos(yaw)*MathF.Cos(pitch)));
        return new CameraState(_smoothedPosition,forward,Vector3.Up,MathHelper.ToRadians(_settings.FieldOfViewDegrees),_settings.NearPlane,_settings.FarPlane);
    }
}
