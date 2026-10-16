using Roentgen.Contracts;
using Roentgen.Combat;
using Roentgen.Entities;

namespace Roentgen.Focus;

public sealed record FocusExecutionResult(bool Complete, bool Interrupted, FocusInterruptionReason Reason, int NextActionIndex);
public sealed class FocusExecutionController
{
    private readonly CombatEncounterRuntime runtime;
    private readonly FocusResourceState resource;
    private readonly IReadOnlyList<CommittedFocusAction> actions;
    private int index;
    public FocusExecutionController(CombatEncounterRuntime runtime,FocusResourceState resource,IReadOnlyList<CommittedFocusAction> actions){this.runtime=runtime;this.resource=resource;this.actions=actions;}
    public int CurrentActionIndex=>index;
    public FocusExecutionResult Step()
    {
        if(index>=actions.Count)return new(true,false,FocusInterruptionReason.None,index);
        var item=actions[index];var a=item.Draft;
        if(!runtime.State.TryGetActor(a.ActorId,out var actor)||actor is null||actor.IsDead)return Interrupt(FocusInterruptionReason.PlayerDied);
        if(actor.Statuses.Any(s=>s.Kind is CombatStatusKind.Stunned or CombatStatusKind.Shocked && s.ExpiresTick>runtime.State.Tick))return Interrupt(FocusInterruptionReason.Stunned);
        bool accepted=a.Kind switch {
            FocusActionKind.Fire or FocusActionKind.Melee or FocusActionKind.Throw=>QueueAttack(actor,a),
            FocusActionKind.Reload=>runtime.QueueReload(a.ActorId),
            _=>false};
        if(!accepted)return Interrupt(FocusInterruptionReason.ActionDisabled);
        resource.ConsumeReserved(a.CostMilliunits);index++;return new(index>=actions.Count,false,FocusInterruptionReason.None,index);
    }
    private bool QueueAttack(Roentgen.Entities.CombatActorState actor,DraftFocusAction a)
    {
        if(!runtime.State.TryGetActor(a.TargetId,out var target)||target is null||target.IsDead)return false;
        var origin=actor.Presentation.FeetPosition+new FixedVector3(0,1400,0);var targetPoint=target.Presentation.FeetPosition+new FixedVector3(0,1000,0);
        if(target.TryGetBodyPart(a.TargetNodeId,out var part)&&part is not null)targetPoint=CombatSpatialModel.PartCenter(target,part.Definition.Region);
        return runtime.QueueAttack(a.ActorId,origin,targetPoint-origin,a.TargetId,a.TargetNodeId);
    }
    private FocusExecutionResult Interrupt(FocusInterruptionReason reason){int refund=actions.Skip(index).Sum(x=>x.Draft.CostMilliunits);resource.Release(refund);return new(false,true,reason,index);}
}

public sealed class FocusModeCoordinator
{
    public FocusTemporalMode Mode { get; private set; }
    public ulong FrozenSimulationTick { get; private set; }
    public FocusInterruptionReason LastInterruption { get; private set; }
    public void EnterPlanning(ulong simulationTick){if(Mode is not FocusTemporalMode.Inactive and not FocusTemporalMode.Completed and not FocusTemporalMode.Interrupted)throw new InvalidOperationException("Focus cannot be entered from the current mode.");FrozenSimulationTick=simulationTick;Mode=FocusTemporalMode.Planning;LastInterruption=FocusInterruptionReason.None;}
    public void BeginExecution(){if(Mode!=FocusTemporalMode.Planning&&Mode!=FocusTemporalMode.Committing)throw new InvalidOperationException("No Focus plan is ready.");Mode=FocusTemporalMode.Executing;}
    public bool ShouldAdvanceSimulation(int interfaceFrame,int divisor=4)=>Mode!=FocusTemporalMode.Planning&&(Mode!=FocusTemporalMode.Executing||interfaceFrame%Math.Max(1,divisor)==0);
    public void Complete(){Mode=FocusTemporalMode.Completed;}
    public void Interrupt(FocusInterruptionReason reason){LastInterruption=reason;Mode=FocusTemporalMode.Interrupted;}
    public void Exit(){Mode=FocusTemporalMode.Inactive;}
}
