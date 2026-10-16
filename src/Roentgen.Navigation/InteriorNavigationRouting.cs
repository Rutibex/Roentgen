using Roentgen.Contracts;
using Roentgen.World;
using Roentgen.World.Structures;
using Roentgen.Core;

namespace Roentgen.Navigation;

public sealed record StructureNavigationPlacement(StableId StructureId, WorldPoint Origin, int BaseElevationCentimeters, int FloorHeightCentimeters = 320);

public sealed class InteriorNavigationRouteService
{
    public NavigationRoute Find(InteriorNavigationData data, StructureNavigationPlacement placement, NavigationRequest request, DynamicNavigationObstacleRegistry? obstacles = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(request);
        NavigationCell? start = ResolveCell(data, placement, request.Start);
        NavigationCell? goal = ResolveCell(data, placement, request.Destination);
        if (start is null) return NavigationRoute.Failed(request, NavigationFailureReason.StartUnavailable, "interior start cell unavailable");
        if (goal is null) return NavigationRoute.Failed(request, NavigationFailureReason.DestinationUnavailable, "interior destination cell unavailable");

        var cells = data.Cells.ToDictionary(static value => value.Id);
        var queue = new SortedSet<QueueEntry>(QueueEntryComparer.Instance) { new(0, start.Id) };
        var cost = new Dictionary<StableId, int> { [start.Id] = 0 };
        var parent = new Dictionary<StableId, ParentEntry>();
        int expanded = 0;
        while (queue.Count > 0)
        {
            QueueEntry entry = queue.Min!;
            queue.Remove(entry);
            if (!cost.TryGetValue(entry.CellId, out int currentCost) || currentCost != entry.Cost) continue;
            expanded++;
            if (entry.CellId == goal.Id) return Build(data, placement, request, cells, parent, start.Id, goal.Id, entry.Cost, expanded);
            if (expanded >= request.MaximumExpandedNodes) return NavigationRoute.Failed(request, NavigationFailureReason.SearchBudgetExceeded, "interior search budget exceeded", expanded);

            foreach (Transition transition in EnumerateTransitions(data, cells, entry.CellId, request.Profile, obstacles))
            {
                NavigationCell next = cells[transition.ToCellId];
                WorldPoint worldPoint = Center(next, placement);
                if (obstacles?.IsBlocked(worldPoint, data.StructureId, next.Id) == true) continue;
                int proposed = checked(entry.Cost + transition.Cost + (obstacles?.AdditionalCost(worldPoint, data.StructureId, next.Id) ?? 0));
                if (cost.TryGetValue(next.Id, out int previous) && previous <= proposed) continue;
                cost[next.Id] = proposed;
                parent[next.Id] = new ParentEntry(entry.CellId, transition);
                queue.Add(new QueueEntry(proposed, next.Id));
            }
        }
        return NavigationRoute.Failed(request, NavigationFailureReason.NoRoute, "no interior route exists", expanded);
    }

    private static IEnumerable<Transition> EnumerateTransitions(
        InteriorNavigationData data,
        IReadOnlyDictionary<StableId, NavigationCell> cells,
        StableId from,
        NavigationAgentProfile profile,
        DynamicNavigationObstacleRegistry? obstacles)
    {
        foreach (NavigationPortal portal in data.Portals.Where(value => !value.Exterior && (value.FromCellId == from || value.ToCellId == from)).OrderBy(static value => value.Id))
        {
            StableId to = portal.FromCellId == from ? portal.ToCellId : portal.FromCellId;
            if (!cells.ContainsKey(to)) continue;
            if (portal.WidthCentimeters * 10 < profile.RadiusMillimeters * 2 && !profile.Has(NavigationCapability.IgnoreNarrowClearance)) continue;
            DoorLifecycleState state = obstacles is not null && obstacles.TryGetDoor(portal.DoorId, out DynamicNavigationDoorState? dynamicDoor) && dynamicDoor is not null
                ? dynamicDoor.State
                : portal.DoorState;
            if (!TryResolveDoorTraversal(portal, state, profile, out NavigationStepKind kind, out int cost, out StableId interactionTargetId)) continue;
            cost = checked(cost + RestrictedAreaCost(data, to, profile));
            yield return new Transition(portal.Id, to, kind, cost, interactionTargetId, portal.Id.ToString());
        }

        foreach (NavigationLink link in data.Links.Where(value => value.FromCellId == from || value.ToCellId == from).OrderBy(static value => value.Id))
        {
            NavigationStepKind kind = link.Kind switch
            {
                VerticalLinkKind.Ladder => NavigationStepKind.ClimbLadder,
                VerticalLinkKind.Stair or VerticalLinkKind.Ramp => NavigationStepKind.UseStairs,
                VerticalLinkKind.CollapsedDrop => NavigationStepKind.Drop,
                _ => NavigationStepKind.UseStairs
            };
            if (kind == NavigationStepKind.ClimbLadder && !profile.Has(NavigationCapability.UseLadders)) continue;
            if (kind == NavigationStepKind.Drop && from == link.FromCellId) continue; // lower-to-upper traversal is impossible through a collapsed drop
            StableId to = link.FromCellId == from ? link.ToCellId : link.FromCellId;
            if (!cells.ContainsKey(to)) continue;
            int cost = kind switch
            {
                NavigationStepKind.ClimbLadder => profile.Costs.LadderCost,
                NavigationStepKind.Drop => Math.Max(1, profile.Costs.LadderCost / 2),
                _ => Math.Max(1, profile.Costs.LadderCost * 3 / 4)
            };
            cost = checked(cost + RestrictedAreaCost(data, to, profile));
            yield return new Transition(link.Id, to, kind, cost, StableId.Empty, link.Id.ToString());
        }
    }

    internal static bool TryResolveDoorTraversal(
        NavigationPortal portal,
        DoorLifecycleState state,
        NavigationAgentProfile profile,
        out NavigationStepKind kind,
        out int cost,
        out StableId interactionTargetId)
    {
        kind = NavigationStepKind.Walk;
        cost = 1000;
        interactionTargetId = StableId.Empty;
        if (state is DoorLifecycleState.Open or DoorLifecycleState.Destroyed or DoorLifecycleState.Missing) return true;
        if (state == DoorLifecycleState.Closed && (portal.LockPolicy is LockPolicy.None or LockPolicy.Optional) && profile.Has(NavigationCapability.OpenDoors))
        {
            kind = NavigationStepKind.OpenDoor;
            cost = checked(cost + profile.Costs.DoorInteractionCost);
            interactionTargetId = portal.DoorId;
            return true;
        }
        if ((state is DoorLifecycleState.Locked or DoorLifecycleState.Closed) && profile.Has(NavigationCapability.UnlockDoors))
        {
            kind = NavigationStepKind.UnlockDoor;
            cost = checked(cost + profile.Costs.DoorInteractionCost * 2);
            interactionTargetId = portal.DoorId;
            return true;
        }
        if (profile.Has(NavigationCapability.ForceDoors))
        {
            kind = NavigationStepKind.ForceDoor;
            cost = checked(cost + profile.Costs.DoorInteractionCost * 3);
            interactionTargetId = portal.DoorId;
            return true;
        }
        return false;
    }

    private static int RestrictedAreaCost(InteriorNavigationData data, StableId cellId, NavigationAgentProfile profile)
    {
        bool hazardous = data.RestrictedAreas.Any(value => value.CellId == cellId && value.Reason.Contains("hazard", StringComparison.OrdinalIgnoreCase));
        return hazardous && !profile.Has(NavigationCapability.HazardProtected) ? profile.Costs.HazardPenalty : 0;
    }

    private static NavigationRoute Build(
        InteriorNavigationData data,
        StructureNavigationPlacement placement,
        NavigationRequest request,
        IReadOnlyDictionary<StableId, NavigationCell> cells,
        Dictionary<StableId, ParentEntry> parent,
        StableId start,
        StableId goal,
        int totalCost,
        int expanded)
    {
        var chain = new List<(StableId From, StableId To, Transition Transition)>();
        StableId cursor = goal;
        while (cursor != start)
        {
            ParentEntry entry = parent[cursor];
            chain.Add((entry.Previous, cursor, entry.Transition));
            cursor = entry.Previous;
        }
        chain.Reverse();

        var steps = new List<NavigationRouteStep>();
        NavigationCell startCell = cells[start];
        steps.Add(new NavigationRouteStep(start, NavigationStepKind.Walk, ToLocation(data.StructureId, startCell, placement), 0, StableId.Empty, "start"));
        foreach ((StableId fromId, StableId toId, Transition transition) in chain)
        {
            NavigationCell fromCell = cells[fromId];
            NavigationCell toCell = cells[toId];
            if (!transition.InteractionTargetId.IsEmpty)
            {
                NavigationLocation doorway = MidpointLocation(data.StructureId, fromCell, toCell, placement);
                StableId actionNodeId = StableIdFactory.CreateChild(transition.Id, "navigation-action", transition.Kind.ToString());
                steps.Add(new NavigationRouteStep(actionNodeId, transition.Kind, doorway, transition.Cost, transition.InteractionTargetId, transition.Annotation));
                steps.Add(new NavigationRouteStep(toCell.Id, NavigationStepKind.Walk, ToLocation(data.StructureId, toCell, placement), 0, StableId.Empty, "enter cell"));
            }
            else
            {
                steps.Add(new NavigationRouteStep(toCell.Id, transition.Kind, ToLocation(data.StructureId, toCell, placement), transition.Cost, StableId.Empty, transition.Annotation));
            }
        }
        AppendExactDestination(steps, request.Destination);
        return new NavigationRoute(request.RequestId, true, NavigationFailureReason.None, steps, totalCost, request.NavigationRevision, expanded, "route complete");
    }

    private static void AppendExactDestination(List<NavigationRouteStep> steps, NavigationLocation destination)
    {
        if (steps.Count > 0 && steps[^1].Location == destination) return;
        StableId nodeId = StableIdFactory.Create("interior-nav-endpoint", $"{destination.StructureId}:{destination.CellId}:{destination.WorldPosition.XCentimeters}:{destination.WorldPosition.ZCentimeters}:{destination.ElevationCentimeters}");
        steps.Add(new NavigationRouteStep(nodeId, NavigationStepKind.Walk, destination with { Space = NavigationSpaceKind.Interior }, 0, StableId.Empty, "destination"));
    }

    private static NavigationCell? ResolveCell(InteriorNavigationData data, StructureNavigationPlacement placement, NavigationLocation location)
    {
        if (!location.CellId.IsEmpty) return data.Cells.FirstOrDefault(value => value.Id == location.CellId);
        int localX = location.WorldPosition.XCentimeters;
        int localZ = location.WorldPosition.ZCentimeters;
        if (!data.Cells.Any(value => value.Bounds.Contains(localX, localZ)))
        {
            localX -= placement.Origin.XCentimeters;
            localZ -= placement.Origin.ZCentimeters;
        }
        int approximateFloor = placement.FloorHeightCentimeters <= 0 ? 0 : Math.Max(0, (location.ElevationCentimeters - placement.BaseElevationCentimeters) / placement.FloorHeightCentimeters);
        return data.Cells.Where(value => value.Bounds.Contains(localX, localZ))
            .OrderBy(value => Math.Abs(value.Floor - approximateFloor))
            .ThenBy(static value => value.Id)
            .FirstOrDefault();
    }

    internal static WorldPoint Center(NavigationCell cell, StructureNavigationPlacement placement) => new(
        checked(placement.Origin.XCentimeters + cell.Bounds.MinimumXCentimeters + cell.Bounds.WidthCentimeters / 2),
        checked(placement.Origin.ZCentimeters + cell.Bounds.MinimumZCentimeters + cell.Bounds.DepthCentimeters / 2));

    internal static int Elevation(NavigationCell cell, StructureNavigationPlacement placement) => checked(placement.BaseElevationCentimeters + cell.Floor * placement.FloorHeightCentimeters);
    private static NavigationLocation ToLocation(StableId structureId, NavigationCell cell, StructureNavigationPlacement placement) => NavigationLocation.Interior(structureId, cell.Id, Center(cell, placement), Elevation(cell, placement));
    private static NavigationLocation MidpointLocation(StableId structureId, NavigationCell left, NavigationCell right, StructureNavigationPlacement placement)
    {
        WorldPoint a = Center(left, placement);
        WorldPoint b = Center(right, placement);
        return NavigationLocation.Interior(structureId, right.Id, new WorldPoint((a.XCentimeters + b.XCentimeters) / 2, (a.ZCentimeters + b.ZCentimeters) / 2), (Elevation(left, placement) + Elevation(right, placement)) / 2);
    }

    private sealed record Transition(StableId Id, StableId ToCellId, NavigationStepKind Kind, int Cost, StableId InteractionTargetId, string Annotation);
    private sealed record ParentEntry(StableId Previous, Transition Transition);
    private readonly record struct QueueEntry(int Cost, StableId CellId);
    private sealed class QueueEntryComparer : IComparer<QueueEntry>
    {
        public static QueueEntryComparer Instance { get; } = new();
        public int Compare(QueueEntry x, QueueEntry y) { int result = x.Cost.CompareTo(y.Cost); return result != 0 ? result : x.CellId.CompareTo(y.CellId); }
    }
}

public sealed class HierarchicalNavigationService
{
    private OutdoorNavigationGraph? outdoor;
    private readonly SortedDictionary<StableId, (InteriorNavigationData Data, StructureNavigationPlacement Placement)> interiors = [];
    private readonly OutdoorRouteFinder outdoorFinder = new();
    private readonly InteriorNavigationRouteService interiorFinder = new();
    public DynamicNavigationObstacleRegistry Obstacles { get; } = new();
    public ulong Revision => (outdoor?.Revision ?? 0) + Obstacles.Revision + (ulong)interiors.Count;

    public void SetOutdoorGraph(OutdoorNavigationGraph graph) => outdoor = graph ?? throw new ArgumentNullException(nameof(graph));
    public void RegisterInterior(InteriorNavigationData data, StructureNavigationPlacement placement)
    {
        if (data.StructureId != placement.StructureId) throw new InvalidOperationException("Interior placement structure ID mismatch.");
        interiors[data.StructureId] = (data, placement);
    }
    public bool RemoveInterior(StableId structureId) => interiors.Remove(structureId);

    public NavigationLocation ResolveLocation(WorldPoint point, int elevationCentimeters)
    {
        foreach (KeyValuePair<StableId, (InteriorNavigationData Data, StructureNavigationPlacement Placement)> pair in interiors)
        {
            StructureNavigationPlacement placement = pair.Value.Placement;
            int localX = point.XCentimeters - placement.Origin.XCentimeters;
            int localZ = point.ZCentimeters - placement.Origin.ZCentimeters;
            int approximateFloor = placement.FloorHeightCentimeters <= 0 ? 0 : Math.Max(0, (elevationCentimeters - placement.BaseElevationCentimeters) / placement.FloorHeightCentimeters);
            NavigationCell? cell = pair.Value.Data.Cells
                .Where(value => value.Bounds.Contains(localX, localZ))
                .OrderBy(value => Math.Abs(value.Floor - approximateFloor))
                .ThenBy(static value => value.Id)
                .FirstOrDefault();
            if (cell is not null)
            {
                int elevation = checked(placement.BaseElevationCentimeters + cell.Floor * placement.FloorHeightCentimeters);
                return NavigationLocation.Interior(pair.Key, cell.Id, point, elevation);
            }
        }
        return NavigationLocation.Outdoor(point, elevationCentimeters);
    }

    public NavigationLocation? FindNearestCover(WorldPoint actor, WorldPoint threat, int maximumDistanceCentimeters)
    {
        long maximumSquared = (long)maximumDistanceCentimeters * maximumDistanceCentimeters;
        (NavigationLocation Location, long Score, StableId CoverId)? best = null;
        foreach (KeyValuePair<StableId, (InteriorNavigationData Data, StructureNavigationPlacement Placement)> pair in interiors)
        {
            StableId structureId = pair.Key;
            (InteriorNavigationData Data, StructureNavigationPlacement Placement) interior = pair.Value;
            foreach (CoverPoint cover in interior.Data.Cover.OrderBy(static value => value.Id))
            {
                WorldPoint point = new(
                    checked(interior.Placement.Origin.XCentimeters + cover.Position.XCentimeters),
                    checked(interior.Placement.Origin.ZCentimeters + cover.Position.ZCentimeters));
                long actorDistance = DistanceSquared(actor, point);
                if (actorDistance > maximumSquared) continue;
                long threatDistance = DistanceSquared(threat, point);
                long protectionBonus = cover.HighCover ? 4_000_000L : 1_500_000L;
                long score = checked(actorDistance * 4L - Math.Min(threatDistance, 20_000_000L) - protectionBonus);
                NavigationLocation location = NavigationLocation.Interior(
                    structureId,
                    cover.CellId,
                    point,
                    checked(interior.Placement.BaseElevationCentimeters + cover.Position.YCentimeters));
                if (best is null || score < best.Value.Score || (score == best.Value.Score && cover.Id.CompareTo(best.Value.CoverId) < 0))
                    best = (location, score, cover.Id);
            }
        }
        return best?.Location;
    }

    public NavigationRoute Find(NavigationRequest request)
    {
        if (request.Start.Space == NavigationSpaceKind.Outdoor && request.Destination.Space == NavigationSpaceKind.Outdoor)
            return outdoor is null ? NavigationRoute.Failed(request, NavigationFailureReason.StreamingUnavailable, "outdoor graph is unavailable") : outdoorFinder.Find(outdoor, request, Obstacles);
        if (request.Start.StructureId == request.Destination.StructureId && !request.Start.StructureId.IsEmpty && interiors.TryGetValue(request.Start.StructureId, out var interior))
            return interiorFinder.Find(interior.Data, interior.Placement, request, Obstacles);
        return FindCrossSpace(request);
    }

    private NavigationRoute FindCrossSpace(NavigationRequest request)
    {
        if (outdoor is null) return NavigationRoute.Failed(request, NavigationFailureReason.StreamingUnavailable, "outdoor graph is unavailable");
        (InteriorNavigationData Data, StructureNavigationPlacement Placement)? sourceInterior =
            !request.Start.StructureId.IsEmpty && interiors.TryGetValue(request.Start.StructureId, out var sourceValue) ? sourceValue : null;
        (InteriorNavigationData Data, StructureNavigationPlacement Placement)? destinationInterior =
            !request.Destination.StructureId.IsEmpty && interiors.TryGetValue(request.Destination.StructureId, out var destinationValue) ? destinationValue : null;
        bool startsInterior = sourceInterior.HasValue;
        bool endsInterior = destinationInterior.HasValue;
        if (!startsInterior && !endsInterior) return NavigationRoute.Failed(request, NavigationFailureReason.DestinationUnavailable, "cross-space interior is unavailable");

        try
        {
            if (!startsInterior && endsInterior)
            {
                PortalBoundary boundary = ResolveExteriorBoundary(destinationInterior.Value.Data, destinationInterior.Value.Placement, request.Profile);
                NavigationRoute outside = outdoorFinder.Find(outdoor, request with { Destination = boundary.OutdoorLocation }, Obstacles);
                if (!outside.Success) return outside;
                NavigationRoute inside = interiorFinder.Find(destinationInterior.Value.Data, destinationInterior.Value.Placement, request with { Start = boundary.InteriorLocation }, Obstacles);
                if (!inside.Success) return inside;
                return Combine(request, outside, boundary, NavigationStepKind.EnterStructure, inside);
            }

            if (startsInterior && !endsInterior)
            {
                PortalBoundary boundary = ResolveExteriorBoundary(sourceInterior.Value.Data, sourceInterior.Value.Placement, request.Profile);
                NavigationRoute inside = interiorFinder.Find(sourceInterior.Value.Data, sourceInterior.Value.Placement, request with { Destination = boundary.InteriorLocation }, Obstacles);
                if (!inside.Success) return inside;
                NavigationRoute outside = outdoorFinder.Find(outdoor, request with { Start = boundary.OutdoorLocation }, Obstacles);
                if (!outside.Success) return outside;
                return Combine(request, inside, boundary, NavigationStepKind.ExitStructure, outside);
            }

            PortalBoundary exitBoundary = ResolveExteriorBoundary(sourceInterior.Value.Data, sourceInterior.Value.Placement, request.Profile);
            PortalBoundary enterBoundary = ResolveExteriorBoundary(destinationInterior.Value.Data, destinationInterior.Value.Placement, request.Profile);
            NavigationRoute sourceRoute = interiorFinder.Find(sourceInterior.Value.Data, sourceInterior.Value.Placement, request with { Destination = exitBoundary.InteriorLocation }, Obstacles);
            if (!sourceRoute.Success) return sourceRoute;
            NavigationRoute outsideRoute = outdoorFinder.Find(outdoor, request with { Start = exitBoundary.OutdoorLocation, Destination = enterBoundary.OutdoorLocation }, Obstacles);
            if (!outsideRoute.Success) return outsideRoute;
            NavigationRoute destinationRoute = interiorFinder.Find(destinationInterior.Value.Data, destinationInterior.Value.Placement, request with { Start = enterBoundary.InteriorLocation }, Obstacles);
            if (!destinationRoute.Success) return destinationRoute;
            NavigationRoute first = Combine(request, sourceRoute, exitBoundary, NavigationStepKind.ExitStructure, outsideRoute);
            return Combine(request, first, enterBoundary, NavigationStepKind.EnterStructure, destinationRoute);
        }
        catch (InvalidOperationException exception)
        {
            return NavigationRoute.Failed(request, NavigationFailureReason.CapabilityMissing, exception.Message);
        }
    }

    private PortalBoundary ResolveExteriorBoundary(InteriorNavigationData data, StructureNavigationPlacement placement, NavigationAgentProfile profile)
    {
        foreach (NavigationPortal portal in data.Portals.Where(static value => value.Exterior).OrderBy(static value => value.Id))
        {
            NavigationCell? roomCell = data.Cells.FirstOrDefault(value => value.Id == portal.FromCellId || value.Id == portal.ToCellId);
            if (roomCell is null) continue;
            DoorLifecycleState state = Obstacles.TryGetDoor(portal.DoorId, out DynamicNavigationDoorState? dynamicDoor) && dynamicDoor is not null ? dynamicDoor.State : portal.DoorState;
            if (!InteriorNavigationRouteService.TryResolveDoorTraversal(portal, state, profile, out NavigationStepKind actionKind, out int actionCost, out StableId interactionTargetId)) continue;
            WorldPoint point = InteriorNavigationRouteService.Center(roomCell, placement);
            int elevation = InteriorNavigationRouteService.Elevation(roomCell, placement);
            return new PortalBoundary(
                portal.Id,
                interactionTargetId,
                actionKind,
                actionCost,
                NavigationLocation.Outdoor(point, elevation),
                NavigationLocation.Interior(data.StructureId, roomCell.Id, point, elevation));
        }
        throw new InvalidOperationException($"Structure '{data.StructureId}' has no exterior portal usable by '{profile.Id}'.");
    }

    private static long DistanceSquared(WorldPoint left, WorldPoint right)
    {
        long dx = left.XCentimeters - right.XCentimeters;
        long dz = left.ZCentimeters - right.ZCentimeters;
        return checked(dx * dx + dz * dz);
    }

    private static NavigationRoute Combine(NavigationRequest request, NavigationRoute first, PortalBoundary boundary, NavigationStepKind transitionKind, NavigationRoute second)
    {
        var steps = new List<NavigationRouteStep>(first.Steps);
        if (!boundary.InteractionTargetId.IsEmpty)
        {
            StableId actionNode = StableIdFactory.CreateChild(boundary.PortalId, "navigation-action", transitionKind.ToString());
            steps.Add(new NavigationRouteStep(actionNode, boundary.ActionKind, boundary.InteriorLocation, boundary.ActionCost, boundary.InteractionTargetId, boundary.ActionKind.ToString()));
        }
        StableId transitionNode = StableIdFactory.CreateChild(boundary.PortalId, "navigation-transition", transitionKind.ToString());
        steps.Add(new NavigationRouteStep(transitionNode, transitionKind, boundary.InteriorLocation, 0, StableId.Empty, transitionKind.ToString()));
        steps.AddRange(second.Steps.Skip(1));
        return new NavigationRoute(
            request.RequestId,
            true,
            NavigationFailureReason.None,
            steps,
            checked(first.TotalCost + boundary.ActionCost + second.TotalCost),
            request.NavigationRevision,
            checked(first.ExpandedNodes + second.ExpandedNodes),
            "cross-space route complete");
    }

    private sealed record PortalBoundary(
        StableId PortalId,
        StableId InteractionTargetId,
        NavigationStepKind ActionKind,
        int ActionCost,
        NavigationLocation OutdoorLocation,
        NavigationLocation InteriorLocation);
}
