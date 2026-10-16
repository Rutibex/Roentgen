namespace Roentgen.World;

internal static class RoadGraphGenerator
{
    public static IReadOnlyList<WorldPoint> FindPath(
        WorldGenerationProfile profile,
        TerrainScalarField elevation,
        TerrainScalarField moisture,
        TerrainScalarField contamination,
        TerrainScalarField industrial,
        (int X, int Z) start,
        (int X, int Z) goal,
        RoadKind kind)
    {
        int width = profile.TopologySamplesX;
        int depth = profile.TopologySamplesZ;
        int startIndex = start.Z * width + start.X;
        int goalIndex = goal.Z * width + goal.X;
        var open = new PriorityQueue<PathCandidate, PathPriority>();
        var best = Enumerable.Repeat(long.MaxValue, width * depth).ToArray();
        var previous = Enumerable.Repeat(-1, width * depth).ToArray();
        best[startIndex] = 0;
        open.Enqueue(new PathCandidate(startIndex, 0), new PathPriority(Heuristic(start.X, start.Z, goal.X, goal.Z), 0, startIndex));
        int[] directions = [-1, 0, 1, 0, -1];

        while (open.TryDequeue(out PathCandidate candidate, out _))
        {
            if (candidate.Cost != best[candidate.Index])
            {
                continue;
            }

            if (candidate.Index == goalIndex)
            {
                return Reconstruct(profile, previous, startIndex, goalIndex);
            }

            int x = candidate.Index % width;
            int z = candidate.Index / width;
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + directions[direction];
                int nz = z + directions[direction + 1];
                if (nx < 1 || nz < 1 || nx >= width - 1 || nz >= depth - 1)
                {
                    continue;
                }

                int next = nz * width + nx;
                int slope = Math.Abs(elevation[nx, nz] - elevation[x, z]);
                int wet = moisture[nx, nz];
                int hazard = contamination[nx, nz];
                int disturbed = industrial[nx, nz];
                long cost = 1000L + slope * (kind == RoadKind.Trail ? 2L : 5L);
                cost += kind == RoadKind.Trail ? wet / 256L : wet / 96L;
                cost += hazard / 192L;
                if (kind != RoadKind.Trail)
                {
                    cost -= disturbed / 512L;
                }

                cost = Math.Max(100L, cost);
                long nextCost = checked(candidate.Cost + cost);
                if (nextCost >= best[next])
                {
                    continue;
                }

                best[next] = nextCost;
                previous[next] = candidate.Index;
                long estimated = checked(nextCost + Heuristic(nx, nz, goal.X, goal.Z));
                open.Enqueue(new PathCandidate(next, nextCost), new PathPriority(estimated, nextCost, next));
            }
        }

        return CreateDirectFallback(profile, start, goal);
    }

    private static IReadOnlyList<WorldPoint> Reconstruct(WorldGenerationProfile profile, IReadOnlyList<int> previous, int start, int goal)
    {
        int width = profile.TopologySamplesX;
        var reverse = new List<int>();
        int current = goal;
        while (current >= 0)
        {
            reverse.Add(current);
            if (current == start)
            {
                break;
            }

            current = previous[current];
        }

        reverse.Reverse();
        var simplified = new List<int>();
        for (int index = 0; index < reverse.Count; index++)
        {
            if (index > 0 && index < reverse.Count - 1)
            {
                int previousIndex = reverse[index - 1];
                int currentIndex = reverse[index];
                int nextIndex = reverse[index + 1];
                int dx1 = currentIndex % width - previousIndex % width;
                int dz1 = currentIndex / width - previousIndex / width;
                int dx2 = nextIndex % width - currentIndex % width;
                int dz2 = nextIndex / width - currentIndex / width;
                if (dx1 == dx2 && dz1 == dz2)
                {
                    continue;
                }
            }

            simplified.Add(reverse[index]);
        }

        return simplified.Select(index => CellToWorld(profile, index % width, index / width)).ToArray();
    }

    private static IReadOnlyList<WorldPoint> CreateDirectFallback(WorldGenerationProfile profile, (int X, int Z) start, (int X, int Z) goal) =>
        [CellToWorld(profile, start.X, start.Z), CellToWorld(profile, goal.X, goal.Z)];

    private static WorldPoint CellToWorld(WorldGenerationProfile profile, int x, int z) => new(
        profile.MinimumWorldXCentimeters + x * profile.TopologyCellCentimeters,
        profile.MinimumWorldZCentimeters + z * profile.TopologyCellCentimeters);

    private static long Heuristic(int x, int z, int goalX, int goalZ) =>
        checked((long)(Math.Abs(goalX - x) + Math.Abs(goalZ - z)) * 1000L);

    private readonly record struct PathCandidate(int Index, long Cost);

    private readonly record struct PathPriority(long EstimatedCost, long Cost, int Index) : IComparable<PathPriority>
    {
        public int CompareTo(PathPriority other)
        {
            int result = EstimatedCost.CompareTo(other.EstimatedCost);
            if (result != 0) return result;
            result = Cost.CompareTo(other.Cost);
            return result != 0 ? result : Index.CompareTo(other.Index);
        }
    }
}
