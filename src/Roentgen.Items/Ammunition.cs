using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public sealed record AmmunitionTransferResult(bool Success, string Message, int Quantity, StableId? ReservationId = null);

public sealed class AmmunitionService
{
    private readonly ItemDefinitionCatalog itemDefinitions;
    private readonly IReadOnlyDictionary<string, AmmunitionDefinition> ammunitionDefinitions;
    public AmmunitionService(ItemDefinitionCatalog itemDefinitions, IReadOnlyDictionary<string, AmmunitionDefinition> ammunitionDefinitions) { this.itemDefinitions = itemDefinitions; this.ammunitionDefinitions = ammunitionDefinitions; }
    public AmmunitionTransferResult Load(ItemWorldState world, StableId weaponId, StableId ammunitionStackId, int quantity, StableId transactionId)
    {
        ItemInstanceState weapon = world.GetItem(weaponId); ItemInstanceState stack = world.GetItem(ammunitionStackId); if (weapon.AmmunitionLoad is null) return new(false, "weapon has no feed system", 0); if (!ammunitionDefinitions.TryGetValue(stack.DefinitionId, out AmmunitionDefinition? ammunition)) return new(false, "item is not ammunition", 0); if (itemDefinitions.Get(stack.DefinitionId).Kind != ItemKind.Ammunition) return new(false, "item kind is not ammunition", 0); if (weapon.RuntimeTags.Any(tag => tag.StartsWith("accepts:", StringComparison.Ordinal)) && !ammunition.CompatibilityTags.Any(tag => weapon.RuntimeTags.Contains($"accepts:{tag}"))) return new(false, "ammunition is incompatible", 0);
        int available = stack.Quantity - world.ReservedQuantity(stack.Id); int requested = Math.Min(Math.Max(0, quantity), available); if (requested <= 0) return new(false, "no ammunition available", 0); int loaded = weapon.AmmunitionLoad.Load(ammunition.Id, requested); if (loaded > 0) weapon.Touch(); if (loaded <= 0) return new(false, "feed is full or incompatible", 0);
        if (loaded == stack.Quantity) world.RemoveItem(stack.Id); else stack.SetQuantity(stack.Quantity - loaded); return new(true, "loaded", loaded);
    }
    public AmmunitionTransferResult ReserveForFocus(ItemWorldState world, StableId stackId, int quantity, StableId ownerId, StableId planId, ulong expiresTick)
    {
        ItemInstanceState stack = world.GetItem(stackId); int available = stack.Quantity - world.ReservedQuantity(stackId); if (quantity <= 0 || quantity > available) return new(false, "ammunition reservation unavailable", 0); StableId reservationId = StableIdFactory.CreateChild(planId, "ammunition-reservation", stackId.ToString()); world.AddReservation(new ItemReservation(reservationId, stackId, quantity, "focus", ownerId, expiresTick)); return new(true, "reserved", quantity, reservationId);
    }
}
