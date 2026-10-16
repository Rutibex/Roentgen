using Microsoft.Xna.Framework;
using Roentgen.World;

namespace Roentgen.Rendering;

public static class TerrainRenderingBridge
{
    public static MeshData ToMeshData(TerrainChunkData chunk, int lod, bool includeSkirts = true, int skirtDepthCentimeters = 250)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (skirtDepthCentimeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skirtDepthCentimeters));
        }

        TerrainChunkLodData source = chunk.GetLod(lod);
        int count = source.SampleCount;
        float spacing = WorldUnits.CentimetersToMeters(source.SpacingCentimeters);
        var vertices = new List<RoentgenVertex>(count * count + (includeSkirts ? count * 4 : 0));
        for (int z = 0; z < count; z++)
        {
            for (int x = 0; x < count; x++)
            {
                int left = source.HeightAt(Math.Max(0, x - 1), z);
                int right = source.HeightAt(Math.Min(count - 1, x + 1), z);
                int back = source.HeightAt(x, Math.Max(0, z - 1));
                int forward = source.HeightAt(x, Math.Min(count - 1, z + 1));
                Vector3 normal = Vector3.Normalize(new Vector3(left - right, source.SpacingCentimeters * 2f, back - forward));
                vertices.Add(new RoentgenVertex(
                    new Vector3(x * spacing, WorldUnits.CentimetersToMeters(source.HeightAt(x, z)), z * spacing),
                    normal,
                    new Vector2(x, z) / 8f,
                    TerrainSurfacePalette.ColorFor(source.SurfaceAt(x, z))));
            }
        }

        var indices = new List<int>(BuildIndices(count));
        if (includeSkirts && skirtDepthCentimeters > 0)
        {
            float skirtDepthMeters = WorldUnits.CentimetersToMeters(skirtDepthCentimeters);
            AddSkirt(vertices, indices, Enumerable.Range(0, count).Select(static x => x).ToArray(), Vector3.Forward, skirtDepthMeters);
            AddSkirt(vertices, indices, Enumerable.Range(0, count).Select(z => z * count + count - 1).ToArray(), Vector3.Right, skirtDepthMeters);
            AddSkirt(vertices, indices, Enumerable.Range(0, count).Select(x => (count - 1) * count + count - 1 - x).ToArray(), Vector3.Backward, skirtDepthMeters);
            AddSkirt(vertices, indices, Enumerable.Range(0, count).Select(z => (count - 1 - z) * count).ToArray(), Vector3.Left, skirtDepthMeters);
        }

        return new MeshData($"terrain-{chunk.Address}-lod{lod}", vertices, indices);
    }

    public static MeshData CreateDistantMesh(RegionalTopology topology, int sampleStride = 2, int verticalOffsetCentimeters = -25)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (sampleStride < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleStride));
        }

        WorldGenerationProfile profile = topology.Profile;
        int sourceWidth = profile.TopologySamplesX;
        int sourceDepth = profile.TopologySamplesZ;
        int width = (sourceWidth - 1) / sampleStride + 1;
        int depth = (sourceDepth - 1) / sampleStride + 1;
        var evaluator = new TerrainHeightEvaluator(topology);
        var vertices = new RoentgenVertex[width * depth];
        int spacing = profile.TopologyCellCentimeters * sampleStride;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int worldX = profile.MinimumWorldXCentimeters + x * spacing;
                int worldZ = profile.MinimumWorldZCentimeters + z * spacing;
                int height = evaluator.SampleHeightCentimeters(worldX, worldZ) + verticalOffsetCentimeters;
                int left = evaluator.SampleHeightCentimeters(worldX - spacing, worldZ);
                int right = evaluator.SampleHeightCentimeters(worldX + spacing, worldZ);
                int back = evaluator.SampleHeightCentimeters(worldX, worldZ - spacing);
                int forward = evaluator.SampleHeightCentimeters(worldX, worldZ + spacing);
                Vector3 normal = Vector3.Normalize(new Vector3(left - right, spacing * 2f, back - forward));
                int slope = Math.Max(Math.Abs(right - left), Math.Abs(forward - back)) * 1000 / Math.Max(1, spacing * 2);
                TerrainSurfaceKind surface = evaluator.SampleSurface(worldX, worldZ, height, slope);
                vertices[z * width + x] = new RoentgenVertex(
                    new Vector3(WorldUnits.CentimetersToMeters(worldX), WorldUnits.CentimetersToMeters(height), WorldUnits.CentimetersToMeters(worldZ)),
                    normal,
                    new Vector2(x, z) / 8f,
                    TerrainSurfacePalette.ColorFor(surface));
            }
        }

        return new MeshData("phase6-distant-terrain", vertices, BuildIndices(width, depth));
    }

    public static long EstimateGpuUploadBytes(MeshData data) => checked((long)data.Vertices.Length * 36L + (long)data.Indices.Length * sizeof(int));

    private static void AddSkirt(
        ICollection<RoentgenVertex> vertices,
        ICollection<int> indices,
        IReadOnlyList<int> topEdge,
        Vector3 outwardNormal,
        float depthMeters)
    {
        if (topEdge.Count < 2)
        {
            return;
        }

        int bottomStart = vertices.Count;
        RoentgenVertex[] source = vertices.ToArray();
        foreach (int topIndex in topEdge)
        {
            RoentgenVertex top = source[topIndex];
            vertices.Add(new RoentgenVertex(
                top.Position - Vector3.Up * depthMeters,
                outwardNormal,
                top.TextureCoordinate,
                top.Color));
        }

        for (int index = 0; index < topEdge.Count - 1; index++)
        {
            int topA = topEdge[index];
            int topB = topEdge[index + 1];
            int bottomA = bottomStart + index;
            int bottomB = bottomA + 1;
            indices.Add(topA);
            indices.Add(topB);
            indices.Add(bottomB);
            indices.Add(topA);
            indices.Add(bottomB);
            indices.Add(bottomA);
        }
    }

    private static int[] BuildIndices(int sampleCount) => BuildIndices(sampleCount, sampleCount);

    private static int[] BuildIndices(int width, int depth)
    {
        var indices = new int[(width - 1) * (depth - 1) * 6];
        int write = 0;
        for (int z = 0; z < depth - 1; z++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int a = z * width + x;
                int b = a + 1;
                int c = a + width;
                int d = c + 1;
                indices[write++] = a;
                indices[write++] = d;
                indices[write++] = b;
                indices[write++] = a;
                indices[write++] = c;
                indices[write++] = d;
            }
        }

        return indices;
    }
}

public static class TerrainSurfacePalette
{
    public static Color ColorFor(TerrainSurfaceKind surface) => surface switch
    {
        TerrainSurfaceKind.DryEarth => new Color(150, 126, 87),
        TerrainSurfaceKind.FusedGlass => new Color(91, 104, 103),
        TerrainSurfaceKind.BrokenAsphalt => new Color(74, 76, 72),
        TerrainSurfaceKind.IndustrialMud => new Color(73, 82, 67),
        TerrainSurfaceKind.ShallowWater => new Color(53, 91, 91),
        TerrainSurfaceKind.PaleScrub => new Color(153, 151, 119),
        TerrainSurfaceKind.ExposedRock => new Color(121, 105, 91),
        TerrainSurfaceKind.ContaminatedSediment => new Color(92, 111, 66),
        TerrainSurfaceKind.Road => new Color(91, 86, 78),
        TerrainSurfaceKind.Trail => new Color(128, 111, 78),
        _ => Color.Magenta
    };
}
