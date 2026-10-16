using System.Numerics;

namespace Roentgen.Animation;

public sealed class PoseBuffer
{
    private readonly BoneTransform[] _local;

    public PoseBuffer(SkeletonDefinition skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        Skeleton = skeleton;
        _local = skeleton.Bones.Select(static bone => bone.BindLocal).ToArray();
    }

    private PoseBuffer(SkeletonDefinition skeleton, BoneTransform[] local)
    {
        Skeleton = skeleton;
        _local = local;
    }

    public SkeletonDefinition Skeleton { get; }
    public int Count => _local.Length;
    public BoneTransform this[int index]
    {
        get => _local[index];
        set => _local[index] = value;
    }

    public PoseBuffer Clone() => new(Skeleton, _local.ToArray());

    public void ResetToBindPose()
    {
        for (int index = 0; index < _local.Length; index++)
        {
            _local[index] = Skeleton.Bones[index].BindLocal;
        }
    }

    public IReadOnlyList<BoneTransform> LocalTransforms => _local;

    public Matrix4x4[] BuildWorldMatrices() => Skeleton.BuildWorldMatrices(_local);

    public Matrix4x4[] BuildSkinMatrices()
    {
        Matrix4x4[] world = BuildWorldMatrices();
        var skin = new Matrix4x4[world.Length];
        for (int index = 0; index < skin.Length; index++)
        {
            skin[index] = Skeleton.InverseBindWorldMatrices[index] * world[index];
        }

        return skin;
    }

    public static PoseBuffer Blend(PoseBuffer left, PoseBuffer right, float amount)
    {
        if (!ReferenceEquals(left.Skeleton, right.Skeleton) && left.Skeleton.SkeletonId != right.Skeleton.SkeletonId)
        {
            throw new ArgumentException("Cannot blend poses from different skeletons.");
        }

        var result = new PoseBuffer(left.Skeleton);
        for (int index = 0; index < result.Count; index++)
        {
            result[index] = BoneTransform.Blend(left[index], right[index], amount);
        }

        return result;
    }

    public void ApplyAdditive(int boneIndex, Quaternion rotation, Vector3 translation, float weight)
    {
        if (boneIndex < 0 || boneIndex >= Count)
        {
            return;
        }

        weight = Math.Clamp(weight, 0f, 1f);
        BoneTransform current = _local[boneIndex];
        Quaternion additive = Quaternion.Slerp(Quaternion.Identity, Quaternion.Normalize(rotation), weight);
        _local[boneIndex] = current with
        {
            Translation = current.Translation + translation * weight,
            Rotation = Quaternion.Normalize(current.Rotation * additive)
        };
    }
}
