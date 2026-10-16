using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Items;

public enum ItemKind { Weapon=0, Ammunition=1, Armor=2, Consumable=3, Component=4, Material=5, Tool=6, Container=7, Currency=8, Quest=9, Document=10, Miscellaneous=11 }
public enum StackPolicy { Never=0, IdenticalDefinition=1, IdenticalBlueprint=2, FungibleBatch=3 }
public enum ItemLocationKind { World=0, Inventory=1, Container=2, Equipped=3, Merchant=4, CraftingReservation=5, TradeReservation=6, Destroyed=7 }
public enum EquipmentSlot { ActiveHand=0, OffHand=1, Sidearm=2, Longarm=3, Melee=4, Throwable=5, QuickConsumable1=6, QuickConsumable2=7, BackEquipment=8, Underlayer=20, Clothing=21, Torso=22, LeftArm=23, RightArm=24, LeftLeg=25, RightLeg=26, Head=27, Respirator=28, Utility=29, PoweredFrame=30 }
public enum WeaponComponentSlot { Receiver=0, Barrel=1, Grip=2, Stock=3, FeedSystem=4, Sight=5, PowerSystem=6, Modification=7 }
public enum ItemDamageKind { Kinetic=0, Cutting=1, Blunt=2, Thermal=3, Electrical=4, Radiological=5, Chemical=6, Biological=7, Sonic=8 }
public enum AmmunitionKind { Cartridge=0, Shell=1, EnergyCell=2, RadiographicCharge=3, ChemicalCanister=4, ExplosiveCharge=5, PoweredWeaponCell=6 }
public enum ItemArmorLayer { Underlayer=0, Clothing=1, PrimaryProtection=2, OuterPlate=3, PoweredFrame=4 }
public enum CraftingStationKind { Workbench=0, ChemistryStation=1, MedicalStation=2, EnergyBench=3, CookingFire=4, ArmorFrame=5, DiagnosticTable=6 }
public enum CraftingOperationKind { Assemble=0, Disassemble=1, Repair=2, Modify=3, Cook=4, Synthesize=5, Analyze=6 }
public enum RecipeKnowledgeSource { Background=0, Book=1, FactionTraining=2, ReverseEngineering=3, Experiment=4, PropertyDiscovery=5, Quest=6, WorldDiscovery=7 }
public enum ConsumableUseKind { Self=0, OtherActor=1, Item=2, Area=3 }
public enum ItemTransactionOperationKind { Create=0, Destroy=1, Transfer=2, Split=3, Merge=4, Equip=5, Unequip=6, Consume=7, LoadAmmunition=8, UnloadAmmunition=9, InstallComponent=10, RemoveComponent=11, DamageCondition=12, RepairCondition=13, Reserve=14, ReleaseReservation=15, ExchangeCurrency=16 }
public enum ItemTransactionStatus { Rejected=0, Committed=1 }
public enum EconomyCommodity { Food=0, Water=1, Medicine=2, Fuel=3, Ammunition=4, Metals=5, Chemicals=6, Electronics=7, Textiles=8, Weapons=9, Armor=10, LuxuryGoods=11, DiagnosticMaterials=12 }
public enum CurrencyPolicyKind { Scrip=0, Barter=1, Mixed=2, FactionCredit=3 }
public enum MerchantArchetype { General=0, ArmsDealer=1, Medic=2, Salvager=3 }

public readonly record struct FixedItemPosition(long XMillimeters, long YMillimeters, long ZMillimeters) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteInt64(XMillimeters); writer.WriteInt64(YMillimeters); writer.WriteInt64(ZMillimeters); }
    public static FixedItemPosition ReadCanonical(CanonicalReader reader) => new(reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64());
}

public sealed record ItemLocation(ItemLocationKind Kind, StableId HolderId, string? SlotId, FixedItemPosition? WorldPosition) : ICanonicalWritable
{
    public static ItemLocation InInventory(StableId ownerId) => new(ItemLocationKind.Inventory, ownerId, null, null);
    public static ItemLocation InContainer(StableId containerId) => new(ItemLocationKind.Container, containerId, null, null);
    public static ItemLocation Equipped(StableId actorId, EquipmentSlot slot) => new(ItemLocationKind.Equipped, actorId, slot.ToString(), null);
    public static ItemLocation InWorld(StableId worldId, FixedItemPosition position) => new(ItemLocationKind.World, worldId, null, position);
    public static ItemLocation Destroyed => new(ItemLocationKind.Destroyed, StableId.Empty, null, null);
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteInt32((int)Kind); writer.WriteStableId(HolderId); writer.WriteString(SlotId); writer.WriteBoolean(WorldPosition.HasValue); if (WorldPosition is { } p) p.WriteCanonical(writer);
    }
    public static ItemLocation ReadCanonical(CanonicalReader reader)
    {
        var kind = (ItemLocationKind)reader.ReadInt32();
        StableId holder = reader.ReadStableId();
        string? slot = reader.ReadString();
        FixedItemPosition? position = reader.ReadBoolean() ? FixedItemPosition.ReadCanonical(reader) : null;
        return new ItemLocation(kind, holder, slot, position);
    }
}

public sealed record ItemProvenance(string GeneratorId, int GeneratorVersion, CampaignSeed CampaignSeed, string Domain, string ManufacturerHistory, string FactionAlteration) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteString(GeneratorId); writer.WriteInt32(GeneratorVersion); writer.WriteUInt64(CampaignSeed.Value); writer.WriteString(Domain); writer.WriteString(ManufacturerHistory); writer.WriteString(FactionAlteration); }
    public static ItemProvenance ReadCanonical(CanonicalReader reader) => new(reader.ReadRequiredString(), reader.ReadInt32(), new CampaignSeed(reader.ReadUInt64()), reader.ReadRequiredString(), reader.ReadRequiredString(), reader.ReadRequiredString());
}

public sealed record ItemReservation(StableId ReservationId, StableId ItemId, int Quantity, string Purpose, StableId OwnerId, ulong ExpiresTick) : ICanonicalWritable
{
    public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(ReservationId); writer.WriteStableId(ItemId); writer.WriteInt32(Quantity); writer.WriteString(Purpose); writer.WriteStableId(OwnerId); writer.WriteUInt64(ExpiresTick); }
    public static ItemReservation ReadCanonical(CanonicalReader reader) => new(reader.ReadStableId(), reader.ReadStableId(), reader.ReadInt32(), reader.ReadRequiredString(), reader.ReadStableId(), reader.ReadUInt64());
}

public sealed record ItemValidationIssue(string Code, string Message, bool IsCritical);
public sealed record ItemValidationReport(bool IsValid, IReadOnlyList<ItemValidationIssue> Issues);
