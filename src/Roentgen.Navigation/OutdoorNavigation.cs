using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.World;

namespace Roentgen.Navigation;

public sealed record OutdoorNavigationNode(
    StableId Id,
    WorldPoint Position,
    int ElevationCentimeters,
    TerrainSurfaceKind Surface,
    int SlopePermille,
    bool RoadPreferred,
    TerrainChunkAddress ChunkAddress);

public sealed record OutdoorNavigationEdge(
    StableId Id,
    StableId FromNodeId,
    StableId ToNodeId,
    int Cost,
    NavigationStepKind StepKind,
    bool Bidirectional = true);

public sealed class OutdoorNavigationGraph : ICanonicalWritable
{
    private readonly SortedDictionary<StableId, OutdoorNavigationNode> nodes;
    private readonly SortedDictionary<StableId, OutdoorNavigationEdge> edges;
    private readonly Dictionary<StableId, OutdoorNavigationEdge[]> outgoing;

    public OutdoorNavigationGraph(IEnumerable<OutdoorNavigationNode> nodes, IEnumerable<OutdoorNavigationEdge> edges, ulong revision = 1)
    {
        this.nodes = new SortedDictionary<StableId, OutdoorNavigationNode>(nodes.ToDictionary(static value => value.Id));
        this.edges = new SortedDictionary<StableId, OutdoorNavigationEdge>(edges.ToDictionary(static value => value.Id));
        Revision = revision;
        var mutable = this.nodes.Keys.ToDictionary(static id => id, static _ => new List<OutdoorNavigationEdge>());
        foreach (OutdoorNavigationEdge edge in this.edges.Values)
        {
            if (!this.nodes.ContainsKey(edge.FromNodeId) || !this.nodes.ContainsKey(edge.ToNodeId))
                throw new InvalidOperationException($"Outdoor navigation edge '{edge.Id}' references an unknown node.");
            mutable[edge.FromNodeId].Add(edge);
            if (edge.Bidirectional)
                mutable[edge.ToNodeId].Add(edge with { FromNodeId = edge.ToNodeId, ToNodeId = edge.FromNodeId });
        }
        outgoing = mutable.ToDictionary(static pair => pair.Key, static pair => pair.Value.OrderBy(value => value.ToNodeId).ThenBy(value => value.Id).ToArray());
    }

    public IReadOnlyCollection<OutdoorNavigationNode> Nodes => nodes.Values;
    public IReadOnlyCollection<OutdoorNavigationEdge> Edges => edges.Values;
    public ulong Revision { get; }
    public OutdoorNavigationNode GetNode(StableId id) => nodes.TryGetValue(id, out OutdoorNavigationNode? value) ? value : throw new KeyNotFoundException($"Unknown navigation node '{id}'.");
    public IReadOnlyList<OutdoorNavigationEdge> Outgoing(StableId id) => outgoing.TryGetValue(id, out OutdoorNavigationEdge[]? value) ? value : [];

    public OutdoorNavigationNode? FindNearest(WorldPoint point, NavigationAgentProfile profile, DynamicNavigationObstacleRegistry? obstacles = null)
    {
        return nodes.Values
            .Where(value => value.SlopePermille <= profile.MaximumSlopePermille)
            .Where(value => value.Surface != TerrainSurfaceKind.ShallowWater || profile.Has(NavigationCapability.Swim) || profile.Has(NavigationCapability.Float))
            .Where(value => obstacles is null || !obstacles.IsBlocked(value.Position, StableId.Empty, StableId.Empty))
            .OrderBy(value => value.Position.DistanceSquared(point))
            .ThenBy(static value => value.Id)
            .FirstOrDefault();
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteUInt64(Revision);
        writer.WriteCount(nodes.Count);
        foreach (OutdoorNavigationNode node in nodes.Values)
        {
            writer.WriteStableId(node.Id); writer.WriteInt32(node.Position.XCentimeters); writer.WriteInt32(node.Position.ZCentimeters);
            writer.WriteInt32(node.ElevationCentimeters); writer.WriteInt32((int)node.Surface); writer.WriteInt32(node.SlopePermille);
            writer.WriteBoolean(node.RoadPreferred); writer.WriteInt32(node.ChunkAddress.X); writer.WriteInt32(node.ChunkAddress.Z);
        }
        writer.WriteCount(edges.Count);
        foreach (OutdoorNavigationEdge edge in edges.Values)
        {
            writer.WriteStableId(edge.Id); writer.WriteStableId(edge.FromNodeId); writer.WriteStableId(edge.ToNodeId); writer.WriteInt32(edge.Cost);
            writer.WriteInt32((int)edge.StepKind); writer.WriteBoolean(edge.Bidirectional);
        }
    }
}

public static class OutdoorNavigationGraphBuilder
{
    public static OutdoorNavigationGraph BuildRoadGraph(RegionalTopology topology, NavigationAgentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(profile);
        var evaluator = new TerrainHeightEvaluator(topology);
        var nodes = new SortedDictionary<StableId, OutdoorNavigationNode>();
        var edges = new List<OutdoorNavigationEdge>();
        foreach (RoadNode roadNode in topology.Roads.Nodes.OrderBy(static value => value.Id))
        {
            TerrainSurfaceKind surface = evaluator.SampleSurface(roadNode.Position.XCentimeters, roadNode.Position.ZCentimeters, roadNode.ElevationCentimeters, 0);
            TerrainChunkAddress chunk = TerrainChunkAddress.FromWorldCentimeters(roadNode.Position.XCentimeters, roadNode.Position.ZCentimeters, topology.Profile.ChunkSizeCentimeters);
            nodes[roadNode.Id] = new OutdoorNavigationNode(roadNode.Id, roadNode.Position, roadNode.ElevationCentimeters, surface, 0, true, chunk);
        }
        foreach (RoadSegment segment in topology.Roads.Segments.OrderBy(static value => value.Id))
        {
            int length = PolylineLength(segment.Polyline);
            int surfaceCost = profile.Costs.SurfaceCost(segment.Kind == RoadKind.Trail ? TerrainSurfaceKind.Trail : TerrainSurfaceKind.Road, profile.Has(NavigationCapability.HazardProtected));
            int cost = Math.Max(1, checked(length * surfaceCost / 1000));
            NavigationStepKind kind = segment.Kind == RoadKind.Trail ? NavigationStepKind.Trail : NavigationStepKind.Road;
            edges.Add(new OutdoorNavigationEdge(segment.Id, segment.StartNodeId, segment.EndNodeId, cost, kind));
        }
        return new OutdoorNavigationGraph(nodes.Values, edges, 1);
    }

    public static OutdoorNavigationGraph BuildFromChunks(IEnumerable<TerrainChunkData> chunks, NavigationAgentProfile profile, int sampleStride = 4)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(profile);
        if (sampleStride <= 0) throw new ArgumentOutOfRangeException(nameof(sampleStride));
        var nodeByCoordinate = new SortedDictionary<(int X, int Z), OutdoorNavigationNode>();
        int chunkSize = 0;
        foreach (TerrainChunkData chunk in chunks.OrderBy(static value => value.Address))
        {
            TerrainChunkLodData lod = chunk.GetLod(0);
            chunkSize = lod.SpacingCentimeters * (lod.SampleCount - 1);
            WorldPoint origin = chunk.Address.Origin(chunkSize);
            for (int z = 0; z < lod.SampleCount; z += sampleStride)
            {
                for (int x = 0; x < lod.SampleCount; x += sampleStride)
                {
                    int worldX = origin.XCentimeters + x * lod.SpacingCentimeters;
                    int worldZ = origin.ZCentimeters + z * lod.SpacingCentimeters;
                    int height = lod.HeightAt(x, z);
                    int x2 = Math.Min(lod.SampleCount - 1, x + sampleStride);
                    int z2 = Math.Min(lod.SampleCount - 1, z + sampleStride);
                    int rise = Math.Max(Math.Abs(lod.HeightAt(x2, z) - height), Math.Abs(lod.HeightAt(x, z2) - height));
                    int run = Math.Max(1, lod.SpacingCentimeters * sampleStride);
                    int slope = rise * 1000 / run;
                    TerrainSurfaceKind surface = lod.SurfaceAt(x, z);
                    if (slope > profile.MaximumSlopePermille) continue;
                    if (surface == TerrainSurfaceKind.ShallowWater && !profile.Has(NavigationCapability.Swim) && !profile.Has(NavigationCapability.Float)) continue;
                    StableId id = StableIdFactory.Create("outdoor-nav-node", $"{worldX}:{worldZ}");
                    nodeByCoordinate[(worldX, worldZ)] = new OutdoorNavigationNode(id, new WorldPoint(worldX, worldZ), height, surface, slope, surface is TerrainSurfaceKind.Road or TerrainSurfaceKind.Trail, chunk.Address);
                }
            }
        }
        var edges = new List<OutdoorNavigationEdge>();
        int spacing = nodeByCoordinate.Count > 1 ? FindMinimumSpacing(nodeByCoordinate.Keys) : Math.Max(1, chunkSize);
        foreach (KeyValuePair<(int X, int Z), OutdoorNavigationNode> pair in nodeByCoordinate)
        {
            (int X, int Z) coordinate = pair.Key;
            OutdoorNavigationNode node = pair.Value;
            foreach ((int dx, int dz) in new[] { (spacing, 0), (0, spacing), (spacing, spacing), (spacing, -spacing) })
            {
                if (!nodeByCoordinate.TryGetValue((coordinate.X + dx, coordinate.Z + dz), out OutdoorNavigationNode? neighbor)) continue;
                int vertical = Math.Abs(neighbor.ElevationCentimeters - node.ElevationCentimeters);
                int horizontal = dx == 0 || dz == 0 ? spacing : (int)Math.Round(spacing * Math.Sqrt(2.0), MidpointRounding.AwayFromZero);
                int slope = vertical * 1000 / Math.Max(1, horizontal);
                if (slope > profile.MaximumSlopePermille) continue;
                int surfaceCost = (profile.Costs.SurfaceCost(node.Surface, profile.Has(NavigationCapability.HazardProtected)) + profile.Costs.SurfaceCost(neighbor.Surface, profile.Has(NavigationCapability.HazardProtected))) / 2;
                int cost = checked(horizontal * surfaceCost / 1000 + slope * profile.Costs.SlopeCostPerPermille);
                StableId edgeId = StableIdFactory.Create("outdoor-nav-edge", $"{node.Id}:{neighbor.Id}");
                NavigationStepKind kind = node.RoadPreferred && neighbor.RoadPreferred ? NavigationStepKind.Road : NavigationStepKind.Walk;
                edges.Add(new OutdoorNavigationEdge(edgeId, node.Id, neighbor.Id, Math.Max(1, cost), kind));
            }
        }
        return new OutdoorNavigationGraph(nodeByCoordinate.Values, edges, 1);
    }

    private static int FindMinimumSpacing(IEnumerable<(int X, int Z)> coordinates)
    {
        int[] xs = coordinates.Select(static value => value.X).Distinct().Order().ToArray();
        int[] zs = coordinates.Select(static value => value.Z).Distinct().Order().ToArray();
        int best = int.MaxValue;
        for (int index = 1; index < xs.Length; index++) best = Math.Min(best, xs[index] - xs[index - 1]);
        for (int index = 1; index < zs.Length; index++) best = Math.Min(best, zs[index] - zs[index - 1]);
        return best == int.MaxValue ? 100 : Math.Max(1, best);
    }

    private static int PolylineLength(IReadOnlyList<WorldPoint> points)
    {
        long total = 0;
        for (int index = 0; index < points.Count - 1; index++)
        {
            long dx = (long)points[index + 1].XCentimeters - points[index].XCentimeters;
            long dz = (long)points[index + 1].ZCentimeters - points[index].ZCentimeters;
            total = checked(total + (long)Math.Round(Math.Sqrt((double)dx * dx + (double)dz * dz), MidpointRounding.AwayFromZero));
        }
        return checked((int)Math.Min(int.MaxValue, total));
    }
}

public sealed class OutdoorRouteFinder
{
    public NavigationRoute Find(OutdoorNavigationGraph graph, NavigationRequest request, DynamicNavigationObstacleRegistry? obstacles = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId.IsEmpty || request.AgentId.IsEmpty) return NavigationRoute.Failed(request, NavigationFailureReason.InvalidRequest, "request and agent IDs are required");
        OutdoorNavigationNode? start = graph.FindNearest(request.Start.WorldPosition, request.Profile, obstacles);
        OutdoorNavigationNode? goal = graph.FindNearest(request.Destination.WorldPosition, request.Profile, obstacles);
        if (start is null) return NavigationRoute.Failed(request, NavigationFailureReason.StartUnavailable, "no traversable start node");
        if (goal is null) return NavigationRoute.Failed(request, NavigationFailureReason.DestinationUnavailable, "no traversable destination node");
        if (start.Id == goal.Id)
        {
            NavigationRouteStep startStep = ToStep(start, NavigationStepKind.Walk, 0, "nearest navigation node");
            NavigationRouteStep destinationStep = ToEndpointStep(request.Destination, "destination");
            int directCost = EndpointDistanceCost(start.Position, request.Destination.WorldPosition);
            return new NavigationRoute(request.RequestId, true, NavigationFailureReason.None, [startStep, destinationStep], directCost, request.NavigationRevision, 1, "route complete");
        }

        var open = new SortedSet<QueueEntry>(QueueEntryComparer.Instance);
        var cost = new Dictionary<StableId, int> { [start.Id] = 0 };
        var parent = new Dictionary<StableId, (StableId Previous, OutdoorNavigationEdge Edge)>();
        open.Add(new QueueEntry(Heuristic(start, goal), 0, start.Id));
        int expanded = 0;
        StableId bestNode = start.Id;
        long bestDistance = start.Position.DistanceSquared(goal.Position);
        while (open.Count > 0)
        {
            QueueEntry currentEntry = open.Min!;
            open.Remove(currentEntry);
            if (!cost.TryGetValue(currentEntry.NodeId, out int currentCost) || currentCost != currentEntry.Cost) continue;
            OutdoorNavigationNode current = graph.GetNode(currentEntry.NodeId);
            expanded++;
            long distance = current.Position.DistanceSquared(goal.Position);
            if (distance < bestDistance || (distance == bestDistance && current.Id.CompareTo(bestNode) < 0)) { bestDistance = distance; bestNode = current.Id; }
            if (current.Id == goal.Id) return Build(graph, request, parent, start.Id, goal.Id, currentCost, expanded);
            if (expanded >= request.MaximumExpandedNodes)
            {
                if (request.AllowPartialRoute && bestNode != start.Id) return Build(graph, request, parent, start.Id, bestNode, cost[bestNode], expanded, true);
                return NavigationRoute.Failed(request, NavigationFailureReason.SearchBudgetExceeded, "navigation search budget exceeded", expanded);
            }
            foreach (OutdoorNavigationEdge edge in graph.Outgoing(current.Id))
            {
                OutdoorNavigationNode next = graph.GetNode(edge.ToNodeId);
                if (obstacles?.IsBlocked(next.Position, StableId.Empty, StableId.Empty) == true) continue;
                int dynamicCost = obstacles?.AdditionalCost(next.Position, StableId.Empty, StableId.Empty) ?? 0;
                int proposed = checked(currentCost + edge.Cost + dynamicCost);
                if (cost.TryGetValue(next.Id, out int prior) && prior <= proposed) continue;
                cost[next.Id] = proposed;
                parent[next.Id] = (current.Id, edge);
                open.Add(new QueueEntry(checked(proposed + Heuristic(next, goal)), proposed, next.Id));
            }
        }
        if (request.AllowPartialRoute && bestNode != start.Id) return Build(graph, request, parent, start.Id, bestNode, cost[bestNode], expanded, true);
        return NavigationRoute.Failed(request, NavigationFailureReason.NoRoute, "no outdoor route exists", expanded);
    }

    private static NavigationRoute Build(OutdoorNavigationGraph graph, NavigationRequest request, Dictionary<StableId, (StableId Previous, OutdoorNavigationEdge Edge)> parent, StableId start, StableId goal, int totalCost, int expanded, bool partial = false)
    {
        var reversed = new List<NavigationRouteStep>();
        StableId cursor = goal;
        while (cursor != start)
        {
            (StableId previous, OutdoorNavigationEdge edge) = parent[cursor];
            reversed.Add(ToStep(graph.GetNode(cursor), edge.StepKind, edge.Cost, edge.Id.ToString()));
            cursor = previous;
        }
        reversed.Add(ToStep(graph.GetNode(start), NavigationStepKind.Walk, 0, "nearest navigation node"));
        reversed.Reverse();
        if (!partial)
        {
            int endpointCost = EndpointDistanceCost(graph.GetNode(goal).Position, request.Destination.WorldPosition);
            reversed.Add(ToEndpointStep(request.Destination, "destination"));
            totalCost = checked(totalCost + endpointCost);
        }
        return new NavigationRoute(request.RequestId, true, NavigationFailureReason.None, reversed, totalCost, request.NavigationRevision, expanded, partial ? "partial route" : "route complete");
    }

    private static NavigationRouteStep ToStep(OutdoorNavigationNode node, NavigationStepKind kind, int cost, string annotation) => new(
        node.Id, kind, NavigationLocation.Outdoor(node.Position, node.ElevationCentimeters), cost, StableId.Empty, annotation);

    private static NavigationRouteStep ToEndpointStep(NavigationLocation location, string annotation)
    {
        StableId nodeId = StableIdFactory.Create("outdoor-nav-endpoint", $"{location.WorldPosition.XCentimeters}:{location.WorldPosition.ZCentimeters}:{location.ElevationCentimeters}");
        return new NavigationRouteStep(nodeId, NavigationStepKind.Walk, location with { Space = NavigationSpaceKind.Outdoor }, 0, StableId.Empty, annotation);
    }

    private static int EndpointDistanceCost(WorldPoint from, WorldPoint to)
    {
        long dx = (long)from.XCentimeters - to.XCentimeters;
        long dz = (long)from.ZCentimeters - to.ZCentimeters;
        return checked((int)Math.Min(int.MaxValue, Math.Round(Math.Sqrt((double)dx * dx + (double)dz * dz), MidpointRounding.AwayFromZero)));
    }

    private static int Heuristic(OutdoorNavigationNode left, OutdoorNavigationNode right)
    {
        long dx = (long)left.Position.XCentimeters - right.Position.XCentimeters;
        long dz = (long)left.Position.ZCentimeters - right.Position.ZCentimeters;
        return checked((int)Math.Min(int.MaxValue, Math.Round(Math.Sqrt((double)dx * dx + (double)dz * dz), MidpointRounding.AwayFromZero)));
    }

    private readonly record struct QueueEntry(int Priority, int Cost, StableId NodeId);
    private sealed class QueueEntryComparer : IComparer<QueueEntry>
    {
        public static QueueEntryComparer Instance { get; } = new();
        public int Compare(QueueEntry x, QueueEntry y)
        {
            int result = x.Priority.CompareTo(y.Priority);
            if (result != 0) return result;
            result = x.Cost.CompareTo(y.Cost);
            return result != 0 ? result : x.NodeId.CompareTo(y.NodeId);
        }
    }
}
