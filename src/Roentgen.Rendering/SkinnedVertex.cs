using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

public readonly struct SkinnedVertex : IVertexType
{
    public SkinnedVertex(
        Vector3 position,
        Vector3 normal,
        Vector2 textureCoordinate,
        Color color,
        Vector4 blendIndices,
        Vector4 blendWeights)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
        Color = color;
        BlendIndices = blendIndices;
        BlendWeights = blendWeights;
    }

    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    public Vector2 TextureCoordinate { get; }
    public Color Color { get; }
    public Vector4 BlendIndices { get; }
    public Vector4 BlendWeights { get; }

    public static VertexDeclaration VertexDeclaration { get; } = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Color, VertexElementUsage.Color, 0),
        new VertexElement(36, VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(52, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0));

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
