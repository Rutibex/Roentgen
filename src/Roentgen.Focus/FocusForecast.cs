using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Combat;

namespace Roentgen.Focus;

public interface IFocusCapabilityProvider
{
    int GetWeaponSkillPermille(StableId actorId, WeaponFamilyKind family);
    int GetAwarenessPermille(StableId actorId);
    int GetReflexPermille(StableId actorId);
    int GetCostModifierPermille(StableId actorId, FocusActionKind action);
}
public sealed class BaselineFocusCapabilityProvider : IFocusCapabilityProvider
{
    public int GetWeaponSkillPermille(StableId actorId, WeaponFamilyKind family)=>650;
    public int GetAwarenessPermille(StableId actorId)=>650;
    public int GetReflexPermille(StableId actorId)=>650;
    public int GetCostModifierPermille(StableId actorId, FocusActionKind action)=>1000;
}

public sealed class FocusForecastService
{
    private readonly AttackEvaluator evaluator = new();
    private readonly FocusCostCalculator costs = new();
    private readonly IFocusCapabilityProvider capabilities;
    public FocusForecastService(IFocusCapabilityProvider? capabilities=null)=>this.capabilities=capabilities??new BaselineFocusCapabilityProvider();

    public FocusActionForecast Forecast(CombatWorldState state, CombatDefinitionCatalog catalog, StableId actionId, StableId actorId, FocusTargetNode node, bool targetMoving=false, bool targetInCover=false)
    {
        var actor=state.GetActor(actorId); var weapon=state.GetWeapon(actor.EquippedWeaponId); var def=catalog.GetWeapon(weapon.DefinitionId); var target=state.GetActor(node.ActorId);
        long dx=target.Presentation.FeetPosition.XMillimeters-actor.Presentation.FeetPosition.XMillimeters;
        long dy=target.Presentation.FeetPosition.YMillimeters-actor.Presentation.FeetPosition.YMillimeters;
        long dz=target.Presentation.FeetPosition.ZMillimeters-actor.Presentation.FeetPosition.ZMillimeters;
        int distance=(int)Math.Min(int.MaxValue,Math.Sqrt((double)dx*dx+(double)dy*dy+(double)dz*dz));
        AttackEvaluation eval=evaluator.Evaluate(actor,weapon,def,distance,targetMoving,targetInCover);
        int sizePenalty=Math.Clamp(320-node.RadiusMillimeters,0,260);
        int skillBonus=(capabilities.GetWeaponSkillPermille(actorId,def.Family)-500)/2;
        int chance=Math.Clamp(eval.EstimatedHitChancePermille-sizePenalty+skillBonus,10,980);
        int raw=def.Damage.Sum(x=>x.Amount); int armorAbsorb=target.Armor.Count==0?0:(int)Math.Clamp(target.Armor.Average(x=>x.ConditionPermille)/3.0,0.0,700.0);
        int expected=raw*chance*(1000-armorAbsorb); // milliunits because /1000 and *1000 cancel
        var cost=costs.Calculate(def.AttackKind==CombatAttackKind.Melee?FocusActionKind.Melee:def.AttackKind is CombatAttackKind.Explosive or CombatAttackKind.Projectile?FocusActionKind.Throw:FocusActionKind.Fire,node.RadiusMillimeters,1000,actor.Presentation.Injury.PainPermille,capabilities.GetCostModifierPermille(actorId,FocusActionKind.Fire));
        var factors=eval.Factors.Select(x=>new ForecastFactor(x.Id,x.ModifierPermille,x.Explanation)).Concat(cost.Factors).OrderBy(x=>x.Id,StringComparer.Ordinal).ToArray();
        int injury=Math.Clamp(chance*node.ConsequenceWeightPermille/1000,0,950); int kill=node.Region is AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster or AnatomicalRegion.Torso?injury/3:injury/12;
        return new(actionId,actorId,node.ActorId,node.NodeId,chance,Math.Clamp(chance/8,0,150),expected,Math.Max(0,raw/4),raw,armorAbsorb,injury,kill/2,kill,cost.TotalMilliunits,
            node.Knowledge>=DiagnosticKnowledgeLevel.InternalStructure?FocusForecastConfidence.High:node.Knowledge>=DiagnosticKnowledgeLevel.ArmorMapped?FocusForecastConfidence.Medium:FocusForecastConfidence.Low,
            factors,[new("functional-damage",node.Consequences[0],injury,node.ConsequenceWeightPermille)],
            node.Knowledge<DiagnosticKnowledgeLevel.FullyDiagnosed?[new("diagnostic-knowledge","Internal structure is not fully diagnosed",250)]:Array.Empty<ForecastUncertainty>());
    }
}
