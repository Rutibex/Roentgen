using Microsoft.Xna.Framework;
using Roentgen.Animation;
using Roentgen.Contracts;

namespace Roentgen.Rendering;

public sealed record CharacterRenderItem(
    StableId CharacterId,
    CharacterGpuAsset Asset,
    PoseBuffer Pose,
    Matrix World,
    CharacterLodLevel Lod,
    bool Highlighted = false,
    bool CastsShadow = true,
    bool ReceivesShadow = true)
{
    public BoundingBox WorldBounds => BoundsTransform.Transform(Asset.SelectLod(Lod).Body.Skinned.LocalBounds, World);
}
