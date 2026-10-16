using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Focus;

public sealed record DraftFocusAction(StableId Id, FocusActionKind Kind, StableId ActorId, StableId WeaponId, StableId TargetId, StableId TargetNodeId, FocusTrackingPolicy TrackingPolicy, FocusTargetLossPolicy LossPolicy, int CostMilliunits);
public sealed record CommittedFocusAction(DraftFocusAction Draft, CombatResolutionSeed Seed, ulong EarliestStartTick, bool Started=false, bool Completed=false, bool Cancelled=false);

public sealed class DraftFocusPlan
{
    private readonly List<DraftFocusAction> actions=[];
    public IReadOnlyList<DraftFocusAction> Actions=>actions;
    public int TotalCostMilliunits=>actions.Sum(x=>x.CostMilliunits);
    public void Add(DraftFocusAction action){if(action.Id.IsEmpty)throw new ArgumentException("Action ID cannot be empty.");if(actions.Any(x=>x.Id==action.Id))throw new InvalidOperationException("Duplicate Focus action ID.");actions.Add(action);}
    public bool Remove(StableId id)=>actions.RemoveAll(x=>x.Id==id)>0;
    public void Move(int from,int to){if(from<0||from>=actions.Count||to<0||to>=actions.Count)throw new ArgumentOutOfRangeException();var x=actions[from];actions.RemoveAt(from);actions.Insert(to,x);}
    public void Clear()=>actions.Clear();
}

public sealed record FocusPlanValidation(bool IsValid, IReadOnlyList<string> Errors);
public sealed class FocusPlanValidator
{
    public FocusPlanValidation Validate(DraftFocusPlan plan, FocusResourceState resource)
    {
        var errors=new List<string>(); if(plan.Actions.Count==0)errors.Add("The Focus plan is empty.");if(plan.Actions.Count>12)errors.Add("The Focus plan exceeds the bounded queue capacity.");
        if(plan.TotalCostMilliunits>resource.AvailableMilliunits)errors.Add("The Focus plan exceeds available Focus.");
        foreach(var a in plan.Actions){if(a.ActorId.IsEmpty)errors.Add($"Action {a.Id} has no actor.");if(a.Kind is FocusActionKind.Fire or FocusActionKind.Melee or FocusActionKind.Throw && a.TargetId.IsEmpty)errors.Add($"Action {a.Id} has no target.");}
        return new(errors.Count==0,errors);
    }
}

public sealed class FocusPlanCommitter
{
    public IReadOnlyList<CommittedFocusAction> Commit(DraftFocusPlan plan,FocusResourceState resource,ulong rootSeed,ulong tick)
    {
        var validation=new FocusPlanValidator().Validate(plan,resource);if(!validation.IsValid)throw new InvalidOperationException(string.Join(" ",validation.Errors));
        if(!resource.Reserve(plan.TotalCostMilliunits))throw new InvalidOperationException("Unable to reserve Focus.");
        ulong sequence=0;var result=new List<CommittedFocusAction>();ulong start=tick;
        foreach(var action in plan.Actions){var seed=new CombatResolutionSeed(rootSeed,sequence++,0);result.Add(new(action,seed,start));start+=1;}
        return result;
    }
}
