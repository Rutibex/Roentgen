namespace Roentgen.World.Structures;

public static class StructureGrammarCatalog
{
    private static readonly IReadOnlyDictionary<BuildingPurpose, BuildingGrammar> Grammars = Create();

    public static BuildingGrammar Get(BuildingPurpose purpose) => Grammars.TryGetValue(purpose, out BuildingGrammar? grammar) ? grammar : Grammars[BuildingPurpose.GenericShelter];
    public static IReadOnlyCollection<BuildingGrammar> All => Grammars.Values.ToArray();

    private static IReadOnlyDictionary<BuildingPurpose, BuildingGrammar> Create()
    {
        var values = new[]
        {
            Clinic(), Residence(), BoardingHouse(), Workshop(), Warehouse(), Market(), GuardPost(), Intake(), Relay(), Generic()
        };
        foreach (BuildingGrammar grammar in values) grammar.Validate();
        return values.ToDictionary(static grammar => grammar.Purpose);
    }

    private static RoomRequirement R(string key, RoomArchetype type, int min, int preferred, AccessClass access = AccessClass.Public, bool exterior = false, bool critical = false, int floor = 0) =>
        new(key, type, access, access is AccessClass.Secure or AccessClass.Hazardous ? PrivacyClass.Restricted : PrivacyClass.Open, min, preferred, 200, floor, exterior, critical);

    private static BuildingGrammar G(string id, BuildingPurpose purpose, StructuralSystemKind system, int minW, int maxW, int minD, int maxD, int floors, int damage, RoomRequirement[] rooms, AdjacencyRule[] adjacency, bool alternate = false) =>
        new(id, 1, purpose, new FootprintRules(minW, maxW, minD, maxD), new FloorCountRules(1, floors, AllowsBasement: purpose == BuildingPurpose.IndustrialIntake, RequiresRoofAccess: purpose == BuildingPurpose.UtilityRelay),
            System(system), rooms, Array.Empty<RoomRequirement>(), adjacency, new NavigationRequirements(150, 100, alternate), new StructureBudget(32, 768, 64, 16), damage);

    private static StructuralSystemRules System(StructuralSystemKind kind) => kind switch
    {
        StructuralSystemKind.ScrapFrame => new(kind, 20, 300, 600),
        StructuralSystemKind.ReinforcedConcrete => new(kind, 30, 360, 900),
        StructuralSystemKind.IndustrialSteel => new(kind, 20, 420, 1200),
        _ => new(kind, 35, 320, 700)
    };

    private static BuildingGrammar Clinic() => G("prospect-clinic", BuildingPurpose.Clinic, StructuralSystemKind.ReinforcedConcrete, 1800, 2600, 1600, 2400, 2, 120,
        new[] { R("entrance", RoomArchetype.Entrance, 6, 8, exterior:true, critical:true), R("reception", RoomArchetype.Reception, 12, 18, critical:true), R("treatment", RoomArchetype.Treatment, 14, 20, AccessClass.Staff, critical:true), R("storage", RoomArchetype.Storage, 8, 12, AccessClass.Staff), R("staff", RoomArchetype.Staff, 10, 14, AccessClass.Staff), R("secure", RoomArchetype.SecureStorage, 8, 10, AccessClass.Secure, critical:true), R("service", RoomArchetype.Utility, 6, 8, AccessClass.Service, exterior:true) },
        new[] { new AdjacencyRule("entrance","reception",ConnectionRequirement.Required), new AdjacencyRule("reception","treatment",ConnectionRequirement.Required), new AdjacencyRule("treatment","storage",ConnectionRequirement.Required), new AdjacencyRule("treatment","staff",ConnectionRequirement.Required), new AdjacencyRule("storage","secure",ConnectionRequirement.Required, LockPolicy:LockPolicy.LockedWithAlternative), new AdjacencyRule("storage","service",ConnectionRequirement.Required) }, alternate:true);

    private static BuildingGrammar Residence() => G("small-residence", BuildingPurpose.Residence, StructuralSystemKind.ScrapFrame, 800, 1200, 800, 1200, 1, 80,
        new[] { R("entrance",RoomArchetype.Entrance,4,5,exterior:true), R("living",RoomArchetype.Living,12,16), R("sleeping",RoomArchetype.Sleeping,8,12,AccessClass.Staff), R("kitchen",RoomArchetype.Kitchen,6,9), R("sanitation",RoomArchetype.Sanitation,4,5,AccessClass.Staff) },
        new[] { new AdjacencyRule("entrance","living",ConnectionRequirement.Required), new AdjacencyRule("living","sleeping",ConnectionRequirement.Required), new AdjacencyRule("living","kitchen",ConnectionRequirement.Required), new AdjacencyRule("living","sanitation",ConnectionRequirement.Required) });

    private static BuildingGrammar BoardingHouse() => G("boarding-house", BuildingPurpose.BoardingHouse, StructuralSystemKind.MasonryBlock, 1400, 2000, 1200, 1800, 2, 100,
        new[] { R("entrance",RoomArchetype.Entrance,5,7,exterior:true), R("living",RoomArchetype.Living,16,24), R("sleep-a",RoomArchetype.Sleeping,10,14,AccessClass.Staff), R("sleep-b",RoomArchetype.Sleeping,10,14,AccessClass.Staff), R("kitchen",RoomArchetype.Kitchen,8,12), R("sanitation",RoomArchetype.Sanitation,5,7,AccessClass.Staff), R("storage",RoomArchetype.Storage,5,8,AccessClass.Service) },
        new[] { new AdjacencyRule("entrance","living",ConnectionRequirement.Required), new AdjacencyRule("living","sleep-a",ConnectionRequirement.Required), new AdjacencyRule("living","sleep-b",ConnectionRequirement.Required), new AdjacencyRule("living","kitchen",ConnectionRequirement.Required), new AdjacencyRule("living","sanitation",ConnectionRequirement.Required), new AdjacencyRule("kitchen","storage",ConnectionRequirement.Required) });

    private static BuildingGrammar Workshop() => G("salvage-workshop", BuildingPurpose.Workshop, StructuralSystemKind.IndustrialSteel, 1200, 1900, 1000, 1700, 1, 180,
        new[] { R("entrance",RoomArchetype.Entrance,5,7,exterior:true), R("workshop",RoomArchetype.Workshop,24,36,AccessClass.Staff,exterior:true,critical:true), R("storage",RoomArchetype.Storage,10,16,AccessClass.Service), R("utility",RoomArchetype.Utility,6,9,AccessClass.Service) },
        new[] { new AdjacencyRule("entrance","workshop",ConnectionRequirement.Required), new AdjacencyRule("workshop","storage",ConnectionRequirement.Required), new AdjacencyRule("workshop","utility",ConnectionRequirement.Required) });

    private static BuildingGrammar Warehouse() => G("warehouse", BuildingPurpose.Warehouse, StructuralSystemKind.IndustrialSteel, 1600, 2600, 1400, 2400, 1, 220,
        new[] { R("entrance",RoomArchetype.Entrance,5,8,exterior:true), R("warehouse",RoomArchetype.Warehouse,40,70,AccessClass.Service,exterior:true), R("office",RoomArchetype.Staff,8,12,AccessClass.Staff), R("secure",RoomArchetype.SecureStorage,8,12,AccessClass.Secure) },
        new[] { new AdjacencyRule("entrance","warehouse",ConnectionRequirement.Required), new AdjacencyRule("warehouse","office",ConnectionRequirement.Required), new AdjacencyRule("office","secure",ConnectionRequirement.Required,LockPolicy:LockPolicy.LockedWithAlternative) });

    private static BuildingGrammar Market() => G("market-shelter", BuildingPurpose.MarketShelter, StructuralSystemKind.ScrapFrame, 1200, 2200, 1000, 1800, 1, 50,
        new[] { R("entrance",RoomArchetype.Entrance,5,8,exterior:true), R("market",RoomArchetype.Market,28,45,exterior:true), R("storage",RoomArchetype.Storage,8,12,AccessClass.Service) },
        new[] { new AdjacencyRule("entrance","market",ConnectionRequirement.Required), new AdjacencyRule("market","storage",ConnectionRequirement.Required) });

    private static BuildingGrammar GuardPost() => G("guard-post", BuildingPurpose.GuardPost, StructuralSystemKind.MasonryBlock, 700, 1100, 700, 1100, 2, 120,
        new[] { R("entrance",RoomArchetype.Entrance,4,5,exterior:true), R("guard",RoomArchetype.Guard,10,14,AccessClass.Staff,exterior:true,critical:true), R("storage",RoomArchetype.Storage,5,7,AccessClass.Secure) },
        new[] { new AdjacencyRule("entrance","guard",ConnectionRequirement.Required), new AdjacencyRule("guard","storage",ConnectionRequirement.Required) });

    private static BuildingGrammar Intake() => G("blackwater-intake", BuildingPurpose.IndustrialIntake, StructuralSystemKind.IndustrialSteel, 2600, 3600, 2200, 3400, 3, 620,
        new[] { R("entrance",RoomArchetype.Entrance,8,12,exterior:true,critical:true), R("machinery",RoomArchetype.Machinery,50,80,AccessClass.Hazardous,exterior:true,critical:true), R("control",RoomArchetype.Control,14,20,AccessClass.Secure,critical:true), R("maintenance",RoomArchetype.Maintenance,18,28,AccessClass.Service,critical:true), R("utility",RoomArchetype.Utility,12,18,AccessClass.Service), R("storage",RoomArchetype.Storage,12,18,AccessClass.Service), R("roof",RoomArchetype.RoofAccess,5,7,AccessClass.Service,floor:1) },
        new[] { new AdjacencyRule("entrance","machinery",ConnectionRequirement.Required), new AdjacencyRule("machinery","control",ConnectionRequirement.Required,LockPolicy:LockPolicy.LockedWithAlternative), new AdjacencyRule("machinery","maintenance",ConnectionRequirement.Required), new AdjacencyRule("maintenance","utility",ConnectionRequirement.Required), new AdjacencyRule("maintenance","storage",ConnectionRequirement.Required), new AdjacencyRule("control","roof",ConnectionRequirement.Required,ConnectionKind.Stair) }, alternate:true);

    private static BuildingGrammar Relay() => G("meridian-relay", BuildingPurpose.UtilityRelay, StructuralSystemKind.ReinforcedConcrete, 1000, 1500, 900, 1400, 2, 300,
        new[] { R("entrance",RoomArchetype.Entrance,5,7,exterior:true), R("utility",RoomArchetype.Utility,18,26,AccessClass.Service,critical:true), R("control",RoomArchetype.Control,10,14,AccessClass.Secure,critical:true), R("roof",RoomArchetype.RoofAccess,5,7,AccessClass.Service,floor:1) },
        new[] { new AdjacencyRule("entrance","utility",ConnectionRequirement.Required), new AdjacencyRule("utility","control",ConnectionRequirement.Required,LockPolicy:LockPolicy.SkillGateWithAlternative), new AdjacencyRule("control","roof",ConnectionRequirement.Required,ConnectionKind.Ladder) }, alternate:true);

    private static BuildingGrammar Generic() => G("generic-shelter", BuildingPurpose.GenericShelter, StructuralSystemKind.ScrapFrame, 700, 1200, 700, 1200, 1, 100,
        new[] { R("entrance",RoomArchetype.Entrance,4,5,exterior:true), R("living",RoomArchetype.Living,10,14), R("storage",RoomArchetype.Storage,5,7,AccessClass.Service) },
        new[] { new AdjacencyRule("entrance","living",ConnectionRequirement.Required), new AdjacencyRule("living","storage",ConnectionRequirement.Required) });
}
