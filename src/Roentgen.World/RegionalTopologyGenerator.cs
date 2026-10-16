using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Generation;

namespace Roentgen.World;

public sealed class RegionalTopologyGenerator : IVersionedGenerator<WorldGenerationProfile, RegionalTopology>
{
    public const string CurrentGeneratorId = "roentgen.world.regional-topology";
    public const int CurrentSchemaVersion = 1;

    private readonly CampaignSeed _campaignSeed;

    public RegionalTopologyGenerator(CampaignSeed campaignSeed)
    {
        _campaignSeed = campaignSeed;
    }

    public string GeneratorId => CurrentGeneratorId;
    public int SchemaVersion => CurrentSchemaVersion;

    public RegionalTopology Generate(WorldGenerationProfile request, DomainSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (!string.Equals(seed.Domain, "world.topology", StringComparison.Ordinal))
        {
            throw new ArgumentException("Regional topology must use the canonical world.topology domain.", nameof(seed));
        }

        StableId campaignId = StableIdFactory.CreateCampaignRoot(_campaignSeed);
        StableId topologyId = StableIdFactory.CreateChild(campaignId, "world-topology", request.SchemaVersion);
        var provenance = new GenerationProvenanceBuilder(topologyId, campaignId, _campaignSeed, seed, GeneratorId, SchemaVersion);
        int width = request.TopologySamplesX;
        int depth = request.TopologySamplesZ;
        int count = checked(width * depth);
        int[] heights = new int[count];
        int[] moisture = new int[count];
        int[] contamination = new int[count];
        int[] industrial = new int[count];
        int[] suitability = new int[count];
        var regions = new RegionFamily[count];
        DomainSeed heightSeed = GenerationDomain.Derive(seed, "height");
        DomainSeed moistureSeed = GenerationDomain.Derive(seed, "moisture");
        DomainSeed contaminationSeed = GenerationDomain.Derive(seed, "contamination");
        DomainSeed industrialSeed = GenerationDomain.Derive(seed, "industrial");

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                int worldX = request.MinimumWorldXCentimeters + x * request.TopologyCellCentimeters;
                int worldZ = request.MinimumWorldZCentimeters + z * request.TopologyCellCentimeters;
                int broad = DeterministicTerrainNoise.FractalSigned(heightSeed, worldX, worldZ, 48_000, 4);
                int detail = DeterministicTerrainNoise.FractalSigned(heightSeed, worldX + 93_517, worldZ - 31_337, 12_000, 3);
                long radialX = (long)worldX * 65_535 / Math.Max(1, request.MaximumWorldXCentimeters);
                long radialZ = (long)worldZ * 65_535 / Math.Max(1, request.MaximumWorldZCentimeters);
                int radial = checked((int)Math.Min(65_535L, (radialX * radialX + radialZ * radialZ) / 65_535L));
                int elevation = broad * 5_200 / 32_768 + detail * 1_350 / 32_768 + radial * 4_500 / 65_535 - 800;

                int corridorDistance = Math.Abs(worldZ - worldX / 3 + 8_000);
                if (corridorDistance < 18_000)
                {
                    elevation = elevation * 3 / 4 - 250;
                }

                if (worldX > request.MaximumWorldXCentimeters / 5 && worldZ > request.MinimumWorldZCentimeters / 3)
                {
                    elevation -= 2_600;
                }

                heights[index] = Math.Clamp(elevation, request.MinimumElevationCentimeters, request.MaximumElevationCentimeters);
                int moistureValue = DeterministicTerrainNoise.FractalUnsigned(moistureSeed, worldX, worldZ, 36_000, 4);
                if (worldX > request.MaximumWorldXCentimeters / 5) moistureValue = Math.Min(65_535, moistureValue + 18_000);
                moisture[index] = moistureValue;
                int contaminationValue = DeterministicTerrainNoise.FractalUnsigned(contaminationSeed, worldX - 72_000, worldZ + 44_000, 28_000, 4);
                if (worldZ > request.MaximumWorldZCentimeters / 4) contaminationValue = Math.Min(65_535, contaminationValue + 9_000);
                contamination[index] = contaminationValue;
                int industrialValue = DeterministicTerrainNoise.FractalUnsigned(industrialSeed, worldX, worldZ, 42_000, 3);
                if (corridorDistance < 24_000) industrialValue = Math.Min(65_535, industrialValue + 24_000);
                industrial[index] = industrialValue;
            }
        }

        var elevationField = new TerrainScalarField("elevation-centimeters", width, depth, heights);
        var moistureField = new TerrainScalarField("moisture", width, depth, moisture);
        var contaminationField = new TerrainScalarField("contamination", width, depth, contamination);
        var industrialField = new TerrainScalarField("industrial-disturbance", width, depth, industrial);
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                int slope = Math.Max(
                    Math.Abs(elevationField.SampleClamped(x + 1, z) - elevationField.SampleClamped(x - 1, z)),
                    Math.Abs(elevationField.SampleClamped(x, z + 1) - elevationField.SampleClamped(x, z - 1)));
                int score = 65_535 - Math.Min(50_000, slope * 10) - contamination[index] / 3;
                if (heights[index] <= request.WaterLevelCentimeters) score -= 30_000;
                suitability[index] = Math.Clamp(score, 0, 65_535);
                int worldX = request.MinimumWorldXCentimeters + x * request.TopologyCellCentimeters;
                int worldZ = request.MinimumWorldZCentimeters + z * request.TopologyCellCentimeters;
                int corridorDistance = Math.Abs(worldZ - worldX / 3 + 8_000);
                regions[index] = ClassifyRegion(request, worldX, worldZ, heights[index], moisture[index], industrial[index], corridorDistance);
            }
        }

        var suitabilityField = new TerrainScalarField("settlement-suitability", width, depth, suitability);
        (int X, int Z) settlementCell = FindBestCell(request, suitabilityField, width / 4, depth / 2, width / 8);
        (int X, int Z) ruinCell = FindBestCell(request, suitabilityField, width * 3 / 4, depth * 2 / 3, width / 9);
        (int X, int Z) relayCell = FindBestCell(request, suitabilityField, width / 2, depth / 5, width / 10);
        RoadNode settlementNode = CreateNode(topologyId, "settlement", "Prospect Clinic", request, settlementCell, elevationField);
        RoadNode ruinNode = CreateNode(topologyId, "ruin", "Blackwater Intake", request, ruinCell, elevationField);
        RoadNode relayNode = CreateNode(topologyId, "relay", "Meridian Relay", request, relayCell, elevationField);
        RoadNode[] nodes = [settlementNode, ruinNode, relayNode];
        RoadSegment[] roads =
        [
            CreateRoad(topologyId, "clinic-intake", RoadKind.MajorRoad, settlementNode, ruinNode, request.RoadHalfWidthCentimeters,
                RoadGraphGenerator.FindPath(request, elevationField, moistureField, contaminationField, industrialField, settlementCell, ruinCell, RoadKind.MajorRoad)),
            CreateRoad(topologyId, "clinic-relay", RoadKind.MinorRoad, settlementNode, relayNode, request.RoadHalfWidthCentimeters * 3 / 4,
                RoadGraphGenerator.FindPath(request, elevationField, moistureField, contaminationField, industrialField, settlementCell, relayCell, RoadKind.MinorRoad)),
            CreateRoad(topologyId, "relay-intake-trail", RoadKind.Trail, relayNode, ruinNode, request.TrailHalfWidthCentimeters,
                RoadGraphGenerator.FindPath(request, elevationField, moistureField, contaminationField, industrialField, relayCell, ruinCell, RoadKind.Trail))
        ];
        var roadGraph = new RoadGraph(nodes, roads);
        LandmarkReservation[] reservations =
        [
            CreateReservation(topologyId, "prospect-clinic", "Prospect Clinic settlement parcel", LandmarkPurpose.Settlement, settlementNode, 14_000, 12_000, 90, true),
            CreateReservation(topologyId, "blackwater-intake", "Blackwater Intake ruin parcel", LandmarkPurpose.Ruin, ruinNode, 11_000, 9_000, 160, true),
            CreateReservation(topologyId, "meridian-relay", "Meridian Relay infrastructure parcel", LandmarkPurpose.Infrastructure, relayNode, 6_000, 6_000, 120, true)
        ];
        provenance.RecordDecision("terrain.identity", "micro-region", "Glass Basin, Broken Meridian and Blackwater Works prototype identities");
        provenance.RecordDecision("roads", "a-star", $"{roads.Length} regional corridors");
        provenance.RecordDecision("reservations", "reserved-before-decoration", $"{reservations.Length} parcels");
        provenance.RecordValidation("Regional scalar fields generated with shared global coordinates.");
        provenance.RecordValidation("Required settlement, ruin and relay anchors are connected.");
        return new RegionalTopology(
            topologyId,
            _campaignSeed,
            request,
            elevationField,
            moistureField,
            contaminationField,
            industrialField,
            suitabilityField,
            regions,
            roadGraph,
            reservations,
            provenance.Build());
    }

    public RegionalTopology Generate(WorldGenerationProfile request)
    {
        DomainSeed seed = GenerationDomain.Derive(_campaignSeed, "world.topology");
        return Generate(request, seed);
    }

    private static RegionFamily ClassifyRegion(WorldGenerationProfile profile, int worldX, int worldZ, int height, int moisture, int industrial, int corridorDistance)
    {
        if (corridorDistance < 24_000 || industrial > 53_000)
        {
            return RegionFamily.BrokenMeridian;
        }

        // The eastern lowland is a reserved Blackwater identity. Elevation and moisture
        // generation already reinforce it, but classification is spatially guaranteed so
        // every Phase 6 seed exposes the prototype biome.
        if (worldX > profile.MaximumWorldXCentimeters / 5 && worldZ > profile.MinimumWorldZCentimeters / 3)
        {
            return RegionFamily.BlackwaterWorks;
        }

        if (height <= profile.WaterLevelCentimeters + 700 && moisture > 31_000)
        {
            return RegionFamily.BlackwaterWorks;
        }

        if (height > 8_500)
        {
            return RegionFamily.QuietRange;
        }

        if (worldX < -profile.WorldWidthMeters * 18)
        {
            return RegionFamily.GlassBasin;
        }

        if (worldZ < -profile.WorldDepthMeters * 18)
        {
            return RegionFamily.RedQuarryCountry;
        }

        return RegionFamily.OssuaryFields;
    }

    private static (int X, int Z) FindBestCell(WorldGenerationProfile profile, TerrainScalarField suitability, int centerX, int centerZ, int radius)
    {
        int bestX = Math.Clamp(centerX, 1, profile.TopologySamplesX - 2);
        int bestZ = Math.Clamp(centerZ, 1, profile.TopologySamplesZ - 2);
        int bestScore = int.MinValue;
        for (int z = Math.Max(1, centerZ - radius); z <= Math.Min(profile.TopologySamplesZ - 2, centerZ + radius); z++)
        {
            for (int x = Math.Max(1, centerX - radius); x <= Math.Min(profile.TopologySamplesX - 2, centerX + radius); x++)
            {
                int distancePenalty = (Math.Abs(centerX - x) + Math.Abs(centerZ - z)) * 160;
                int score = suitability[x, z] - distancePenalty;
                if (score > bestScore || score == bestScore && (z < bestZ || z == bestZ && x < bestX))
                {
                    bestScore = score;
                    bestX = x;
                    bestZ = z;
                }
            }
        }

        return (bestX, bestZ);
    }

    private static RoadNode CreateNode(StableId topologyId, string key, string name, WorldGenerationProfile profile, (int X, int Z) cell, TerrainScalarField elevation)
    {
        WorldPoint point = new(
            profile.MinimumWorldXCentimeters + cell.X * profile.TopologyCellCentimeters,
            profile.MinimumWorldZCentimeters + cell.Z * profile.TopologyCellCentimeters);
        return new RoadNode(StableIdFactory.CreateChild(topologyId, "road-node", key), name, point, elevation[cell.X, cell.Z]);
    }

    private static RoadSegment CreateRoad(StableId topologyId, string key, RoadKind kind, RoadNode start, RoadNode end, int width, IReadOnlyList<WorldPoint> points) =>
        new(StableIdFactory.CreateChild(topologyId, "road-segment", key), key, kind, start.Id, end.Id, width, points);

    private static LandmarkReservation CreateReservation(StableId topologyId, string key, string name, LandmarkPurpose purpose, RoadNode node, int halfWidth, int halfDepth, int slope, bool roadAccess) =>
        new(StableIdFactory.CreateChild(topologyId, "landmark-reservation", key), name, purpose, node.Position, halfWidth, halfDepth, node.ElevationCentimeters, slope, roadAccess);
}
