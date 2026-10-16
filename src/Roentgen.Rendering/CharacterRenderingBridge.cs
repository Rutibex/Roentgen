using Microsoft.Xna.Framework;
using Roentgen.Animation;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsVector4 = System.Numerics.Vector4;

namespace Roentgen.Rendering;

public static class CharacterRenderingBridge
{
    public static SkinnedVertex[] ToSkinnedVertices(CharacterMeshProduct mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var result = new SkinnedVertex[mesh.Vertices.Length];
        for (int index = 0; index < result.Length; index++)
        {
            CharacterVertex source = mesh.Vertices[index];
            result[index] = new SkinnedVertex(
                ToXna(source.Position),
                ToXna(source.Normal),
                ToXna(source.TextureCoordinate),
                ToColor(source.Color),
                new Vector4(source.Skin.Bone0, source.Skin.Bone1, source.Skin.Bone2, source.Skin.Bone3),
                ToXna(source.Skin.Weights));
        }

        return result;
    }

    public static Matrix[] ToXnaMatrices(IReadOnlyList<NumericsMatrix> matrices)
    {
        var result = new Matrix[matrices.Count];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = ToXna(matrices[index]);
        }

        return result;
    }

    public static Matrix ToXna(NumericsMatrix value) => new(
        value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44);

    public static Vector2 ToXna(NumericsVector2 value) => new(value.X, value.Y);
    public static Vector3 ToXna(NumericsVector3 value) => new(value.X, value.Y, value.Z);
    public static Vector4 ToXna(NumericsVector4 value) => new(value.X, value.Y, value.Z, value.W);

    public static Color ToColor(NumericsVector4 value) => new(
        Math.Clamp(value.X, 0f, 1f),
        Math.Clamp(value.Y, 0f, 1f),
        Math.Clamp(value.Z, 0f, 1f),
        Math.Clamp(value.W, 0f, 1f));

    public static BoundingBox ToBoundingBox(CharacterBounds bounds) => new(ToXna(bounds.Minimum), ToXna(bounds.Maximum));
}
