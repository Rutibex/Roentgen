using Microsoft.Xna.Framework;

namespace Roentgen.Rendering;

public readonly record struct DirectionalLight(Vector3 Direction, Color Color, float Intensity)
{
    public DirectionalLight Normalized()
    {
        Vector3 direction = Direction;
        if (direction.LengthSquared() < 0.000001f)
        {
            direction = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.25f));
        }
        else
        {
            direction.Normalize();
        }

        return this with { Direction = direction, Intensity = Math.Max(0f, Intensity) };
    }
}

public readonly record struct FogSettings(Color Color, float Start, float End)
{
    public void Validate()
    {
        if (Start < 0f || End <= Start)
        {
            throw new ArgumentOutOfRangeException(nameof(End));
        }
    }
}
