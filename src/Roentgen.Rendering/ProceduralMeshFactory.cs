using Microsoft.Xna.Framework;

namespace Roentgen.Rendering;

public static class ProceduralMeshFactory
{
    public static MeshData CreateBox(string name, Vector3 size, Color color)
    {
        if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Vector3 h = size * 0.5f;
        var vertices = new List<RoentgenVertex>(24);
        var indices = new List<int>(36);
        AddFace(vertices, indices, new(-h.X, -h.Y, h.Z), new(h.X, -h.Y, h.Z), new(h.X, h.Y, h.Z), new(-h.X, h.Y, h.Z), Vector3.Forward, color);
        AddFace(vertices, indices, new(h.X, -h.Y, -h.Z), new(-h.X, -h.Y, -h.Z), new(-h.X, h.Y, -h.Z), new(h.X, h.Y, -h.Z), Vector3.Backward, color);
        AddFace(vertices, indices, new(-h.X, -h.Y, -h.Z), new(-h.X, -h.Y, h.Z), new(-h.X, h.Y, h.Z), new(-h.X, h.Y, -h.Z), Vector3.Left, color);
        AddFace(vertices, indices, new(h.X, -h.Y, h.Z), new(h.X, -h.Y, -h.Z), new(h.X, h.Y, -h.Z), new(h.X, h.Y, h.Z), Vector3.Right, color);
        AddFace(vertices, indices, new(-h.X, h.Y, h.Z), new(h.X, h.Y, h.Z), new(h.X, h.Y, -h.Z), new(-h.X, h.Y, -h.Z), Vector3.Up, color);
        AddFace(vertices, indices, new(-h.X, -h.Y, -h.Z), new(h.X, -h.Y, -h.Z), new(h.X, -h.Y, h.Z), new(-h.X, -h.Y, h.Z), Vector3.Down, color);
        return new MeshData(name, vertices, indices);
    }

    public static MeshData CreateTerrainGrid(string name, int cellsX, int cellsZ, float cellSize, Func<int, int, float> height, Func<int, int, Color>? color = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(height);
        if (cellsX < 1 || cellsZ < 1 || cellSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellsX));
        }

        int width = cellsX + 1;
        int depth = cellsZ + 1;
        float[,] heights = new float[width, depth];
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                heights[x, z] = height(x, z);
            }
        }

        var vertices = new RoentgenVertex[width * depth];
        float originX = cellsX * cellSize * -0.5f;
        float originZ = cellsZ * cellSize * -0.5f;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float left = heights[Math.Max(0, x - 1), z];
                float right = heights[Math.Min(width - 1, x + 1), z];
                float back = heights[x, Math.Max(0, z - 1)];
                float forward = heights[x, Math.Min(depth - 1, z + 1)];
                Vector3 normal = Vector3.Normalize(new Vector3(left - right, 2f * cellSize, back - forward));
                vertices[z * width + x] = new RoentgenVertex(
                    new Vector3(originX + x * cellSize, heights[x, z], originZ + z * cellSize),
                    normal,
                    new Vector2(x, z) / 8f,
                    color?.Invoke(x, z) ?? new Color(142, 126, 96));
            }
        }

        var indices = new int[cellsX * cellsZ * 6];
        int write = 0;
        for (int z = 0; z < cellsZ; z++)
        {
            for (int x = 0; x < cellsX; x++)
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

        return new MeshData(name, vertices, indices);
    }

    public static MeshData CreateCylinder(string name, float radius, float height, int sides, Color color)
    {
        if (radius <= 0f || height <= 0f || sides < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        var vertices = new List<RoentgenVertex>(sides * 4 + 2);
        var indices = new List<int>(sides * 12);
        float half = height * 0.5f;
        for (int side = 0; side < sides; side++)
        {
            float angle = MathHelper.TwoPi * side / sides;
            float nextAngle = MathHelper.TwoPi * (side + 1) / sides;
            Vector3 n0 = new(MathF.Sin(angle), 0f, MathF.Cos(angle));
            Vector3 n1 = new(MathF.Sin(nextAngle), 0f, MathF.Cos(nextAngle));
            int start = vertices.Count;
            vertices.Add(new RoentgenVertex(n0 * radius + Vector3.Down * half, n0, new Vector2((float)side / sides, 1f), color));
            vertices.Add(new RoentgenVertex(n1 * radius + Vector3.Down * half, n1, new Vector2((float)(side + 1) / sides, 1f), color));
            vertices.Add(new RoentgenVertex(n1 * radius + Vector3.Up * half, n1, new Vector2((float)(side + 1) / sides, 0f), color));
            vertices.Add(new RoentgenVertex(n0 * radius + Vector3.Up * half, n0, new Vector2((float)side / sides, 0f), color));
            indices.AddRange([start, start + 2, start + 1, start, start + 3, start + 2]);
        }

        AddCap(vertices, indices, radius, half, sides, Vector3.Up, color);
        AddCap(vertices, indices, radius, -half, sides, Vector3.Down, color);
        return new MeshData(name, vertices, indices);
    }

    private static void AddFace(List<RoentgenVertex> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
    {
        int start = vertices.Count;
        vertices.Add(new RoentgenVertex(a, normal, new Vector2(0f, 1f), color));
        vertices.Add(new RoentgenVertex(b, normal, new Vector2(1f, 1f), color));
        vertices.Add(new RoentgenVertex(c, normal, new Vector2(1f, 0f), color));
        vertices.Add(new RoentgenVertex(d, normal, new Vector2(0f, 0f), color));
        indices.AddRange([start, start + 2, start + 1, start, start + 3, start + 2]);
    }

    private static void AddCap(List<RoentgenVertex> vertices, List<int> indices, float radius, float y, int sides, Vector3 normal, Color color)
    {
        int center = vertices.Count;
        vertices.Add(new RoentgenVertex(new Vector3(0f, y, 0f), normal, new Vector2(0.5f), color));
        int ring = vertices.Count;
        for (int side = 0; side < sides; side++)
        {
            float angle = MathHelper.TwoPi * side / sides;
            Vector3 point = new(MathF.Sin(angle) * radius, y, MathF.Cos(angle) * radius);
            vertices.Add(new RoentgenVertex(point, normal, new Vector2(point.X / radius * 0.5f + 0.5f, point.Z / radius * 0.5f + 0.5f), color));
        }

        for (int side = 0; side < sides; side++)
        {
            int current = ring + side;
            int next = ring + ((side + 1) % sides);
            if (normal.Y > 0f)
            {
                indices.AddRange([center, current, next]);
            }
            else
            {
                indices.AddRange([center, next, current]);
            }
        }
    }
}
