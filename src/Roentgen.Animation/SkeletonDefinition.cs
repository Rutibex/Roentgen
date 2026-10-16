using System.Numerics;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Animation;

public readonly record struct BoneTransform(Vector3 Translation, Quaternion Rotation, Vector3 Scale)
{
    public static BoneTransform Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);

    public Matrix4x4 ToMatrix() =>
        Matrix4x4.CreateScale(Scale) *
        Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(Rotation)) *
        Matrix4x4.CreateTranslation(Translation);

    public static BoneTransform Blend(BoneTransform left, BoneTransform right, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new BoneTransform(
            Vector3.Lerp(left.Translation, right.Translation, amount),
            Quaternion.Slerp(left.Rotation, right.Rotation, amount),
            Vector3.Lerp(left.Scale, right.Scale, amount));
    }
}

public sealed record BoneDefinition(
    StableId Id,
    string Name,
    int ParentIndex,
    BoneTransform BindLocal,
    float MinimumPitchRadians = -3.1415927f,
    float MaximumPitchRadians = 3.1415927f,
    float MinimumYawRadians = -3.1415927f,
    float MaximumYawRadians = 3.1415927f,
    float MinimumRollRadians = -3.1415927f,
    float MaximumRollRadians = 3.1415927f);

public sealed class SkeletonDefinition : ICanonicalWritable
{
    private readonly Dictionary<string, int> _byName;

    public SkeletonDefinition(string skeletonId, IReadOnlyList<BoneDefinition> bones, IReadOnlyDictionary<string, int>? sockets = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skeletonId);
        ArgumentNullException.ThrowIfNull(bones);
        if (bones.Count == 0 || bones.Count > 48)
        {
            throw new ArgumentOutOfRangeException(nameof(bones), "A skeleton must contain between 1 and 48 bones.");
        }

        SkeletonId = skeletonId;
        Bones = bones.ToArray();
        _byName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < Bones.Count; index++)
        {
            BoneDefinition bone = Bones[index];
            if (string.IsNullOrWhiteSpace(bone.Name) || !_byName.TryAdd(bone.Name, index))
            {
                throw new ArgumentException($"Bone name '{bone.Name}' is empty or duplicated.", nameof(bones));
            }

            if (bone.ParentIndex >= index || bone.ParentIndex < -1)
            {
                throw new ArgumentException($"Bone '{bone.Name}' has invalid parent index {bone.ParentIndex}.", nameof(bones));
            }

            ValidateTransform(bone.BindLocal, bone.Name);
        }

        if (Bones.Count(static bone => bone.ParentIndex == -1) != 1)
        {
            throw new ArgumentException("A skeleton must contain exactly one root bone.", nameof(bones));
        }

        Sockets = sockets is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(sockets, StringComparer.Ordinal);
        foreach ((string name, int boneIndex) in Sockets)
        {
            if (string.IsNullOrWhiteSpace(name) || boneIndex < 0 || boneIndex >= Bones.Count)
            {
                throw new ArgumentException($"Socket '{name}' references invalid bone index {boneIndex}.", nameof(sockets));
            }
        }

        BindWorldMatrices = BuildWorldMatrices(Bones.Select(static value => value.BindLocal).ToArray());
        InverseBindWorldMatrices = BindWorldMatrices.Select(static matrix =>
        {
            if (!Matrix4x4.Invert(matrix, out Matrix4x4 inverse))
            {
                throw new InvalidDataException("A bind pose matrix is not invertible.");
            }

            return inverse;
        }).ToArray();
    }

    public string SkeletonId { get; }
    public IReadOnlyList<BoneDefinition> Bones { get; }
    public IReadOnlyDictionary<string, int> Sockets { get; }
    public IReadOnlyList<Matrix4x4> BindWorldMatrices { get; }
    public IReadOnlyList<Matrix4x4> InverseBindWorldMatrices { get; }

    public int IndexOf(string boneName) => _byName.TryGetValue(boneName, out int index) ? index : -1;

    public Matrix4x4[] BuildWorldMatrices(IReadOnlyList<BoneTransform> localPose)
    {
        if (localPose.Count != Bones.Count)
        {
            throw new ArgumentException("Pose bone count does not match skeleton.", nameof(localPose));
        }

        var world = new Matrix4x4[Bones.Count];
        for (int index = 0; index < Bones.Count; index++)
        {
            Matrix4x4 local = localPose[index].ToMatrix();
            int parent = Bones[index].ParentIndex;
            world[index] = parent < 0 ? local : local * world[parent];
        }

        return world;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteString(SkeletonId);
        writer.WriteCount(Bones.Count);
        foreach (BoneDefinition bone in Bones)
        {
            writer.WriteStableId(bone.Id);
            writer.WriteString(bone.Name);
            writer.WriteInt32(bone.ParentIndex);
            WriteTransform(writer, bone.BindLocal);
        }

        writer.WriteCount(Sockets.Count);
        foreach ((string name, int index) in Sockets.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteString(name);
            writer.WriteInt32(index);
        }
    }

    private static void ValidateTransform(BoneTransform transform, string name)
    {
        if (!IsFinite(transform.Translation) || !IsFinite(transform.Scale) ||
            !float.IsFinite(transform.Rotation.X) || !float.IsFinite(transform.Rotation.Y) ||
            !float.IsFinite(transform.Rotation.Z) || !float.IsFinite(transform.Rotation.W))
        {
            throw new ArgumentException($"Bone '{name}' contains a non-finite bind transform.");
        }

        if (transform.Scale.X <= 0f || transform.Scale.Y <= 0f || transform.Scale.Z <= 0f)
        {
            throw new ArgumentException($"Bone '{name}' contains a non-positive scale.");
        }
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void WriteTransform(CanonicalWriter writer, BoneTransform transform)
    {
        writer.WriteSingle(transform.Translation.X);
        writer.WriteSingle(transform.Translation.Y);
        writer.WriteSingle(transform.Translation.Z);
        writer.WriteSingle(transform.Rotation.X);
        writer.WriteSingle(transform.Rotation.Y);
        writer.WriteSingle(transform.Rotation.Z);
        writer.WriteSingle(transform.Rotation.W);
        writer.WriteSingle(transform.Scale.X);
        writer.WriteSingle(transform.Scale.Y);
        writer.WriteSingle(transform.Scale.Z);
    }
}
