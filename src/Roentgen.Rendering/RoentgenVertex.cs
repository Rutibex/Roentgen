using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct RoentgenVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    public RoentgenVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate, Color color)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
        Color = color;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector2 TextureCoordinate;
    public readonly Color Color;
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
