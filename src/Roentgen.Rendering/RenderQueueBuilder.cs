using Microsoft.Xna.Framework;

namespace Roentgen.Rendering;

public sealed class RenderQueueBuilder
{
    public RenderQueueResult Build(RenderFrameSnapshot snapshot, float aspectRatio, bool frustumCulling)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        CameraMatrices camera = snapshot.Camera.ToMatrices(aspectRatio);
        var visible = new List<RenderItem>(snapshot.OpaqueItems.Count);
        int culled = 0;
        foreach (RenderItem item in snapshot.OpaqueItems)
        {
            if (frustumCulling && camera.Frustum.Contains(item.WorldBounds) == ContainmentType.Disjoint)
            {
                culled++;
                continue;
            }

            visible.Add(item);
        }

        visible.Sort(static (left, right) =>
        {
            int material = string.CompareOrdinal(left.Material.Id, right.Material.Id);
            if (material != 0) return material;
            int mesh = string.CompareOrdinal(left.Mesh.Name, right.Mesh.Name);
            if (mesh != 0) return mesh;
            return string.CompareOrdinal(left.Id, right.Id);
        });
        return new RenderQueueResult(camera, visible, culled);
    }
}

public sealed record RenderQueueResult(CameraMatrices Camera, IReadOnlyList<RenderItem> VisibleItems, int CulledItemCount);
