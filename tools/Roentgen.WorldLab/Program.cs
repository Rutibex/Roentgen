using System.Globalization;
using System.Text;
using System.Text.Json;
using Roentgen.Contracts;
using Roentgen.World;

namespace Roentgen.WorldLab;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Execute(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"WorldLab error: {exception.Message}");
            return 1;
        }
    }

    private static int Execute(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        Dictionary<string, string> options = ParseOptions(args.Skip(1));
        CampaignSeed seed = ParseSeed(Get(options, "seed", "5931052874560947534"));
        return args[0] switch
        {
            "summary" => Summary(seed, options.GetValueOrDefault("output")),
            "export-map" => ExportMap(seed, Get(options, "output", "artifacts/worldlab/phase6-region-map.ppm"), Get(options, "layer", "regions")),
            "inspect-chunk" => InspectChunk(seed, ParseInt(Require(options, "x")), ParseInt(Require(options, "z")), options.GetValueOrDefault("output")),
            "compile-region" => CompileRegion(seed, Get(options, "output", "artifacts/worldlab/chunks")),
            "validate-seeds" => ValidateSeeds(ParseInt(Get(options, "count", "20")), ParseSeed(Get(options, "start", "1"))),
            _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
        };
    }

    private static int Summary(CampaignSeed seed, string? output)
    {
        RegionalTopology topology = Generate(seed);
        WorldGenerationProfile profile = topology.Profile;
        var payload = new
        {
            schemaVersion = 1,
            campaignSeed = seed.Value,
            topologyId = topology.Id.ToString(),
            contentChecksum = topology.ContentChecksum.ToString(),
            profile = new
            {
                schemaVersion = profile.SchemaVersion,
                worldWidthMeters = profile.WorldWidthMeters,
                worldDepthMeters = profile.WorldDepthMeters,
                chunkSizeMeters = profile.ChunkSizeMeters,
                nearChunkSamples = profile.NearChunkSamples,
                topologyCellMeters = profile.TopologyCellMeters,
                waterLevelCentimeters = profile.WaterLevelCentimeters,
                minimumElevationCentimeters = profile.MinimumElevationCentimeters,
                maximumElevationCentimeters = profile.MaximumElevationCentimeters,
                roadHalfWidthCentimeters = profile.RoadHalfWidthCentimeters,
                trailHalfWidthCentimeters = profile.TrailHalfWidthCentimeters,
                streamingRadiusChunks = profile.StreamingRadiusChunks,
                dormantRadiusChunks = profile.DormantRadiusChunks,
                maximumCpuResidentBytes = profile.MaximumCpuResidentBytes,
                maximumPendingGenerationRequests = profile.MaximumPendingGenerationRequests,
                workerCount = profile.WorkerCount
            },
            regionCounts = topology.RegionFamilies.GroupBy(static family => family).OrderBy(static group => group.Key).ToDictionary(static group => group.Key.ToString(), static group => group.Count()),
            roads = topology.Roads.Segments.Select(static road => new
            {
                id = road.Id.ToString(),
                name = road.Name,
                kind = road.Kind.ToString(),
                halfWidthCentimeters = road.HalfWidthCentimeters,
                polyline = road.Polyline.Select(static point => new[] { point.XCentimeters, point.ZCentimeters })
            }),
            reservations = topology.Reservations.Select(static reservation => new
            {
                id = reservation.Id.ToString(),
                name = reservation.Name,
                purpose = reservation.Purpose.ToString(),
                centerCentimeters = new[] { reservation.Center.XCentimeters, reservation.Center.ZCentimeters },
                halfWidthCentimeters = reservation.HalfWidthCentimeters,
                halfDepthCentimeters = reservation.HalfDepthCentimeters,
                targetElevationCentimeters = reservation.TargetElevationCentimeters,
                maximumSlopePermille = reservation.MaximumSlopePermille,
                requiresRoadAccess = reservation.RequiresRoadAccess
            })
        };
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(json);
        }
        else
        {
            WriteText(output, json);
            Console.WriteLine($"Wrote topology summary to {Path.GetFullPath(output)}.");
        }

        return 0;
    }

    private static int ExportMap(CampaignSeed seed, string output, string layer)
    {
        RegionalTopology topology = Generate(seed);
        int width = topology.Profile.TopologySamplesX;
        int depth = topology.Profile.TopologySamplesZ;
        var pixels = new Rgb[width * depth];
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[z * width + x] = layer switch
                {
                    "elevation" => ElevationColor(topology.Elevation[x, z], topology.Profile),
                    "contamination" => Gradient(topology.Contamination[x, z], new Rgb(28, 33, 29), new Rgb(151, 214, 72)),
                    "moisture" => Gradient(topology.Moisture[x, z], new Rgb(133, 103, 63), new Rgb(41, 92, 116)),
                    "industrial" => Gradient(topology.IndustrialDisturbance[x, z], new Rgb(91, 91, 83), new Rgb(219, 130, 69)),
                    _ => RegionColor(topology.RegionAtCell(x, z))
                };
            }
        }

        OverlayRoads(topology, pixels, width, depth);
        OverlayReservations(topology, pixels, width, depth);
        WritePpm(output, width, depth, pixels);
        Console.WriteLine($"Wrote {layer} map to {Path.GetFullPath(output)} ({width}x{depth}); checksum {topology.ContentChecksum}.");
        return 0;
    }

    private static int InspectChunk(CampaignSeed seed, int x, int z, string? output)
    {
        RegionalTopology topology = Generate(seed);
        TerrainChunkData chunk = new TerrainChunkCompiler(topology).Compile(new TerrainChunkAddress(x, z));
        var payload = new
        {
            address = chunk.Address.ToString(),
            id = chunk.Id.ToString(),
            checksum = chunk.ContentChecksum.ToString(),
            estimatedCpuBytes = chunk.EstimatedCpuBytes,
            lods = chunk.Lods.Select(static lod => new
            {
                lod.Lod,
                lod.SampleCount,
                lod.SpacingCentimeters,
                minimumHeight = lod.Heights.Min(),
                maximumHeight = lod.Heights.Max(),
                surfaces = lod.Surfaces.GroupBy(static surface => surface).ToDictionary(static group => group.Key.ToString(), static group => group.Count())
            })
        };
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        if (string.IsNullOrWhiteSpace(output)) Console.WriteLine(json); else WriteText(output, json);
        return 0;
    }

    private static int CompileRegion(CampaignSeed seed, string output)
    {
        RegionalTopology topology = Generate(seed);
        var compiler = new TerrainChunkCompiler(topology);
        Directory.CreateDirectory(output);
        var records = new List<object>();
        int minimumX = WorldUnits.FloorDivide(topology.Profile.MinimumWorldXCentimeters, topology.Profile.ChunkSizeCentimeters);
        int minimumZ = WorldUnits.FloorDivide(topology.Profile.MinimumWorldZCentimeters, topology.Profile.ChunkSizeCentimeters);
        for (int z = 0; z < topology.Profile.ChunkCountZ; z++)
        {
            for (int x = 0; x < topology.Profile.ChunkCountX; x++)
            {
                var address = new TerrainChunkAddress(minimumX + x, minimumZ + z);
                TerrainChunkData chunk = compiler.Compile(address);
                records.Add(new
                {
                    address = address.ToString(),
                    id = chunk.Id.ToString(),
                    checksum = chunk.ContentChecksum.ToString(),
                    chunk.EstimatedCpuBytes,
                    lodSamples = chunk.Lods.Select(static lod => lod.SampleCount).ToArray()
                });
            }
        }

        string manifest = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            campaignSeed = seed.Value,
            topologyChecksum = topology.ContentChecksum.ToString(),
            chunks = records
        }, new JsonSerializerOptions { WriteIndented = true });
        string path = Path.Combine(output, "terrain-chunks.manifest.json");
        File.WriteAllText(path, manifest);
        Console.WriteLine($"Compiled {records.Count} deterministic chunk records to {Path.GetFullPath(path)}.");
        return 0;
    }

    private static int ValidateSeeds(int count, CampaignSeed start)
    {
        if (count is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        int failures = 0;
        for (int index = 0; index < count; index++)
        {
            var seed = new CampaignSeed(unchecked(start.Value + (ulong)index));
            try
            {
                RegionalTopology topology = Generate(seed);
                ValidationReport report = new WorldTopologyValidator().Validate(topology);
                if (!report.IsValid)
                {
                    failures++;
                    Console.WriteLine($"FAIL {seed.Value}: {string.Join("; ", report.Issues.Select(static issue => issue.Message))}");
                    continue;
                }

                var compiler = new TerrainChunkCompiler(topology);
                TerrainChunkData center = compiler.Compile(new TerrainChunkAddress(0, 0));
                TerrainChunkData east = compiler.Compile(new TerrainChunkAddress(1, 0));
                if (!TerrainChunkValidator.ValidateSeam(center, east, alongX: true).IsValid)
                {
                    failures++;
                    Console.WriteLine($"FAIL {seed.Value}: east seam mismatch");
                }
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"FAIL {seed.Value}: {exception.Message}");
            }
        }

        Console.WriteLine($"Validated {count} seeds; {failures} failure(s).");
        return failures == 0 ? 0 : 2;
    }

    private static RegionalTopology Generate(CampaignSeed seed) => new RegionalTopologyGenerator(seed).Generate(WorldGenerationProfile.MicroRegion);

    private static void OverlayRoads(RegionalTopology topology, Rgb[] pixels, int width, int depth)
    {
        foreach (RoadSegment road in topology.Roads.Segments)
        {
            Rgb color = road.Kind == RoadKind.Trail ? new Rgb(255, 203, 84) : new Rgb(64, 244, 191);
            foreach (WorldPoint point in road.Polyline)
            {
                int x = Math.Clamp(WorldUnits.FloorDivide(point.XCentimeters - topology.Profile.MinimumWorldXCentimeters, topology.Profile.TopologyCellCentimeters), 0, width - 1);
                int z = Math.Clamp(WorldUnits.FloorDivide(point.ZCentimeters - topology.Profile.MinimumWorldZCentimeters, topology.Profile.TopologyCellCentimeters), 0, depth - 1);
                pixels[z * width + x] = color;
            }
        }
    }

    private static void OverlayReservations(RegionalTopology topology, Rgb[] pixels, int width, int depth)
    {
        foreach (LandmarkReservation reservation in topology.Reservations)
        {
            int x = Math.Clamp(WorldUnits.FloorDivide(reservation.Center.XCentimeters - topology.Profile.MinimumWorldXCentimeters, topology.Profile.TopologyCellCentimeters), 0, width - 1);
            int z = Math.Clamp(WorldUnits.FloorDivide(reservation.Center.ZCentimeters - topology.Profile.MinimumWorldZCentimeters, topology.Profile.TopologyCellCentimeters), 0, depth - 1);
            for (int dz = -2; dz <= 2; dz++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int px = x + dx;
                    int pz = z + dz;
                    if (px >= 0 && pz >= 0 && px < width && pz < depth && (dx == 0 || dz == 0))
                    {
                        pixels[pz * width + px] = new Rgb(255, 245, 188);
                    }
                }
            }
        }
    }

    private static Rgb RegionColor(RegionFamily family) => family switch
    {
        RegionFamily.GlassBasin => new Rgb(104, 126, 126),
        RegionFamily.OssuaryFields => new Rgb(177, 171, 137),
        RegionFamily.BlackwaterWorks => new Rgb(56, 91, 75),
        RegionFamily.BrokenMeridian => new Rgb(84, 82, 78),
        RegionFamily.RedQuarryCountry => new Rgb(148, 91, 60),
        RegionFamily.QuietRange => new Rgb(112, 119, 126),
        _ => new Rgb(255, 0, 255)
    };

    private static Rgb ElevationColor(int height, WorldGenerationProfile profile)
    {
        int value = (height - profile.MinimumElevationCentimeters) * 65_535 / (profile.MaximumElevationCentimeters - profile.MinimumElevationCentimeters);
        return Gradient(value, new Rgb(40, 65, 68), new Rgb(225, 211, 180));
    }

    private static Rgb Gradient(int value, Rgb low, Rgb high)
    {
        int clamped = Math.Clamp(value, 0, 65_535);
        byte Mix(byte a, byte b) => checked((byte)(a + ((b - a) * clamped + 32_767) / 65_535));
        return new Rgb(Mix(low.R, high.R), Mix(low.G, high.G), Mix(low.B, high.B));
    }

    private static void WritePpm(string path, int width, int height, IReadOnlyList<Rgb> pixels)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        byte[] header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        stream.Write(header);
        foreach (Rgb pixel in pixels)
        {
            stream.WriteByte(pixel.R);
            stream.WriteByte(pixel.G);
            stream.WriteByte(pixel.B);
        }
    }

    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, content);
    }

    private static CampaignSeed ParseSeed(string value)
    {
        string normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        NumberStyles style = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        return ulong.TryParse(normalized, style, CultureInfo.InvariantCulture, out ulong seed)
            ? new CampaignSeed(seed)
            : throw new ArgumentException($"Invalid campaign seed '{value}'.");
    }

    private static int ParseInt(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
        ? result
        : throw new ArgumentException($"Invalid integer '{value}'.");

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> values)
    {
        string[] args = values.ToArray();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{key}' requires a value.");
            }

            result[key[2..]] = args[++index];
        }

        return result;
    }

    private static string Require(IReadOnlyDictionary<string, string> options, string key) => options.TryGetValue(key, out string? value)
        ? value
        : throw new ArgumentException($"Missing --{key}.");

    private static string Get(IReadOnlyDictionary<string, string> options, string key, string fallback) => options.TryGetValue(key, out string? value) ? value : fallback;

    private static void PrintHelp()
    {
        Console.WriteLine("Roentgen WorldLab — deterministic regional topology and terrain diagnostics");
        Console.WriteLine("  summary [--seed <ulong|0xhex>] [--output <json>]");
        Console.WriteLine("  export-map [--seed <seed>] [--layer regions|elevation|moisture|contamination|industrial] [--output <ppm>]");
        Console.WriteLine("  inspect-chunk --x <chunk-x> --z <chunk-z> [--seed <seed>] [--output <json>]");
        Console.WriteLine("  compile-region [--seed <seed>] [--output <folder>]");
        Console.WriteLine("  validate-seeds [--count <1..10000>] [--start <seed>]");
    }

    private readonly record struct Rgb(byte R, byte G, byte B);
}
