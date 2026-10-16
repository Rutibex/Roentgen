using Roentgen.Contracts;
using Roentgen.Focus;
using Xunit;
namespace Roentgen.Focus.Tests;
public sealed class FocusTests
{
 [Fact] public void Planning_freezes_simulation_time(){var c=new FocusModeCoordinator();c.EnterPlanning(42);Assert.False(c.ShouldAdvanceSimulation(100));Assert.Equal(42UL,c.FrozenSimulationTick);}
 [Fact] public void Resource_reservation_is_bounded(){var r=new FocusResourceState{CurrentMilliunits=90000,MaximumMilliunits=90000};Assert.True(r.Reserve(20000));Assert.False(r.Reserve(80000));Assert.Equal(70000,r.AvailableMilliunits);}
 [Fact] public void Draft_plan_can_be_reordered(){var p=new DraftFocusPlan();var a=new DraftFocusAction(new(1,1),FocusActionKind.Reload,new(2,2),new(3,3),default,default,FocusTrackingPolicy.LockedAimPoint,FocusTargetLossPolicy.Cancel,1000);var b=a with{Id=new(1,2)};p.Add(a);p.Add(b);p.Move(1,0);Assert.Equal(b.Id,p.Actions[0].Id);}
 [Fact] public void Resolution_seed_domains_are_stable(){var s=new CombatResolutionSeed(1,2,3);Assert.Equal(s.Derive("spread"),s.Derive("spread"));Assert.NotEqual(s.Derive("spread"),s.Derive("jam"));}
 [Fact] public void Accessibility_values_are_clamped(){var a=new FocusAccessibilitySettings(DiagnosticIntensityPermille:5000,SlowMotionPermille:0).Clamped();Assert.Equal(1000,a.DiagnosticIntensityPermille);Assert.Equal(50,a.SlowMotionPermille);}
}
