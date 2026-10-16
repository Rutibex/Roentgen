using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

public sealed record MaterialDefinition(
    string Id,
    Color Albedo,
    float Roughness = 0.75f,
    float Metallic = 0f,
    float EmissiveStrength = 0f,
    Texture2D? AlbedoTexture = null,
    bool DoubleSided = false)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        if (Roughness is < 0f or > 1f || Metallic is < 0f or > 1f || EmissiveStrength < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Roughness));
        }
    }
}
