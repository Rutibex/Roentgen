using System.Numerics;
using Roentgen.Animation;

namespace Roentgen.Generation;

internal sealed class ProceduralCharacterMeshBuilder
{
    private readonly List<CharacterVertex> _vertices = [];
    private readonly List<int> _indices = [];

    public void AddTaperedSegment(
        Vector3 start,
        Vector3 end,
        float startRadius,
        float endRadius,
        int startBone,
        int endBone,
        Vector4 color,
        int sides,
        int rings = 4)
    {
        sides = Math.Max(3, sides);
        rings = Math.Max(2, rings);
        Vector3 direction = end - start;
        float length = direction.Length();
        if (length < 0.0001f)
        {
            return;
        }

        Vector3 axis = direction / length;
        Vector3 reference = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) > 0.92f ? Vector3.UnitX : Vector3.UnitY;
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(axis, reference));
        Vector3 bitangent = Vector3.Normalize(Vector3.Cross(axis, tangent));
        int baseIndex = _vertices.Count;
        for (int ring = 0; ring < rings; ring++)
        {
            float t = ring / (float)(rings - 1);
            Vector3 center = Vector3.Lerp(start, end, t);
            float radius = startRadius + (endRadius - startRadius) * t;
            SkinInfluences skin = new(
                checked((byte)startBone),
                checked((byte)endBone),
                0,
                0,
                new Vector4(1f - t, t, 0f, 0f));
            for (int side = 0; side < sides; side++)
            {
                float angle = MathF.Tau * side / sides;
                Vector3 radial = tangent * MathF.Cos(angle) + bitangent * MathF.Sin(angle);
                _vertices.Add(new CharacterVertex(
                    center + radial * radius,
                    radial,
                    new Vector2(side / (float)sides, t),
                    color,
                    skin));
            }
        }

        for (int ring = 0; ring < rings - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                int nextSide = (side + 1) % sides;
                int a = baseIndex + ring * sides + side;
                int b = baseIndex + ring * sides + nextSide;
                int c = baseIndex + (ring + 1) * sides + side;
                int d = baseIndex + (ring + 1) * sides + nextSide;
                AddQuad(a, b, d, c);
            }
        }

        AddCap(baseIndex, start, -axis, startRadius, tangent, bitangent, sides, startBone, color, reverse: true);
        int endRingStart = baseIndex + (rings - 1) * sides;
        AddCap(endRingStart, end, axis, endRadius, tangent, bitangent, sides, endBone, color, reverse: false);
    }

    public void AddEllipsoid(Vector3 center, Vector3 radii, int boneIndex, Vector4 color, int longitudeSegments, int latitudeSegments)
    {
        longitudeSegments = Math.Max(4, longitudeSegments);
        latitudeSegments = Math.Max(3, latitudeSegments);
        int baseIndex = _vertices.Count;
        for (int latitude = 0; latitude <= latitudeSegments; latitude++)
        {
            float v = latitude / (float)latitudeSegments;
            float phi = MathF.PI * v;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);
            for (int longitude = 0; longitude <= longitudeSegments; longitude++)
            {
                float u = longitude / (float)longitudeSegments;
                float theta = MathF.Tau * u;
                Vector3 unit = new(MathF.Cos(theta) * sinPhi, cosPhi, MathF.Sin(theta) * sinPhi);
                Vector3 position = center + new Vector3(unit.X * radii.X, unit.Y * radii.Y, unit.Z * radii.Z);
                Vector3 normal = Vector3.Normalize(new Vector3(
                    unit.X / MathF.Max(0.0001f, radii.X),
                    unit.Y / MathF.Max(0.0001f, radii.Y),
                    unit.Z / MathF.Max(0.0001f, radii.Z)));
                _vertices.Add(new CharacterVertex(
                    position,
                    normal,
                    new Vector2(u, v),
                    color,
                    new SkinInfluences(checked((byte)boneIndex), 0, 0, 0, Vector4.UnitX)));
            }
        }

        int row = longitudeSegments + 1;
        for (int latitude = 0; latitude < latitudeSegments; latitude++)
        {
            for (int longitude = 0; longitude < longitudeSegments; longitude++)
            {
                int a = baseIndex + latitude * row + longitude;
                int b = a + 1;
                int c = a + row;
                int d = c + 1;
                AddQuad(a, b, d, c);
            }
        }
    }

    public void AddBox(Vector3 center, Vector3 halfExtents, int boneIndex, Vector4 color)
    {
        Vector3[] corners =
        [
            center + new Vector3(-halfExtents.X, -halfExtents.Y, -halfExtents.Z),
            center + new Vector3( halfExtents.X, -halfExtents.Y, -halfExtents.Z),
            center + new Vector3( halfExtents.X,  halfExtents.Y, -halfExtents.Z),
            center + new Vector3(-halfExtents.X,  halfExtents.Y, -halfExtents.Z),
            center + new Vector3(-halfExtents.X, -halfExtents.Y,  halfExtents.Z),
            center + new Vector3( halfExtents.X, -halfExtents.Y,  halfExtents.Z),
            center + new Vector3( halfExtents.X,  halfExtents.Y,  halfExtents.Z),
            center + new Vector3(-halfExtents.X,  halfExtents.Y,  halfExtents.Z)
        ];
        int[,] faces =
        {
            { 0, 1, 2, 3 },
            { 5, 4, 7, 6 },
            { 4, 0, 3, 7 },
            { 1, 5, 6, 2 },
            { 3, 2, 6, 7 },
            { 4, 5, 1, 0 }
        };
        Vector3[] normals = [Vector3.UnitZ * -1f, Vector3.UnitZ, Vector3.UnitX * -1f, Vector3.UnitX, Vector3.UnitY, Vector3.UnitY * -1f];
        for (int face = 0; face < 6; face++)
        {
            int start = _vertices.Count;
            for (int corner = 0; corner < 4; corner++)
            {
                _vertices.Add(new CharacterVertex(
                    corners[faces[face, corner]],
                    normals[face],
                    corner switch
                    {
                        0 => Vector2.Zero,
                        1 => Vector2.UnitX,
                        2 => Vector2.One,
                        _ => Vector2.UnitY
                    },
                    color,
                    new SkinInfluences(checked((byte)boneIndex), 0, 0, 0, Vector4.UnitX)));
            }

            AddQuad(start, start + 1, start + 2, start + 3);
        }
    }

    public CharacterMeshProduct Build(string name) => new(name, _vertices, _indices);

    private void AddCap(int ringStart, Vector3 center, Vector3 normal, float radius, Vector3 tangent, Vector3 bitangent, int sides, int bone, Vector4 color, bool reverse)
    {
        int centerIndex = _vertices.Count;
        _vertices.Add(new CharacterVertex(
            center,
            normal,
            new Vector2(0.5f, 0.5f),
            color,
            new SkinInfluences(checked((byte)bone), 0, 0, 0, Vector4.UnitX)));
        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            if (reverse)
            {
                _indices.Add(centerIndex);
                _indices.Add(ringStart + next);
                _indices.Add(ringStart + side);
            }
            else
            {
                _indices.Add(centerIndex);
                _indices.Add(ringStart + side);
                _indices.Add(ringStart + next);
            }
        }

        _ = radius;
        _ = tangent;
        _ = bitangent;
    }

    private void AddQuad(int a, int b, int c, int d)
    {
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
        _indices.Add(a);
        _indices.Add(c);
        _indices.Add(d);
    }
}
