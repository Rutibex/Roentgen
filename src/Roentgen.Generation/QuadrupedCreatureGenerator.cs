using System.Numerics;
using Roentgen.Animation;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Generation;

public sealed class QuadrupedCreatureGenerator : IVersionedGenerator<CharacterGenerationRequest, GeneratedCharacterProduct>
{
    public string GeneratorId => "roentgen.creature.blackwater-quadruped";
    public int SchemaVersion => 1;

    public GeneratedCharacterProduct Generate(CharacterGenerationRequest request, DomainSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainSeed root = string.IsNullOrEmpty(seed.Domain) ? CharacterSeedDomains.Root(request.CampaignSeed, request.CharacterId) : seed;
        Pcg32Random random = RandomStreamFactory.Create(CharacterSeedDomains.CreatureBodyPlan(root), "quadruped");
        int height = request.HeightMillimetersOverride ?? random.NextInt32(850, 1251);
        int build = request.BuildPermilleOverride ?? random.NextInt32(850, 1351);
        var appearance = new CharacterAppearanceDescriptor(
            CharacterSpecies.IndustrialQuadruped,
            request.ArchetypeId,
            height,
            build,
            random.NextInt32(1000),
            random.NextInt32(780, 1021),
            random.NextInt32(6),
            0,
            random.NextInt32(5),
            random.NextInt32(8));
        SkeletonDefinition skeleton = BuildSkeleton(request.CharacterId, appearance);
        Vector4 bodyColor = new(0.21f, 0.28f, 0.25f, 1f);
        Vector4 growthColor = new(0.45f, 0.63f, 0.42f, 1f);
        var lods = new List<CharacterLodProduct>();
        for (int lod = 0; lod < 3; lod++)
        {
            int sides = lod switch { 0 => 8, 1 => 6, _ => 4 };
            CharacterMeshProduct body = BuildMesh($"{request.CharacterId}-quadruped-lod{lod}", skeleton, appearance, bodyColor, growthColor, sides);
            lods.Add(new CharacterLodProduct(body, null, lod));
        }

        IReadOnlyList<CharacterTargetPart> parts = BuildTargetParts(request.CharacterId, appearance);
        var provenance = new GenerationProvenanceBuilder(request.CharacterId, request.ParentId, request.CampaignSeed, root, GeneratorId, SchemaVersion);
        provenance.RecordDecision("body-plan", "industrial-quadruped");
        provenance.RecordDecision("height-mm", height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var product = new GeneratedCharacterProduct(
            request.CharacterId,
            appearance,
            skeleton,
            lods,
            null,
            parts,
            new Dictionary<string, EquipmentPoseProfile>(StringComparer.Ordinal),
            provenance.Build());
        ValidationReport report = new CharacterProductValidator().Validate(product);
        foreach (ValidationIssue issue in report.Issues)
        {
            provenance.RecordValidation($"{issue.Code}:{issue.Message}");
        }

        GeneratedCharacterProduct accepted = product;
        if (!report.IsValid)
        {
            RepairResult<GeneratedCharacterProduct> repair = new CharacterGenerationRepairer().TryRepair(product, report, CharacterSeedDomains.Repair(root));
            provenance.RecordRepair(repair.Summary);
            if (!repair.Changed)
            {
                throw new InvalidDataException(string.Join("; ", report.Issues.Select(static value => value.Message)));
            }

            accepted = repair.Value;
        }

        return accepted with { Provenance = provenance.Build() };
    }

    public static GeneratedCharacterProduct Generate(CharacterGenerationRequest request)
    {
        DomainSeed root = CharacterSeedDomains.Root(request.CampaignSeed, request.CharacterId);
        return new QuadrupedCreatureGenerator().Generate(request, root);
    }

    private static SkeletonDefinition BuildSkeleton(StableId characterId, CharacterAppearanceDescriptor appearance)
    {
        float scale = appearance.HeightMillimeters / 1000f;
        var bones = new List<BoneDefinition>();
        int Add(string name, int parent, Vector3 translation)
        {
            bones.Add(new BoneDefinition(
                StableIdFactory.CreateChild(characterId, "bone", name),
                name,
                parent,
                new BoneTransform(translation, Quaternion.Identity, Vector3.One)));
            return bones.Count - 1;
        }

        int root = Add("root", -1, Vector3.Zero);
        int pelvis = Add("pelvis", root, new Vector3(0f, 0.58f * scale, 0.18f * scale));
        int spineRear = Add("spine_rear", pelvis, new Vector3(0f, 0.1f * scale, -0.08f * scale));
        int spineMid = Add("spine_mid", spineRear, new Vector3(0f, 0.03f * scale, -0.42f * scale));
        int chest = Add("chest", spineMid, new Vector3(0f, 0.06f * scale, -0.42f * scale));
        int neck = Add("neck", chest, new Vector3(0f, 0.07f * scale, -0.22f * scale));
        int head = Add("head", neck, new Vector3(0f, -0.02f * scale, -0.23f * scale));
        Add("jaw", head, new Vector3(0f, -0.08f * scale, -0.12f * scale));
        Add("sensory_l", head, new Vector3(-0.09f * scale, 0.07f * scale, -0.06f * scale));
        Add("sensory_r", head, new Vector3(0.09f * scale, 0.07f * scale, -0.06f * scale));
        AddLeg("front_l", chest, -1f);
        AddLeg("front_r", chest, 1f);
        AddLeg("rear_l", pelvis, -1f);
        AddLeg("rear_r", pelvis, 1f);
        Add("growth", spineMid, new Vector3(0f, 0.18f * scale, 0f));
        Add("tail", pelvis, new Vector3(0f, 0.02f * scale, 0.24f * scale));
        return new SkeletonDefinition("roentgen.industrial-quadruped.v1", bones);

        void AddLeg(string prefix, int parent, float side)
        {
            int upper = Add($"{prefix}_upper", parent, new Vector3(side * 0.18f * scale, -0.08f * scale, 0f));
            int lower = Add($"{prefix}_lower", upper, new Vector3(0f, -0.37f * scale, 0.04f * scale));
            Add($"{prefix}_foot", lower, new Vector3(0f, -0.32f * scale, -0.08f * scale));
        }
    }

    private static CharacterMeshProduct BuildMesh(string name, SkeletonDefinition skeleton, CharacterAppearanceDescriptor appearance, Vector4 bodyColor, Vector4 growthColor, int sides)
    {
        float scale = appearance.HeightMillimeters / 1000f;
        float build = appearance.BuildPermille / 1000f;
        Matrix4x4[] world = skeleton.BindWorldMatrices.ToArray();
        Vector3 P(string bone) => Translation(world[skeleton.IndexOf(bone)]);
        int B(string bone) => skeleton.IndexOf(bone);
        var builder = new ProceduralCharacterMeshBuilder();
        builder.AddTaperedSegment(P("pelvis"), P("chest"), 0.22f * scale * build, 0.25f * scale * build, B("pelvis"), B("chest"), bodyColor, sides, 7);
        builder.AddTaperedSegment(P("chest"), P("head"), 0.17f * scale * build, 0.12f * scale, B("chest"), B("head"), bodyColor, sides, 5);
        builder.AddEllipsoid(P("head") + new Vector3(0f, 0f, -0.08f * scale), new Vector3(0.15f, 0.13f, 0.22f) * scale, B("head"), bodyColor, sides + 2, Math.Max(4, sides - 1));
        foreach (string prefix in new[] { "front_l", "front_r", "rear_l", "rear_r" })
        {
            builder.AddTaperedSegment(P($"{prefix}_upper"), P($"{prefix}_lower"), 0.075f * scale * build, 0.055f * scale, B($"{prefix}_upper"), B($"{prefix}_lower"), bodyColor, sides, 4);
            builder.AddTaperedSegment(P($"{prefix}_lower"), P($"{prefix}_foot"), 0.055f * scale, 0.04f * scale, B($"{prefix}_lower"), B($"{prefix}_foot"), bodyColor, sides, 4);
            builder.AddBox(P($"{prefix}_foot") + new Vector3(0f, -0.025f * scale, -0.06f * scale), new Vector3(0.065f, 0.04f, 0.12f) * scale, B($"{prefix}_foot"), bodyColor);
        }

        builder.AddEllipsoid(P("growth"), new Vector3(0.18f, 0.16f, 0.15f) * scale, B("growth"), growthColor, sides, Math.Max(4, sides - 1));
        builder.AddTaperedSegment(P("pelvis"), P("tail") + new Vector3(0f, 0f, 0.35f * scale), 0.055f * scale, 0.018f * scale, B("pelvis"), B("tail"), bodyColor, sides, 5);
        return builder.Build(name);
    }

    private static IReadOnlyList<CharacterTargetPart> BuildTargetParts(StableId characterId, CharacterAppearanceDescriptor appearance)
    {
        int height = appearance.HeightMillimeters;
        CharacterTargetPart Part(string name, AnatomicalRegion region, string bone, int consequence) =>
            new(StableIdFactory.CreateChild(characterId, "target-part", name), name, region, bone, height / 14, height / 3, consequence);
        return
        [
            Part("Sensory Cluster", AnatomicalRegion.SensoryCluster, "head", 900),
            Part("Jaw", AnatomicalRegion.Jaw, "jaw", 450),
            Part("Torso", AnatomicalRegion.Torso, "chest", 800),
            Part("Front Left Limb", AnatomicalRegion.FrontLeftLimb, "front_l_upper", 500),
            Part("Front Right Limb", AnatomicalRegion.FrontRightLimb, "front_r_upper", 500),
            Part("Rear Left Limb", AnatomicalRegion.RearLeftLimb, "rear_l_upper", 600),
            Part("Rear Right Limb", AnatomicalRegion.RearRightLimb, "rear_r_upper", 600),
            Part("Exposed Growth", AnatomicalRegion.ExposedGrowth, "growth", 1000)
        ];
    }

    private static Vector3 Translation(Matrix4x4 matrix) => new(matrix.M41, matrix.M42, matrix.M43);
}
