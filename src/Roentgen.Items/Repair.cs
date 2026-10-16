using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public interface IItemSkillCapabilityProvider { int GetEffectiveSkill(StableId actorId, SkillId skill); }
public sealed record RepairPlan(StableId PlanId, StableId ActorId, StableId TargetItemId, IReadOnlyList<StableId> ComponentItemIds, StableId? ToolId, StableId? StationId, SkillId Skill, int Difficulty, int DurationTicks, int MaximumRestoredConditionPermille) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(PlanId); writer.WriteStableId(ActorId); writer.WriteStableId(TargetItemId); writer.WriteCount(ComponentItemIds.Count); foreach (StableId id in ComponentItemIds.Order()) writer.WriteStableId(id); writer.WriteBoolean(ToolId.HasValue); if (ToolId.HasValue) writer.WriteStableId(ToolId.Value); writer.WriteBoolean(StationId.HasValue); if (StationId.HasValue) writer.WriteStableId(StationId.Value); writer.WriteInt32((int)Skill); writer.WriteInt32(Difficulty); writer.WriteInt32(DurationTicks); writer.WriteInt32(MaximumRestoredConditionPermille); }
}
public sealed record RepairResult(bool Success, bool CostlySuccess, string Message, int RestoredPermille);

public sealed class RepairService
{
    private readonly ItemDefinitionCatalog catalog;
    private readonly IItemSkillCapabilityProvider capabilityProvider;

    public RepairService(ItemDefinitionCatalog catalog, IItemSkillCapabilityProvider capabilityProvider)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.capabilityProvider = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
    }

    public RepairResult Execute(ItemWorldState world, RepairPlan plan)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);
        if (!world.TryGetItem(plan.TargetItemId, out ItemInstanceState? target) || target is null) return new(false, false, "repair target missing", 0);
        if (target.Location.HolderId != plan.ActorId) return new(false, false, "repair target is not controlled by the actor", 0);
        if (plan.ComponentItemIds.Count == 0 || plan.ComponentItemIds.Distinct().Count() != plan.ComponentItemIds.Count) return new(false, false, "repair requires distinct compatible components", 0);

        ItemDefinition targetDefinition = catalog.Get(target.DefinitionId);
        foreach (StableId componentId in plan.ComponentItemIds.Order())
        {
            if (!world.TryGetItem(componentId, out ItemInstanceState? component) || component is null) return new(false, false, "repair component missing", 0);
            if (component.Id == target.Id || component.Location.HolderId != plan.ActorId) return new(false, false, "repair component is not available to the actor", 0);
            if (world.ReservedQuantity(component.Id) > 0) return new(false, false, "repair component is reserved", 0);
            ItemDefinition componentDefinition = catalog.Get(component.DefinitionId);
            if (!IsCompatible(targetDefinition, componentDefinition)) return new(false, false, $"'{componentDefinition.DisplayName}' is incompatible with '{targetDefinition.DisplayName}'", 0);
        }

        int skill = capabilityProvider.GetEffectiveSkill(plan.ActorId, plan.Skill);
        int margin = skill - plan.Difficulty;
        if (margin < -10)
        {
            foreach (StableId componentId in plan.ComponentItemIds.Take(1).ToArray()) world.RemoveItem(componentId);
            target.Condition.Damage(20, 15);
            target.Touch();
            return new(false, false, "repair failed", 0);
        }

        bool costly = margin < 0;
        int restored = Math.Clamp(120 + Math.Max(0, margin) * 4, 80, 400);
        if (costly) restored /= 2;
        foreach (StableId componentId in plan.ComponentItemIds) world.RemoveItem(componentId);
        target.Condition.Repair(restored, restored / 2, restored / 3, restored / 4, plan.MaximumRestoredConditionPermille);
        target.Touch();
        return new(true, costly, costly ? "costly repair" : "repair completed", restored);
    }

    private static bool IsCompatible(ItemDefinition target, ItemDefinition component)
    {
        if (component.Kind == ItemKind.Material)
            return target.Kind is ItemKind.Weapon or ItemKind.Armor or ItemKind.Tool or ItemKind.Container;
        if (component.Kind != ItemKind.Component || target.Kind != ItemKind.Weapon) return false;
        return component.Tags.Contains("component") && (target.Tags.Contains("weapon") || target.Tags.Overlaps(component.Tags));
    }
}
