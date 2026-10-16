using Microsoft.Xna.Framework;

namespace Roentgen.Rendering;

public sealed record RenderItem(
    string Id,
    GpuMesh Mesh,
    MaterialDefinition Material,
    Matrix World,
    bool CastsShadow = true,
    bool ReceivesShadow = true)
{
    public BoundingBox WorldBounds => BoundsTransform.Transform(Mesh.LocalBounds, World);
}

internal static class BoundsTransform
{
    public static BoundingBox Transform(BoundingBox bounds, Matrix transform)
    {
        Vector3[] corners = bounds.GetCorners();
        Vector3 minimum = Vector3.Transform(corners[0], transform);
        Vector3 maximum = minimum;
        for (int index = 1; index < corners.Length; index++)
        {
            Vector3 transformed = Vector3.Transform(corners[index], transform);
            minimum = Vector3.Min(minimum, transformed);
            maximum = Vector3.Max(maximum, transformed);
        }

        return new BoundingBox(minimum, maximum);
    }
}
