using System.Globalization;
using System.Text;
using System.Text.Json;
using Roentgen.Contracts;
using Roentgen.Navigation;
using Roentgen.Physics;
using Roentgen.World;
using Roentgen.World.Structures;

namespace Roentgen.StructureLab;

public static class Program
{
    private const ulong DefaultSeed = 0x524F454E5447454EUL;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            Options options = Options.Parse(args.Skip(1));
            CampaignSeed seed = new(options.GetUInt64("seed", DefaultSeed));
            return command switch
            {
                "generate" => Generate(seed, options.Get("output", "artifacts/structurelab")),
                "settlement" => ExportSettlement(seed, options.Get("output", "artifacts/structurelab/settlement.svg")),
                "floorplan" => ExportFloorPlan(seed, options.Get("structure", "blackwater"), options.Get("output", "artifacts/structurelab/floorplan.svg")),
                "inspect" => Inspect(seed, options.Get("structure", "prospect clinic"), options.GetOptional("output")),
                "validate" => Validate(seed),
                "validate-seeds" => ValidateSeeds(seed, options.GetInt32("count", 100)),
                "stream-smoke" => StreamSmoke(seed, options.GetInt32("iterations", 64)),
                "collision-smoke" => CollisionSmoke(seed),
                _ => Unknown(command)
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int Generate(CampaignSeed seed, string output)
    {
        GeneratedStructureWorld world = GenerateWorld(seed);
        Directory.CreateDirectory(output);
        foreach (StructureBlueprint structure in world.Structures)
        {
            string safe = SafeName(structure.Name);
            File.WriteAllText(Path.Combine(output, $"{safe}.floorplan.svg"), FloorPlanSvg(structure));
            File.WriteAllText(Path.Combine(output, $"{safe}.json"), StructureJson(structure));
        }

        File.WriteAllText(Path.Combine(output, "settlement.svg"), SettlementSvg(world.Settlement));
        var manifest = new
        {
            schemaVersion = 1,
            phase = "Phase 7 — Structures and Interiors",
            campaignSeed = seed.Value,
            topologyChecksum = world.Topology.ContentChecksum.ToString(),
            settlementChecksum = world.Settlement.ContentChecksum.ToString(),
            structureWorldChecksum = world.ContentChecksum.ToString(),
            structures = world.Structures.Select(structure => new
            {
                id = structure.Id.ToString(),
                structure.Name,
                grammar = structure.Grammar.GrammarId,
                checksum = structure.ContentChecksum.ToString(),
                structure.FloorCount,
                rooms = structure.Rooms.Count,
                doors = structure.Doors.Count,
                primitives = structure.Primitives.Count,
                reservations = structure.Reservations.Count,
                damageEvents = structure.DamageHistory.Events.Count,
                repairHistory = structure.Provenance.RepairHistory
            })
        };
        string path = Path.Combine(output, "structure-world.manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine($"Generated {world.Structures.Count} structures to {Path.GetFullPath(output)}.");
        Console.WriteLine($"World checksum: {world.ContentChecksum}");
        return 0;
    }

    private static int ExportSettlement(CampaignSeed seed, string output)
    {
        GeneratedStructureWorld world = GenerateWorld(seed);
        WriteText(output, SettlementSvg(world.Settlement));
        Console.WriteLine($"Wrote settlement plan to {Path.GetFullPath(output)}.");
        return 0;
    }

    private static int ExportFloorPlan(CampaignSeed seed, string query, string output)
    {
        StructureBlueprint structure = Find(GenerateWorld(seed), query);
        WriteText(output, FloorPlanSvg(structure));
        Console.WriteLine($"Wrote {structure.Name} floor plan to {Path.GetFullPath(output)}.");
        return 0;
    }

    private static int Inspect(CampaignSeed seed, string query, string? output)
    {
        StructureBlueprint structure = Find(GenerateWorld(seed), query);
        string json = StructureJson(structure);
        if (string.IsNullOrWhiteSpace(output)) Console.WriteLine(json); else WriteText(output, json);
        return 0;
    }

    private static int Validate(CampaignSeed seed)
    {
        GeneratedStructureWorld world = GenerateWorld(seed);
        int failures = 0;
        var emitter = new StructureNavigationEmitter();
        var navValidator = new AutomatedStructureTraversalValidator();
        foreach (StructureBlueprint structure in world.Structures)
        {
            StructureValidationReport structureReport = new StructureValidator().Validate(structure);
            InteriorNavigationData navigation = emitter.Emit(structure);
            TraversalValidationReport traversal = navValidator.Validate(structure, navigation);
            string status = !structureReport.HasCriticalIssues && traversal.IsValid ? "PASS" : "FAIL";
            if (status == "FAIL") failures++;
            Console.WriteLine($"{status} {structure.Name,-24} rooms={structure.Rooms.Count,2} doors={structure.Doors.Count,2} geometry={structure.Primitives.Count,3} damage={structure.DamageHistory.Events.Count,2} checksum={structure.ContentChecksum}");
            foreach (StructureValidationIssue issue in structureReport.Issues.Where(static issue => issue.Severity >= StructureValidationSeverity.Error)) Console.WriteLine($"  {issue.Code}: {issue.Message}");
            foreach (TraversalValidationIssue issue in traversal.Issues) Console.WriteLine($"  {issue.Code}: {issue.Message}");
        }
        Console.WriteLine($"Validated {world.Structures.Count} structures; {failures} failure(s).");
        return failures == 0 ? 0 : 2;
    }

    private static int ValidateSeeds(CampaignSeed start, int count)
    {
        if (count is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(count));
        int target = checked(count * 10);
        StructureBatchValidationSummary summary = new StructureBatchValidator().Validate(start, target);
        Console.WriteLine(JsonSerializer.Serialize(summary, JsonOptions));
        return summary.Passed ? 0 : 2;
    }

    private static int StreamSmoke(CampaignSeed seed, int iterations)
    {
        if (iterations is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(iterations));
        GeneratedStructureWorld world = GenerateWorld(seed);
        return StreamSmokeAsync(world, iterations).GetAwaiter().GetResult();
    }

    private static async Task<int> StreamSmokeAsync(GeneratedStructureWorld world, int iterations)
    {
        await using var streaming = new StructureStreamingCoordinator(world.Structures, new StructureStreamingOptions(2, 32, 32, 64L * 1024L * 1024L));
        int completed = 0;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            StructureBlueprint structure = world.Structures[iteration % world.Structures.Count];
            StructureCellLayer layer = (StructureCellLayer)(iteration % 3);
            streaming.TryRequest(structure.Id, layer, iteration % 5, CancellationToken.None, out _);
            await Task.Delay(1).ConfigureAwait(false);
            while (streaming.TryReadResult(out StructureStreamingResult? result)) if (result is not null && result.Succeeded) completed++;
            if (iteration % 7 == 0) streaming.Unload(structure.Id);
        }
        await Task.Delay(20).ConfigureAwait(false);
        while (streaming.TryReadResult(out StructureStreamingResult? result)) if (result is not null && result.Succeeded) completed++;
        StructureStreamingTelemetry telemetry = streaming.CaptureTelemetry();
        Console.WriteLine(JsonSerializer.Serialize(new { completed, telemetry }, JsonOptions));
        return telemetry.ResidentCpuBytes <= 64L * 1024L * 1024L ? 0 : 2;
    }

    private static int CollisionSmoke(CampaignSeed seed)
    {
        GeneratedStructureWorld world = GenerateWorld(seed);
        var registry = new StructureCollisionRegistry();
        var compiler = new StructureCollisionCompiler();
        foreach (StructureBlueprint structure in world.Structures) registry.Register(compiler.Compile(structure));
        StructureBlueprint clinic = Find(world, "prospect clinic");
        StructurePoint origin = clinic.WorldOrigin;
        var start = new StructureCollisionPoint(origin.XCentimeters + clinic.Footprint.CenterXCentimeters, origin.YCentimeters + 150, origin.ZCentimeters + clinic.Footprint.MinimumZCentimeters - 500);
        bool hit = registry.Raycast(start, new StructureCollisionVector(0, 0, 1), 10_000, out StructureRaycastHit result);
        Console.WriteLine(JsonSerializer.Serialize(new { registered = registry.Count, registry.EstimatedBytes, hit, result }, JsonOptions));
        return registry.Count == world.Structures.Count ? 0 : 2;
    }

    private static GeneratedStructureWorld GenerateWorld(CampaignSeed seed)
    {
        RegionalTopology topology = new RegionalTopologyGenerator(seed).Generate(WorldGenerationProfile.MicroRegion);
        ValidationReport report = new WorldTopologyValidator().Validate(topology);
        if (!report.IsValid) throw new InvalidDataException(string.Join("; ", report.Issues.Select(static issue => issue.Message)));
        return new MicroRegionStructureCompiler(topology).Compile();
    }

    private static StructureBlueprint Find(GeneratedStructureWorld world, string query)
    {
        StructureBlueprint? exact = world.Structures.FirstOrDefault(structure => structure.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || structure.Grammar.GrammarId.Contains(query, StringComparison.OrdinalIgnoreCase));
        return exact ?? throw new KeyNotFoundException($"No structure matches '{query}'. Available: {string.Join(", ", world.Structures.Select(static structure => structure.Name))}");
    }

    private static string StructureJson(StructureBlueprint structure)
    {
        StructureValidationReport validation = new StructureValidator().Validate(structure);
        InteriorNavigationData navigation = new StructureNavigationEmitter().Emit(structure);
        TraversalValidationReport traversal = new AutomatedStructureTraversalValidator().Validate(structure, navigation);
        var payload = new
        {
            id = structure.Id.ToString(),
            structure.Name,
            grammar = structure.Grammar.GrammarId,
            checksum = structure.ContentChecksum.ToString(),
            origin = structure.WorldOrigin,
            structure.Footprint,
            structure.FloorCount,
            rooms = structure.Rooms,
            connections = structure.Connections,
            doors = structure.Doors,
            verticalLinks = structure.VerticalLinks,
            primitives = structure.Primitives,
            reservations = structure.Reservations,
            damageHistory = structure.DamageHistory,
            streamingCells = structure.StreamingCells,
            navigationChecksum = navigation.ContentChecksum.ToString(),
            validation = new { validation.IsValid, validation.HasCriticalIssues, validation.Metrics, validation.Issues },
            traversal,
            provenance = structure.Provenance
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string SettlementSvg(SettlementPlan plan)
    {
        int width = plan.Site.HalfWidthCentimeters * 2;
        int depth = plan.Site.HalfDepthCentimeters * 2;
        var builder = SvgHeader(width, depth, $"Settlement: {plan.Site.Name}");
        foreach (DistrictPlan district in plan.Districts) Rect(builder, district.Bounds, DistrictColor(district.Kind), 0.18, district.Kind.ToString());
        foreach (StreetSegment street in plan.Streets)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<line x1='{street.Start.XCentimeters + width / 2}' y1='{street.Start.ZCentimeters + depth / 2}' x2='{street.End.XCentimeters + width / 2}' y2='{street.End.ZCentimeters + depth / 2}' stroke='{(street.IsPrimary ? "#73e6c0" : "#b6a476")}' stroke-width='{street.HalfWidthCentimeters * 2}' opacity='0.7'/>");
        }
        foreach (SettlementParcel parcel in plan.Parcels) Rect(builder, parcel.LocalBounds, parcel.MayRemainEmpty ? "#555" : "#d9b875", 0.72, parcel.Name);
        return SvgEnd(builder);
    }

    private static string FloorPlanSvg(StructureBlueprint structure)
    {
        int floorHeight = structure.Grammar.StructuralSystem.FloorHeightCentimeters;
        int width = structure.Footprint.WidthCentimeters;
        int depth = structure.Footprint.DepthCentimeters;
        int canvasWidth = checked(width * structure.FloorCount + 400 * Math.Max(0, structure.FloorCount - 1));
        var builder = SvgHeader(canvasWidth, depth, $"{structure.Name} — {structure.Grammar.GrammarId}");
        for (int floor = 0; floor < structure.FloorCount; floor++)
        {
            int offsetX = floor * (width + 400) - structure.Footprint.MinimumXCentimeters;
            foreach (RoomNode room in structure.Rooms.Where(room => room.Floor == floor))
            {
                string color = room.QuestCritical ? "#d9b552" : room.Archetype is RoomArchetype.Corridor or RoomArchetype.Stairwell ? "#6a8c99" : "#7d8375";
                StructureRect translated = room.Bounds.Translate(offsetX, -structure.Footprint.MinimumZCentimeters);
                Rect(builder, translated, color, 0.76, room.Key);
            }
            foreach (DoorDefinition door in structure.Doors.Where(door => door.Center.YCentimeters / floorHeight == floor))
            {
                int x = door.Center.XCentimeters + offsetX;
                int z = door.Center.ZCentimeters - structure.Footprint.MinimumZCentimeters;
                builder.Append(CultureInfo.InvariantCulture, $"<circle cx='{x}' cy='{z}' r='45' fill='{(door.Exterior ? "#70edc0" : "#f2dc8c")}' stroke='#111' stroke-width='12'/>");
            }
            builder.Append(CultureInfo.InvariantCulture, $"<text x='{floor * (width + 400) + 40}' y='100' font-size='90' fill='#fff'>Floor {floor}</text>");
        }
        return SvgEnd(builder);
    }

    private static StringBuilder SvgHeader(int width, int height, string title)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' width='1400' height='{Math.Max(600, 1400 * height / Math.Max(1, width))}'><rect width='100%' height='100%' fill='#202522'/><title>{Escape(title)}</title>");
        return builder;
    }

    private static void Rect(StringBuilder builder, StructureRect rect, string color, double opacity, string label)
    {
        builder.Append(CultureInfo.InvariantCulture, $"<rect x='{rect.MinimumXCentimeters}' y='{rect.MinimumZCentimeters}' width='{rect.WidthCentimeters}' height='{rect.DepthCentimeters}' fill='{color}' fill-opacity='{opacity:F2}' stroke='#111' stroke-width='20'/>");
        builder.Append(CultureInfo.InvariantCulture, $"<text x='{rect.MinimumXCentimeters + 50}' y='{rect.MinimumZCentimeters + 120}' font-size='85' fill='#fff'>{Escape(label)}</text>");
    }

    private static string SvgEnd(StringBuilder builder) { builder.Append("</svg>"); return builder.ToString(); }
    private static string DistrictColor(DistrictKind kind) => kind switch { DistrictKind.Medical => "#72aeb5", DistrictKind.Market => "#b7a46a", DistrictKind.Residential => "#7d9278", DistrictKind.Workshop => "#a87861", DistrictKind.Defensive => "#8a7370", _ => "#777" };
    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;
    private static string SafeName(string value) => string.Concat(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
    private static void WriteText(string output, string content) { string? directory = Path.GetDirectoryName(output); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory); File.WriteAllText(output, content); }
    private static bool IsHelp(string value) => value is "help" or "--help" or "-h" or "/?";
    private static int Unknown(string command) { Console.Error.WriteLine($"Unknown command '{command}'."); PrintHelp(); return 1; }

    private static void PrintHelp()
    {
        Console.WriteLine("Roentgen.StructureLab — Phase 7 structure compiler and diagnostics");
        Console.WriteLine("  generate        --seed N --output PATH");
        Console.WriteLine("  settlement      --seed N --output settlement.svg");
        Console.WriteLine("  floorplan       --seed N --structure NAME --output floorplan.svg");
        Console.WriteLine("  inspect         --seed N --structure NAME [--output report.json]");
        Console.WriteLine("  validate        --seed N");
        Console.WriteLine("  validate-seeds  --seed N --count N   (generates count × 10 structures)");
        Console.WriteLine("  stream-smoke    --seed N --iterations N");
        Console.WriteLine("  collision-smoke --seed N");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed class Options
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        public static Options Parse(IEnumerable<string> args)
        {
            var options = new Options(); string? key = null;
            foreach (string arg in args)
            {
                if (arg.StartsWith("--", StringComparison.Ordinal)) { key = arg[2..]; options._values[key] = "true"; }
                else if (key is not null) { options._values[key] = arg; key = null; }
                else throw new ArgumentException($"Unexpected argument '{arg}'.");
            }
            return options;
        }
        public string Get(string key, string fallback) => _values.TryGetValue(key, out string? value) ? value : fallback;
        public string? GetOptional(string key) => _values.TryGetValue(key, out string? value) ? value : null;
        public int GetInt32(string key, int fallback) => _values.TryGetValue(key, out string? value) ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;
        public ulong GetUInt64(string key, ulong fallback) => _values.TryGetValue(key, out string? value) ? ulong.Parse(value, CultureInfo.InvariantCulture) : fallback;
    }
}
