using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.World;

public sealed record RegionCell(int X, int Z, RegionFamily Family, int SettlementSuitability);

public sealed record RoadNode(StableId Id, string Name, WorldPoint Position, int ElevationCentimeters);

public sealed record RoadSegment(
    StableId Id,
    string Name,
    RoadKind Kind,
    StableId StartNodeId,
    StableId EndNodeId,
    int HalfWidthCentimeters,
    IReadOnlyList<WorldPoint> Polyline)
{
    public void Validate()
    {
        if (Id.IsEmpty || StartNodeId.IsEmpty || EndNodeId.IsEmpty)
        {
            throw new InvalidOperationException("Road IDs cannot be empty.");
        }

        if (HalfWidthCentimeters <= 0 || Polyline.Count < 2)
        {
            throw new InvalidOperationException("A road requires a positive width and at least two points.");
        }
    }
}

public sealed record RoadGraph(IReadOnlyList<RoadNode> Nodes, IReadOnlyList<RoadSegment> Segments)
{
    public void Validate()
    {
        var ids = new HashSet<StableId>();
        foreach (RoadNode node in Nodes)
        {
            if (node.Id.IsEmpty || !ids.Add(node.Id))
            {
                throw new InvalidOperationException("Road-node IDs must be non-empty and unique.");
            }
        }

        foreach (RoadSegment segment in Segments)
        {
            segment.Validate();
            if (!ids.Contains(segment.StartNodeId) || !ids.Contains(segment.EndNodeId))
            {
                throw new InvalidOperationException("A road segment references an unknown node.");
            }
        }
    }
}

public sealed record LandmarkReservation(
    StableId Id,
    string Name,
    LandmarkPurpose Purpose,
    WorldPoint Center,
    int HalfWidthCentimeters,
    int HalfDepthCentimeters,
    int TargetElevationCentimeters,
    int MaximumSlopePermille,
    bool RequiresRoadAccess)
{
    public WorldRect Bounds => new(
        Center.XCentimeters - HalfWidthCentimeters,
        Center.ZCentimeters - HalfDepthCentimeters,
        Center.XCentimeters + HalfWidthCentimeters,
        Center.ZCentimeters + HalfDepthCentimeters);

    public void Validate()
    {
        if (Id.IsEmpty || string.IsNullOrWhiteSpace(Name) || HalfWidthCentimeters <= 0 || HalfDepthCentimeters <= 0)
        {
            throw new InvalidOperationException("Landmark reservation is invalid.");
        }

        if (MaximumSlopePermille is < 0 or > 1_000)
        {
            throw new InvalidOperationException("Landmark slope limit must be from 0 through 1000 permille.");
        }
    }
}

public sealed class RegionalTopology : ICanonicalWritable
{
    private readonly RegionFamily[] _regionFamilies;

    public RegionalTopology(
        StableId id,
        CampaignSeed campaignSeed,
        WorldGenerationProfile profile,
        TerrainScalarField elevation,
        TerrainScalarField moisture,
        TerrainScalarField contamination,
        TerrainScalarField industrialDisturbance,
        TerrainScalarField settlementSuitability,
        IReadOnlyList<RegionFamily> regionFamilies,
        RoadGraph roads,
        IReadOnlyList<LandmarkReservation> reservations,
        GenerationProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(elevation);
        ArgumentNullException.ThrowIfNull(moisture);
        ArgumentNullException.ThrowIfNull(contamination);
        ArgumentNullException.ThrowIfNull(industrialDisturbance);
        ArgumentNullException.ThrowIfNull(settlementSuitability);
        ArgumentNullException.ThrowIfNull(regionFamilies);
        ArgumentNullException.ThrowIfNull(roads);
        ArgumentNullException.ThrowIfNull(reservations);
        ArgumentNullException.ThrowIfNull(provenance);
        if (id.IsEmpty)
        {
            throw new ArgumentException("Topology ID cannot be empty.", nameof(id));
        }

        profile.Validate();
        int expected = checked(profile.TopologySamplesX * profile.TopologySamplesZ);
        if (elevation.Values.Count != expected || moisture.Values.Count != expected || contamination.Values.Count != expected ||
            industrialDisturbance.Values.Count != expected || settlementSuitability.Values.Count != expected || regionFamilies.Count != expected)
        {
            throw new ArgumentException("Topology field dimensions do not match the profile.");
        }

        Id = id;
        CampaignSeed = campaignSeed;
        Profile = profile;
        Elevation = elevation;
        Moisture = moisture;
        Contamination = contamination;
        IndustrialDisturbance = industrialDisturbance;
        SettlementSuitability = settlementSuitability;
        _regionFamilies = regionFamilies.ToArray();
        Roads = roads;
        Reservations = reservations.ToArray();
        Provenance = provenance;
        Roads.Validate();
        foreach (LandmarkReservation reservation in Reservations)
        {
            reservation.Validate();
        }
    }

    public StableId Id { get; }
    public CampaignSeed CampaignSeed { get; }
    public WorldGenerationProfile Profile { get; }
    public TerrainScalarField Elevation { get; }
    public TerrainScalarField Moisture { get; }
    public TerrainScalarField Contamination { get; }
    public TerrainScalarField IndustrialDisturbance { get; }
    public TerrainScalarField SettlementSuitability { get; }
    public IReadOnlyList<RegionFamily> RegionFamilies => _regionFamilies;
    public RoadGraph Roads { get; }
    public IReadOnlyList<LandmarkReservation> Reservations { get; }
    public GenerationProvenance Provenance { get; }
    public StateChecksum ContentChecksum => ComputeChecksum();

    public RegionFamily RegionAtCell(int x, int z) => _regionFamilies[checked(z * Profile.TopologySamplesX + x)];

    public RegionFamily RegionAtWorld(int xCentimeters, int zCentimeters)
    {
        int x = Math.Clamp(WorldUnits.FloorDivide(xCentimeters - Profile.MinimumWorldXCentimeters, Profile.TopologyCellCentimeters), 0, Profile.TopologySamplesX - 1);
        int z = Math.Clamp(WorldUnits.FloorDivide(zCentimeters - Profile.MinimumWorldZCentimeters, Profile.TopologyCellCentimeters), 0, Profile.TopologySamplesZ - 1);
        return RegionAtCell(x, z);
    }

    public StateChecksum ComputeChecksum()
    {
        var writer = new CanonicalWriter();
        WriteCanonical(writer);
        return writer.ComputeChecksum();
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStableId(Id);
        writer.WriteUInt64(CampaignSeed.Value);
        writer.WriteInt32(Profile.SchemaVersion);
        writer.WriteInt32(Profile.WorldWidthMeters);
        writer.WriteInt32(Profile.WorldDepthMeters);
        writer.WriteInt32(Profile.ChunkSizeMeters);
        writer.WriteInt32(Profile.NearChunkSamples);
        writer.WriteInt32(Profile.TopologyCellMeters);
        Elevation.WriteCanonical(writer);
        Moisture.WriteCanonical(writer);
        Contamination.WriteCanonical(writer);
        IndustrialDisturbance.WriteCanonical(writer);
        SettlementSuitability.WriteCanonical(writer);
        writer.WriteCount(_regionFamilies.Length);
        foreach (RegionFamily family in _regionFamilies)
        {
            writer.WriteInt32((int)family);
        }

        writer.WriteCount(Roads.Nodes.Count);
        foreach (RoadNode node in Roads.Nodes.OrderBy(static node => node.Id))
        {
            writer.WriteStableId(node.Id);
            writer.WriteString(node.Name);
            writer.WriteInt32(node.Position.XCentimeters);
            writer.WriteInt32(node.Position.ZCentimeters);
            writer.WriteInt32(node.ElevationCentimeters);
        }

        writer.WriteCount(Roads.Segments.Count);
        foreach (RoadSegment segment in Roads.Segments.OrderBy(static segment => segment.Id))
        {
            writer.WriteStableId(segment.Id);
            writer.WriteString(segment.Name);
            writer.WriteInt32((int)segment.Kind);
            writer.WriteStableId(segment.StartNodeId);
            writer.WriteStableId(segment.EndNodeId);
            writer.WriteInt32(segment.HalfWidthCentimeters);
            writer.WriteCount(segment.Polyline.Count);
            foreach (WorldPoint point in segment.Polyline)
            {
                writer.WriteInt32(point.XCentimeters);
                writer.WriteInt32(point.ZCentimeters);
            }
        }

        writer.WriteCount(Reservations.Count);
        foreach (LandmarkReservation reservation in Reservations.OrderBy(static reservation => reservation.Id))
        {
            writer.WriteStableId(reservation.Id);
            writer.WriteString(reservation.Name);
            writer.WriteInt32((int)reservation.Purpose);
            writer.WriteInt32(reservation.Center.XCentimeters);
            writer.WriteInt32(reservation.Center.ZCentimeters);
            writer.WriteInt32(reservation.HalfWidthCentimeters);
            writer.WriteInt32(reservation.HalfDepthCentimeters);
            writer.WriteInt32(reservation.TargetElevationCentimeters);
            writer.WriteInt32(reservation.MaximumSlopePermille);
            writer.WriteBoolean(reservation.RequiresRoadAccess);
        }
    }
}
