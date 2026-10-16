using Roentgen.Content;

namespace Roentgen.World.Structures;

public static class Phase21StructureGrammarCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, BuildingGrammar>> Grammars = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyCollection<BuildingGrammar> All => Grammars.Value.Values.ToArray();
    public static BuildingGrammar Get(string id) => Grammars.Value.TryGetValue(id, out BuildingGrammar? grammar) ? grammar : throw new KeyNotFoundException($"Unknown Phase 21 structure grammar '{id}'.");

    private static IReadOnlyDictionary<string, BuildingGrammar> Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        var result = new SortedDictionary<string, BuildingGrammar>(StringComparer.Ordinal);
        foreach (StructureGrammarContentDefinition definition in content.Pack.StructureGrammars)
        {
            BuildingGrammar grammar = Build(definition);
            grammar.Validate();
            result.Add(definition.Id, grammar);
        }
        if (result.Count != ContentBudgets.StructureGrammars) throw new InvalidOperationException($"Expected {ContentBudgets.StructureGrammars} runtime structure grammars; found {result.Count}.");
        return result;
    }

    private static BuildingGrammar Build(StructureGrammarContentDefinition source)
    {
        int seed = StableIndex(source.Id, 1000);
        int minWidth = 700 + seed % 700;
        int minDepth = 700 + seed / 3 % 700;
        int maxWidth = minWidth + 500 + seed % 900;
        int maxDepth = minDepth + 500 + seed / 5 % 900;
        int minimumFloors = Math.Clamp(source.MinimumFloors, 1, 4);
        int maximumFloors = Math.Clamp(source.MaximumFloors, minimumFloors, 4);
        StructuralSystemKind system = source.Group switch
        {
            "industrial-and-utility" or "transport-infrastructure" => StructuralSystemKind.IndustrialSteel,
            "medical-and-scientific" or "security-and-military" or "underground-and-bunker" => StructuralSystemKind.ReinforcedConcrete,
            "residential" or "agricultural-and-survival" => StructuralSystemKind.ScrapFrame,
            _ => StructuralSystemKind.MasonryBlock
        };
        RoomArchetype primary = source.Group switch
        {
            "residential" => RoomArchetype.Living,
            "commercial-and-civic" => RoomArchetype.Market,
            "industrial-and-utility" => RoomArchetype.Machinery,
            "medical-and-scientific" => RoomArchetype.Treatment,
            "security-and-military" => RoomArchetype.Guard,
            "agricultural-and-survival" => RoomArchetype.Workshop,
            "transport-infrastructure" => RoomArchetype.Warehouse,
            "underground-and-bunker" => RoomArchetype.Control,
            _ => RoomArchetype.Utility
        };
        var required = new List<RoomRequirement>
        {
            Room("entrance", RoomArchetype.Entrance, 5, 8, AccessClass.Public, exterior: true, critical: true),
            Room("primary", primary, 18 + seed % 12, 30 + seed % 22, primary is RoomArchetype.Machinery ? AccessClass.Hazardous : AccessClass.Staff, exterior: primary is RoomArchetype.Warehouse, critical: true),
            Room("service", RoomArchetype.Utility, 7, 12, AccessClass.Service, critical: source.QuestReservationTags.Contains("technical-route", StringComparer.Ordinal)),
            Room("storage", RoomArchetype.Storage, 7, 14, AccessClass.Service)
        };
        var optional = new List<RoomRequirement>();
        if (maximumFloors > 1) optional.Add(Room("upper", RoomArchetype.Staff, 8, 15, AccessClass.Staff, floor: 1));
        if (source.QuestReservationTags.Contains("hidden-route", StringComparer.Ordinal)) optional.Add(Room("secure", RoomArchetype.SecureStorage, 6, 10, AccessClass.Secure, critical: true));
        var adjacency = new List<AdjacencyRule>
        {
            new("entrance", "primary", ConnectionRequirement.Required),
            new("primary", "service", ConnectionRequirement.Required),
            new("primary", "storage", ConnectionRequirement.Required)
        };
        if (optional.Any(static value => value.Key == "upper")) adjacency.Add(new("primary", "upper", ConnectionRequirement.Required, ConnectionKind.Stair));
        if (optional.Any(static value => value.Key == "secure")) adjacency.Add(new("service", "secure", ConnectionRequirement.Required, ConnectionKind.Door, LockPolicy.LockedWithAlternative));
        return new BuildingGrammar(
            source.Id,
            21,
            BuildingPurpose.GenericShelter,
            new FootprintRules(minWidth, maxWidth, minDepth, maxDepth),
            new FloorCountRules(minimumFloors, maximumFloors, source.Group == "underground-and-bunker", maximumFloors > 1),
            Structural(system),
            required,
            optional,
            adjacency,
            new NavigationRequirements(150, 100, source.QuestReservationTags.Contains("alternate-route", StringComparer.Ordinal), true),
            new StructureBudget(32, 900, 72, 20),
            100 + seed % 700);
    }

    private static RoomRequirement Room(string key, RoomArchetype archetype, int minimum, int preferred, AccessClass access, bool exterior = false, bool critical = false, int floor = 0) =>
        new(key, archetype, access, access is AccessClass.Secure or AccessClass.Hazardous ? PrivacyClass.Restricted : PrivacyClass.Open, minimum, preferred, 200, floor, exterior, critical);

    private static StructuralSystemRules Structural(StructuralSystemKind kind) => kind switch
    {
        StructuralSystemKind.ScrapFrame => new(kind, 20, 300, 600),
        StructuralSystemKind.ReinforcedConcrete => new(kind, 30, 360, 900),
        StructuralSystemKind.IndustrialSteel => new(kind, 20, 420, 1200),
        _ => new(kind, 35, 320, 700)
    };

    private static int StableIndex(string value, int modulus) { uint hash = 2166136261; foreach (char character in value) hash = (hash ^ character) * 16777619; return (int)(hash % (uint)modulus); }
}
