using System.Numerics;

namespace Roentgen.Animation;

public readonly record struct SkinnedCharacterVertex(Vector3 Position, Vector3 Normal, Vector2 TextureCoordinate, Vector4 Color);

public static class CpuSkinner
{
    public static SkinnedCharacterVertex[] Skin(CharacterMeshProduct mesh, IReadOnlyList<Matrix4x4> skinMatrices)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(skinMatrices);
        var output = new SkinnedCharacterVertex[mesh.Vertices.Length];
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            CharacterVertex source = mesh.Vertices[index];
            Vector3 position = Vector3.Zero;
            Vector3 normal = Vector3.Zero;
            foreach ((byte bone, float weight) in source.Skin.Enumerate())
            {
                if (weight <= 0f)
                {
                    continue;
                }

                if (bone >= skinMatrices.Count)
                {
                    throw new InvalidDataException($"Vertex {index} references bone {bone} outside palette.");
                }

                Matrix4x4 matrix = skinMatrices[bone];
                position += Vector3.Transform(source.Position, matrix) * weight;
                normal += Vector3.TransformNormal(source.Normal, matrix) * weight;
            }

            if (normal.LengthSquared() < 0.000001f)
            {
                normal = Vector3.UnitY;
            }
            else
            {
                normal = Vector3.Normalize(normal);
            }

            output[index] = new SkinnedCharacterVertex(position, normal, source.TextureCoordinate, source.Color);
        }

        return output;
    }
}
