using Roentgen.Contracts;

namespace Roentgen.World;

public sealed class WorldTopologyValidator : IValidator<RegionalTopology>
{
    public ValidationReport Validate(RegionalTopology value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var issues = new List<ValidationIssue>();
        WorldGenerationProfile profile = value.Profile;
        int expected = checked(profile.TopologySamplesX * profile.TopologySamplesZ);
        foreach ((string Name, TerrainScalarField Field) field in new[]
                 {
                     ("elevation", value.Elevation),
                     ("moisture", value.Moisture),
                     ("contamination", value.Contamination),
                     ("industrial", value.IndustrialDisturbance),
                     ("settlement-suitability", value.SettlementSuitability)
                 })
        {
            if (field.Field.Width != profile.TopologySamplesX || field.Field.Depth != profile.TopologySamplesZ || field.Field.Values.Count != expected)
            {
                issues.Add(new ValidationIssue("topology.field-size", $"{field.Name} field does not match the generation profile.", true));
            }
        }

        if (value.RegionFamilies.Count != expected)
        {
            issues.Add(new ValidationIssue("topology.region-field-size", "Region-family field does not match the generation profile.", true));
        }

        if (value.Roads.Nodes.Count < 3 || value.Roads.Segments.Count < 3)
        {
            issues.Add(new ValidationIssue("topology.required-roads", "The micro-region requires three connected anchor nodes and corridors.", true));
        }

        ValidateRoadConnectivity(value, profile, issues);
        var evaluator = new TerrainHeightEvaluator(value);
        foreach (LandmarkReservation reservation in value.Reservations)
        {
            if (!profile.Bounds.Contains(reservation.Center) || !Contains(profile.Bounds, reservation.Bounds))
            {
                issues.Add(new ValidationIssue("topology.reservation-outside-world", $"Reservation {reservation.Name} is outside world bounds.", true));
            }

            if (reservation.RequiresRoadAccess && !value.Roads.Nodes.Any(node => node.Position.DistanceSquared(reservation.Center) <= 1_000_000L))
            {
                issues.Add(new ValidationIssue("topology.reservation-no-road", $"Reservation {reservation.Name} lacks a road anchor.", true));
            }

            int measuredSlope = MeasureReservationSlopePermille(evaluator, reservation);
            if (measuredSlope > reservation.MaximumSlopePermille)
            {
                issues.Add(new ValidationIssue(
                    "topology.reservation-slope",
                    $"Reservation {reservation.Name} has {measuredSlope} permille slope after terrain reservation; maximum is {reservation.MaximumSlopePermille}.",
                    true));
            }
        }

        if (value.Reservations.Select(static reservation => reservation.Id).Distinct().Count() != value.Reservations.Count)
        {
            issues.Add(new ValidationIssue("topology.duplicate-reservation-id", "Landmark reservation IDs are not unique.", true));
        }

        var families = value.RegionFamilies.ToHashSet();
        IReadOnlyList<RegionFamily> requiredFamilies = profile.RequiresAllRegionFamilies
            ? Enum.GetValues<RegionFamily>()
            : [RegionFamily.GlassBasin, RegionFamily.BrokenMeridian, RegionFamily.BlackwaterWorks];
        foreach (RegionFamily required in requiredFamilies)
        {
            if (!families.Contains(required))
            {
                string code = profile.RequiresAllRegionFamilies ? "topology.launch-region-missing" : "topology.prototype-region-missing";
                issues.Add(new ValidationIssue(code, $"Required region {required} was not generated for profile '{profile.ProfileId}'.", true));
            }
        }

        return new ValidationReport(issues.All(static issue => !issue.IsCritical), issues);
    }

    private static void ValidateRoadConnectivity(RegionalTopology topology, WorldGenerationProfile profile, ICollection<ValidationIssue> issues)
    {
        var adjacency = topology.Roads.Nodes.ToDictionary(static node => node.Id, static _ => new HashSet<StableId>());
        foreach (RoadSegment segment in topology.Roads.Segments)
        {
            if (!adjacency.ContainsKey(segment.StartNodeId) || !adjacency.ContainsKey(segment.EndNodeId))
            {
                issues.Add(new ValidationIssue("topology.road-node-reference", $"Road {segment.Name} references an unknown node.", true));
                continue;
            }

            adjacency[segment.StartNodeId].Add(segment.EndNodeId);
            adjacency[segment.EndNodeId].Add(segment.StartNodeId);
            if (segment.Polyline.Any(point => !profile.Bounds.Contains(point)))
            {
                issues.Add(new ValidationIssue("topology.road-outside-world", $"Road {segment.Name} leaves world bounds.", true));
            }
        }

        if (adjacency.Count == 0)
        {
            return;
        }

        var visited = new HashSet<StableId>();
        var pending = new Queue<StableId>();
        pending.Enqueue(adjacency.Keys.Order().First());
        while (pending.TryDequeue(out StableId current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (StableId neighbor in adjacency[current].Order())
            {
                pending.Enqueue(neighbor);
            }
        }

        if (visited.Count != adjacency.Count)
        {
            issues.Add(new ValidationIssue("topology.road-disconnected", "Required road anchors are not part of one connected graph.", true));
        }
    }

    private static int MeasureReservationSlopePermille(TerrainHeightEvaluator evaluator, LandmarkReservation reservation)
    {
        int center = evaluator.SampleHeightCentimeters(reservation.Center.XCentimeters, reservation.Center.ZCentimeters);
        int east = evaluator.SampleHeightCentimeters(reservation.Center.XCentimeters + reservation.HalfWidthCentimeters, reservation.Center.ZCentimeters);
        int west = evaluator.SampleHeightCentimeters(reservation.Center.XCentimeters - reservation.HalfWidthCentimeters, reservation.Center.ZCentimeters);
        int north = evaluator.SampleHeightCentimeters(reservation.Center.XCentimeters, reservation.Center.ZCentimeters - reservation.HalfDepthCentimeters);
        int south = evaluator.SampleHeightCentimeters(reservation.Center.XCentimeters, reservation.Center.ZCentimeters + reservation.HalfDepthCentimeters);
        int maximumRise = new[] { Math.Abs(east - center), Math.Abs(west - center), Math.Abs(north - center), Math.Abs(south - center) }.Max();
        int minimumRun = Math.Max(1, Math.Min(reservation.HalfWidthCentimeters, reservation.HalfDepthCentimeters));
        return maximumRise * 1000 / minimumRun;
    }

    private static bool Contains(WorldRect outer, WorldRect inner) =>
        inner.MinimumXCentimeters >= outer.MinimumXCentimeters && inner.MaximumXCentimeters <= outer.MaximumXCentimeters &&
        inner.MinimumZCentimeters >= outer.MinimumZCentimeters && inner.MaximumZCentimeters <= outer.MaximumZCentimeters;
}
