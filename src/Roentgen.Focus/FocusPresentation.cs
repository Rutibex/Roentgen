using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.Focus;
public sealed record FocusCameraState(FixedVector3 Pivot,int YawUnits,int PitchUnits,int DistanceMillimeters,StableId TargetId,StableId TargetNodeId);
public sealed record FocusPresentationSnapshot(FocusTemporalMode Mode,ulong FrozenTick,FocusCameraState Camera,FocusResourceState Resource,FocusTargetGraph? TargetGraph,FocusActionForecast? Forecast,IReadOnlyList<DraftFocusAction> Queue,FocusAccessibilitySettings Accessibility);
public sealed class FocusTacticalCamera
{
    public FocusCameraState State{get;private set;}=new(FixedVector3.Zero,0,-1800,4500,StableId.Empty,StableId.Empty);
    public void Select(StableId target,StableId node,FixedVector3 pivot)=>State=State with{TargetId=target,TargetNodeId=node,Pivot=pivot};
    public void Orbit(int yawDelta,int pitchDelta)=>State=State with{YawUnits=State.YawUnits+yawDelta,PitchUnits=Math.Clamp(State.PitchUnits+pitchDelta,-8500,2500)};
    public void Zoom(int delta)=>State=State with{DistanceMillimeters=Math.Clamp(State.DistanceMillimeters+delta,1800,9000)};
}
