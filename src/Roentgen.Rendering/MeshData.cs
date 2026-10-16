using Microsoft.Xna.Framework;

namespace Roentgen.Rendering;

public sealed class MeshData
{
    public MeshData(string name, IReadOnlyList<RoentgenVertex> vertices, IReadOnlyList<int> indices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        if (vertices.Count < 3)
        {
            throw new ArgumentException("A mesh requires at least three vertices.", nameof(vertices));
        }

        if (indices.Count == 0 || indices.Count % 3 != 0)
        {
            throw new ArgumentException("Triangle-list indices must be a non-empty multiple of three.", nameof(indices));
        }

        for (int index = 0; index < indices.Count; index++)
        {
            if (indices[index] < 0 || indices[index] >= vertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(indices), $"Index {indices[index]} at position {index} is outside vertex range.");
            }
        }

        for (int index = 0; index < vertices.Count; index++)
        {
            RoentgenVertex vertex = vertices[index];
            if (!IsFinite(vertex.Position) || !IsFinite(vertex.Normal) || !float.IsFinite(vertex.TextureCoordinate.X) || !float.IsFinite(vertex.TextureCoordinate.Y))
            {
                throw new ArgumentException($"Vertex {index} contains a non-finite value.", nameof(vertices));
            }

            if (vertex.Normal.LengthSquared() < 0.000001f)
            {
                throw new ArgumentException($"Vertex {index} contains a zero normal.", nameof(vertices));
            }
        }

        Name = name;
        Vertices = vertices.ToArray();
        Indices = indices.ToArray();
        LocalBounds = CalculateBounds(Vertices);
    }

    public string Name { get; }
    public RoentgenVertex[] Vertices { get; }
    public int[] Indices { get; }
    public BoundingBox LocalBounds { get; }
    public int PrimitiveCount => Indices.Length / 3;

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static BoundingBox CalculateBounds(IReadOnlyList<RoentgenVertex> vertices)
    {
        Vector3 minimum = vertices[0].Position;
        Vector3 maximum = vertices[0].Position;
        for (int index = 1; index < vertices.Count; index++)
        {
            minimum = Vector3.Min(minimum, vertices[index].Position);
            maximum = Vector3.Max(maximum, vertices[index].Position);
        }

        return new BoundingBox(minimum, maximum);
    }
}
