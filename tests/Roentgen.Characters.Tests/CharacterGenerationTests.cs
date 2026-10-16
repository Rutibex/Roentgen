using System.Numerics;
using Roentgen.AI;
using Roentgen.Animation;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Generation;

namespace Roentgen.Characters.Tests;

public sealed class CharacterGenerationTests
{
    private static readonly CampaignSeed Seed = new(0x524F454E5447454EUL);

    [Fact]
    public void SameSeedProducesSameCharacterChecksum()
    {
        StableId id = StableIdFactory.Create("tests", "same-character");
        CharacterGenerationRequest request = new(Seed, id, "test-human");
        GeneratedCharacterProduct first = HumanoidCharacterGenerator.Generate(request);
        GeneratedCharacterProduct second = HumanoidCharacterGenerator.Generate(request);
        Assert.Equal(Checksum(first), Checksum(second));
    }

    [Fact]
    public void HumanoidContainsRequiredSkeletonAndTargetParts()
    {
        StableId id = StableIdFactory.Create("tests", "required-bones");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        Assert.InRange(product.Skeleton.Bones.Count, 20, 48);
        Assert.True(product.Skeleton.IndexOf("head") >= 0);
        Assert.True(product.Skeleton.IndexOf("hand_l") >= 0);
        Assert.True(product.Skeleton.IndexOf("foot_r") >= 0);
        Assert.Contains(product.TargetParts, static part => part.Region == AnatomicalRegion.Head);
        Assert.Contains(product.TargetParts, static part => part.Region == AnatomicalRegion.Torso);
    }

    [Fact]
    public void HumanoidFacialLandmarksUseYUpOrientation()
    {
        StableId id = StableIdFactory.Create("tests", "facial-landmark-orientation");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(
            Seed,
            id,
            "test-human",
            HeightMillimetersOverride: 1780,
            BuildPermilleOverride: 1000));

        int headIndex = product.Skeleton.IndexOf("head");
        int leftEyeIndex = product.Skeleton.IndexOf("eye_l");
        int rightEyeIndex = product.Skeleton.IndexOf("eye_r");
        Assert.True(headIndex >= 0 && leftEyeIndex >= 0 && rightEyeIndex >= 0);

        Vector3 head = Translation(product.Skeleton.BindWorldMatrices[headIndex]);
        Vector3 leftEye = Translation(product.Skeleton.BindWorldMatrices[leftEyeIndex]);
        Vector3 rightEye = Translation(product.Skeleton.BindWorldMatrices[rightEyeIndex]);
        Assert.True(leftEye.Y > head.Y + 0.07f);
        Assert.True(rightEye.Y > head.Y + 0.07f);

        CharacterMeshProduct body = product.Lods[0].Body;
        CharacterVertex[] eyeVertices = body.Vertices
            .Where(vertex =>
                (vertex.Skin.Bone0 == leftEyeIndex || vertex.Skin.Bone0 == rightEyeIndex) &&
                vertex.Skin.Weights.X > 0.99f)
            .ToArray();
        Assert.NotEmpty(eyeVertices);

        CharacterVertex[] noseTipVertices = body.Vertices
            .Where(vertex =>
                vertex.Skin.Bone0 == headIndex &&
                vertex.Skin.Weights.X > 0.99f &&
                vertex.Position.Z < head.Z - 0.12f)
            .ToArray();
        Assert.NotEmpty(noseTipVertices);

        float eyeHeight = eyeVertices.Average(static vertex => vertex.Position.Y);
        float noseHeight = noseTipVertices.Average(static vertex => vertex.Position.Y);
        Assert.True(eyeHeight > noseHeight, $"Expected eyes above nose in Y-up coordinates, but eye Y={eyeHeight:F3} and nose Y={noseHeight:F3}.");
    }

    [Fact]
    public void AllGeneratedWeightsAreNormalizedAndValid()
    {
        StableId id = StableIdFactory.Create("tests", "weights");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        foreach (CharacterLodProduct lod in product.Lods)
        {
            ValidateMesh(lod.Body, product.Skeleton.Bones.Count);
            if (lod.Clothing is not null)
            {
                ValidateMesh(lod.Clothing, product.Skeleton.Bones.Count);
            }
        }
    }


    [Fact]
    public void HumanoidGeneratesFirstPersonArmsAndEquipmentProfiles()
    {
        StableId id = StableIdFactory.Create("tests", "presentation-products");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        Assert.NotNull(product.FirstPersonArms);
        Assert.NotEmpty(product.FirstPersonArms!.Vertices);
        Assert.Contains("unarmed", product.EquipmentProfiles.Keys);
        Assert.Contains("sidearm", product.EquipmentProfiles.Keys);
        Assert.Contains("rifle", product.EquipmentProfiles.Keys);
        Assert.Contains("melee-one-handed", product.EquipmentProfiles.Keys);
        Assert.Contains("tool", product.EquipmentProfiles.Keys);
    }

    [Fact]
    public void TaskRunnerAppliesActionEquipmentProfile()
    {
        StableId id = StableIdFactory.Create("tests", "task-equipment");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        var state = new CharacterState(id, product.Appearance, FixedVector3.Zero);
        var runner = new CharacterTaskRunner();
        runner.Register(state, new CharacterTaskSequence(
        [
            new CharacterTask(CharacterTaskKind.Aim, new FixedVector3(0, 0, -1000), StableId.Empty, 1, "rifle"),
            new CharacterTask(CharacterTaskKind.MeleeDemonstration, new FixedVector3(0, 0, -1000), StableId.Empty, 1, "melee-one-handed")
        ]));
        runner.Tick();
        Assert.Equal("rifle", state.EquipmentPoseProfileId);
        runner.Tick();
        Assert.Equal("melee-one-handed", state.EquipmentPoseProfileId);
    }

    [Fact]
    public void PoseRuntimeProducesFiniteTransformsForAllActions()
    {
        StableId id = StableIdFactory.Create("tests", "poses");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        var runtime = new CharacterPoseRuntime();
        foreach (CharacterActionKind action in Enum.GetValues<CharacterActionKind>())
        {
            PoseBuffer pose = runtime.Evaluate(product, new CharacterPoseInput(
                action,
                500,
                375,
                new Vector3(2f, 0f, 1f),
                0,
                -Vector3.UnitZ,
                Vector3.Normalize(new Vector3(0.2f, 0.1f, -1f)),
                CharacterInjuryPresentation.None,
                "rifle",
                CharacterLodLevel.Full,
                LeftGroundNormal: Vector3.UnitY,
                RightGroundNormal: Vector3.UnitY));
            foreach (BoneTransform transform in pose.LocalTransforms)
            {
                Assert.True(IsFinite(transform.Translation));
                Assert.True(IsFinite(transform.Scale));
                Assert.True(float.IsFinite(transform.Rotation.X));
                Assert.True(float.IsFinite(transform.Rotation.Y));
                Assert.True(float.IsFinite(transform.Rotation.Z));
                Assert.True(float.IsFinite(transform.Rotation.W));
            }
        }
    }

    [Fact]
    public void CpuSkinningPreservesVertexCount()
    {
        StableId id = StableIdFactory.Create("tests", "cpu-skinning");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        CharacterMeshProduct mesh = product.Lods[0].Body;
        var pose = new PoseBuffer(product.Skeleton);
        SkinnedCharacterVertex[] result = CpuSkinner.Skin(mesh, pose.BuildSkinMatrices());
        Assert.Equal(mesh.Vertices.Length, result.Length);
        Assert.All(result, static vertex => Assert.True(IsFinite(vertex.Position) && IsFinite(vertex.Normal)));
    }

    [Fact]
    public void QuadrupedBodyPlanContainsFourSupportLimbs()
    {
        StableId id = StableIdFactory.Create("tests", "quadruped");
        GeneratedCharacterProduct product = QuadrupedCreatureGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-quadruped", CharacterSpecies.IndustrialQuadruped));
        Assert.True(product.Skeleton.IndexOf("front_l_foot") >= 0);
        Assert.True(product.Skeleton.IndexOf("front_r_foot") >= 0);
        Assert.True(product.Skeleton.IndexOf("rear_l_foot") >= 0);
        Assert.True(product.Skeleton.IndexOf("rear_r_foot") >= 0);
        Assert.Contains(product.TargetParts, static part => part.Region == AnatomicalRegion.ExposedGrowth);
    }

    [Fact]
    public void TaskRunnerMovesAndChangesActionsDeterministically()
    {
        StableId id = StableIdFactory.Create("tests", "task-runner");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        var first = new CharacterState(id, product.Appearance, FixedVector3.Zero);
        var second = new CharacterState(id, product.Appearance, FixedVector3.Zero);
        var sequence = new CharacterTaskSequence(
        [
            new CharacterTask(CharacterTaskKind.MoveTo, new FixedVector3(6000, 0, 0), StableId.Empty, 60),
            new CharacterTask(CharacterTaskKind.Aim, new FixedVector3(6000, 0, -10000), StableId.Empty, 30, "rifle")
        ]);
        var runnerA = new CharacterTaskRunner();
        var runnerB = new CharacterTaskRunner();
        runnerA.Register(first, sequence);
        runnerB.Register(second, sequence);
        for (int tick = 0; tick < 120; tick++)
        {
            runnerA.Tick();
            runnerB.Tick();
        }

        Assert.Equal(first.ComputeChecksum(), second.ComputeChecksum());
    }

    [Fact]
    public void CharacterValidatorAcceptsGeneratedProducts()
    {
        StableId id = StableIdFactory.Create("tests", "validator");
        GeneratedCharacterProduct product = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(Seed, id, "test-human"));
        ValidationReport report = new CharacterProductValidator().Validate(product);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues.Select(static issue => issue.Message)));
    }

    private static void ValidateMesh(CharacterMeshProduct mesh, int boneCount)
    {
        foreach (CharacterVertex vertex in mesh.Vertices)
        {
            float sum = vertex.Skin.Weights.X + vertex.Skin.Weights.Y + vertex.Skin.Weights.Z + vertex.Skin.Weights.W;
            Assert.InRange(sum, 0.999f, 1.001f);
            foreach ((byte bone, float weight) in vertex.Skin.Enumerate())
            {
                if (weight > 0f)
                {
                    Assert.InRange((int)bone, 0, boneCount - 1);
                }
            }
        }
    }

    private static StateChecksum Checksum(GeneratedCharacterProduct product)
    {
        var writer = new CanonicalWriter();
        product.Skeleton.WriteCanonical(writer);
        foreach (CharacterLodProduct lod in product.Lods)
        {
            lod.Body.WriteCanonical(writer);
            lod.Clothing?.WriteCanonical(writer);
        }

        return writer.ComputeChecksum();
    }

    private static Vector3 Translation(Matrix4x4 matrix) => new(matrix.M41, matrix.M42, matrix.M43);

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
