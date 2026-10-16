using Roentgen.Contracts;
using Roentgen.Generation;

namespace Roentgen.World;

public sealed class TerrainHeightEvaluator
{
    private readonly RegionalTopology _topology;
    private readonly DomainSeed _detailSeed;

    public TerrainHeightEvaluator(RegionalTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        _topology = topology;
        _detailSeed = GenerationDomain.Derive(topology.CampaignSeed, "terrain.detail");
    }

    public int SampleHeightCentimeters(int worldXCentimeters, int worldZCentimeters)
    {
        WorldGenerationProfile profile = _topology.Profile;
        int baseHeight = _topology.Elevation.SampleBilinear(worldXCentimeters, worldZCentimeters, profile);
        int detail = DeterministicTerrainNoise.FractalSigned(_detailSeed, worldXCentimeters, worldZCentimeters, 4_800, 4) * 145 / 32_768;
        int height = Math.Clamp(baseHeight + detail, profile.MinimumElevationCentimeters, profile.MaximumElevationCentimeters);
        height = ApplyRoads(worldXCentimeters, worldZCentimeters, height);
        height = ApplyReservations(worldXCentimeters, worldZCentimeters, height);
        return Math.Clamp(height, profile.MinimumElevationCentimeters, profile.MaximumElevationCentimeters);
    }

    public TerrainSurfaceKind SampleSurface(int worldXCentimeters, int worldZCentimeters, int heightCentimeters, int slopePermille)
    {
        if (TryGetRoad(worldXCentimeters, worldZCentimeters, out RoadSegment road, out _))
        {
            return road.Kind == RoadKind.Trail ? TerrainSurfaceKind.Trail : TerrainSurfaceKind.Road;
        }

        int moisture = _topology.Moisture.SampleBilinear(worldXCentimeters, worldZCentimeters, _topology.Profile);
        int contamination = _topology.Contamination.SampleBilinear(worldXCentimeters, worldZCentimeters, _topology.Profile);
        if (heightCentimeters <= _topology.Profile.WaterLevelCentimeters && moisture > 28_000)
        {
            return contamination > 40_000 ? TerrainSurfaceKind.ContaminatedSediment : TerrainSurfaceKind.ShallowWater;
        }

        if (slopePermille > 520)
        {
            return TerrainSurfaceKind.ExposedRock;
        }

        return _topology.RegionAtWorld(worldXCentimeters, worldZCentimeters) switch
        {
            RegionFamily.GlassBasin => TerrainSurfaceKind.FusedGlass,
            RegionFamily.BrokenMeridian => TerrainSurfaceKind.BrokenAsphalt,
            RegionFamily.BlackwaterWorks => contamination > 39_000 ? TerrainSurfaceKind.ContaminatedSediment : TerrainSurfaceKind.IndustrialMud,
            RegionFamily.OssuaryFields => TerrainSurfaceKind.PaleScrub,
            RegionFamily.RedQuarryCountry => TerrainSurfaceKind.ExposedRock,
            RegionFamily.QuietRange => TerrainSurfaceKind.ExposedRock,
            _ => TerrainSurfaceKind.DryEarth
        };
    }

    private int ApplyRoads(int x, int z, int sourceHeight)
    {
        if (!TryGetRoad(x, z, out RoadSegment road, out RoadProjection projection))
        {
            return sourceHeight;
        }

        int startHeight = _topology.Elevation.SampleBilinear(projection.Start.XCentimeters, projection.Start.ZCentimeters, _topology.Profile);
        int endHeight = _topology.Elevation.SampleBilinear(projection.End.XCentimeters, projection.End.ZCentimeters, _topology.Profile);
        int targetHeight = checked((int)(startHeight + ((long)(endHeight - startHeight) * projection.Numerator + projection.Denominator / 2) / projection.Denominator));
        int fullWidth = road.HalfWidthCentimeters;
        long fullWidthSquared = (long)fullWidth * fullWidth;
        if (projection.DistanceSquared <= fullWidthSquared)
        {
            return targetHeight;
        }

        int featherWidth = fullWidth + 350;
        long featherSquared = (long)featherWidth * featherWidth;
        if (projection.DistanceSquared <= featherSquared)
        {
            return BlendHeight(sourceHeight, targetHeight, featherWidth - (int)Math.Sqrt(projection.DistanceSquared), featherWidth - fullWidth);
        }

        return sourceHeight;
    }

    private int ApplyReservations(int x, int z, int sourceHeight)
    {
        foreach (LandmarkReservation reservation in _topology.Reservations)
        {
            int dx = Math.Abs(x - reservation.Center.XCentimeters);
            int dz = Math.Abs(z - reservation.Center.ZCentimeters);
            if (dx <= reservation.HalfWidthCentimeters && dz <= reservation.HalfDepthCentimeters)
            {
                return reservation.TargetElevationCentimeters;
            }

            const int feather = 500;
            if (dx <= reservation.HalfWidthCentimeters + feather && dz <= reservation.HalfDepthCentimeters + feather)
            {
                int outside = Math.Max(dx - reservation.HalfWidthCentimeters, dz - reservation.HalfDepthCentimeters);
                return BlendHeight(sourceHeight, reservation.TargetElevationCentimeters, feather - outside, feather);
            }
        }

        return sourceHeight;
    }

    private static int BlendHeight(int source, int target, int numerator, int denominator)
    {
        long t = Math.Clamp(numerator * 1000L / denominator, 0, 1000);
        long weight = t * t * (3000 - 2 * t) / 1000000;
        return checked(source + (int)((target - (long)source) * weight / 1000));
    }

    private bool TryGetRoad(int x, int z, out RoadSegment bestRoad, out RoadProjection bestProjection)
    {
        bestRoad = null!;
        bestProjection = default;
        long bestDistance = long.MaxValue;
        foreach (RoadSegment road in _topology.Roads.Segments)
        {
            int searchWidth = road.HalfWidthCentimeters + 350;
            long maximumDistance = (long)searchWidth * searchWidth;
            for (int index = 0; index < road.Polyline.Count - 1; index++)
            {
                WorldPoint start = road.Polyline[index];
                WorldPoint end = road.Polyline[index + 1];
                RoadProjection projection = Project(x, z, start, end);
                if (projection.DistanceSquared <= maximumDistance && projection.DistanceSquared < bestDistance)
                {
                    bestDistance = projection.DistanceSquared;
                    bestRoad = road;
                    bestProjection = projection;
                }
            }
        }

        return bestRoad is not null;
    }

    private static RoadProjection Project(int x, int z, WorldPoint start, WorldPoint end)
    {
        long dx = (long)end.XCentimeters - start.XCentimeters;
        long dz = (long)end.ZCentimeters - start.ZCentimeters;
        long px = (long)x - start.XCentimeters;
        long pz = (long)z - start.ZCentimeters;
        long lengthSquared = checked(dx * dx + dz * dz);
        if (lengthSquared == 0)
        {
            long distance = checked(px * px + pz * pz);
            return new RoadProjection(start, end, 0, 1, distance);
        }

        long dot = checked(px * dx + pz * dz);
        if (dot <= 0)
        {
            return new RoadProjection(start, end, 0, lengthSquared, checked(px * px + pz * pz));
        }

        if (dot >= lengthSquared)
        {
            long ex = (long)x - end.XCentimeters;
            long ez = (long)z - end.ZCentimeters;
            return new RoadProjection(start, end, lengthSquared, lengthSquared, checked(ex * ex + ez * ez));
        }

        long cross = checked(px * dz - pz * dx);
        Int128 squaredCross = (Int128)cross * cross;
        long distanceSquared = checked((long)(squaredCross / lengthSquared));
        return new RoadProjection(start, end, dot, lengthSquared, distanceSquared);
    }

    private readonly record struct RoadProjection(WorldPoint Start, WorldPoint End, long Numerator, long Denominator, long DistanceSquared);
}
