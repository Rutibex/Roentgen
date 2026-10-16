using System.Numerics;
using Roentgen.Animation;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Generation;

public sealed class HumanoidCharacterGenerator : IVersionedGenerator<CharacterGenerationRequest, GeneratedCharacterProduct>
{
    public const string CurrentGeneratorId = "roentgen.character.humanoid";
    public const int CurrentSchemaVersion = 2;

    public string GeneratorId => CurrentGeneratorId;
    public int SchemaVersion => CurrentSchemaVersion;

    public GeneratedCharacterProduct Generate(CharacterGenerationRequest request, DomainSeed seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ArchetypeId);
        if (request.CharacterId.IsEmpty)
        {
            throw new ArgumentException("Character ID cannot be empty.", nameof(request));
        }

        DomainSeed root = string.IsNullOrEmpty(seed.Domain) ? CharacterSeedDomains.Root(request.CampaignSeed, request.CharacterId) : seed;
        Pcg32Random bodyRandom = RandomStreamFactory.Create(CharacterSeedDomains.Body(root), "proportions");
        Pcg32Random appearanceRandom = RandomStreamFactory.Create(CharacterSeedDomains.Appearance(root), "appearance");
        int height = request.HeightMillimetersOverride ?? bodyRandom.NextInt32(1580, 1961);
        int build = request.BuildPermilleOverride ?? bodyRandom.NextInt32(700, 1351);
        int age = bodyRandom.NextInt32(100, 901);
        int posture = bodyRandom.NextInt32(780, 1021);
        var appearance = new CharacterAppearanceDescriptor(
            request.Species,
            request.ArchetypeId,
            height,
            build,
            age,
            posture,
            appearanceRandom.NextInt32(8),
            appearanceRandom.NextInt32(8),
            appearanceRandom.NextInt32(10),
            appearanceRandom.NextInt32(12));

        SkeletonDefinition skeleton = BuildSkeleton(request.CharacterId, appearance);
        Vector4 skinColor = SkinColor(appearance.SkinPaletteIndex);
        Vector4 clothingColor = ClothingColor(appearance.ClothingProfileIndex);
        var lods = new List<CharacterLodProduct>();
        for (int lod = 0; lod < 3; lod++)
        {
            int sides = lod switch { 0 => 8, 1 => 6, _ => 4 };
            int latitudes = lod switch { 0 => 6, 1 => 4, _ => 3 };
            CharacterMeshProduct body = BuildBodyMesh($"{request.CharacterId}-body-lod{lod}", skeleton, appearance, skinColor, sides, latitudes);
            CharacterMeshProduct clothing = BuildClothingMesh($"{request.CharacterId}-clothing-lod{lod}", skeleton, appearance, clothingColor, sides);
            lods.Add(new CharacterLodProduct(body, clothing, lod));
        }

        CharacterMeshProduct firstPersonArms = BuildFirstPersonArms(request.CharacterId, skeleton, appearance, skinColor, clothingColor);
        IReadOnlyList<CharacterTargetPart> targetParts = BuildTargetParts(request.CharacterId, skeleton, appearance);
        IReadOnlyDictionary<string, EquipmentPoseProfile> equipmentProfiles = BuildEquipmentProfiles();
        var provenance = new GenerationProvenanceBuilder(
            request.CharacterId,
            request.ParentId,
            request.CampaignSeed,
            root,
            GeneratorId,
            SchemaVersion);
        provenance.RecordDecision("body.height", height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        provenance.RecordDecision("body.build", build.ToString(System.Globalization.CultureInfo.InvariantCulture));
        provenance.RecordDecision("appearance.clothing", appearance.ClothingProfileIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var product = new GeneratedCharacterProduct(
            request.CharacterId,
            appearance,
            skeleton,
            lods,
            firstPersonArms,
            targetParts,
            equipmentProfiles,
            provenance.Build());
        ValidationReport validation = new CharacterProductValidator().Validate(product);
        foreach (ValidationIssue issue in validation.Issues)
        {
            provenance.RecordValidation($"{issue.Code}:{issue.Message}");
        }

        GeneratedCharacterProduct accepted = product;
        if (!validation.IsValid)
        {
            RepairResult<GeneratedCharacterProduct> repair = new CharacterGenerationRepairer().TryRepair(product, validation, CharacterSeedDomains.Repair(root));
            provenance.RecordRepair(repair.Summary);
            if (!repair.Changed)
            {
                throw new InvalidDataException($"Generated character {request.CharacterId} failed validation: {string.Join("; ", validation.Issues.Select(static issue => issue.Message))}");
            }

            accepted = repair.Value;
        }

        return accepted with { Provenance = provenance.Build() };
    }

    public static GeneratedCharacterProduct Generate(CharacterGenerationRequest request)
    {
        DomainSeed root = CharacterSeedDomains.Root(request.CampaignSeed, request.CharacterId);
        return new HumanoidCharacterGenerator().Generate(request, root);
    }

    private static SkeletonDefinition BuildSkeleton(StableId characterId, CharacterAppearanceDescriptor appearance)
    {
        float scale = appearance.HeightMillimeters / 1780f;
        float shoulder = 0.205f * scale * appearance.BuildPermille / 1000f;
        float hip = 0.11f * scale * (0.85f + appearance.BuildPermille / 5000f);
        float leg = 0.47f * scale;
        float shin = 0.45f * scale;
        float upperArm = 0.31f * scale;
        float forearm = 0.29f * scale;
        var bones = new List<BoneDefinition>();
        int Add(string name, int parent, Vector3 translation)
        {
            StableId id = StableIdFactory.CreateChild(characterId, "bone", name);
            bones.Add(new BoneDefinition(id, name, parent, new BoneTransform(translation, Quaternion.Identity, Vector3.One)));
            return bones.Count - 1;
        }

        int root = Add("root", -1, Vector3.Zero);
        int pelvis = Add("pelvis", root, new Vector3(0f, 0.91f * scale, 0f));
        int spineLower = Add("spine_lower", pelvis, new Vector3(0f, 0.16f * scale, 0f));
        int spineUpper = Add("spine_upper", spineLower, new Vector3(0f, 0.19f * scale, 0f));
        int chest = Add("chest", spineUpper, new Vector3(0f, 0.19f * scale, 0f));
        int neck = Add("neck", chest, new Vector3(0f, 0.19f * scale, 0f));
        int head = Add("head", neck, new Vector3(0f, 0.15f * scale, 0f));
        Add("jaw", head, new Vector3(0f, -0.06f * scale, -0.055f * scale));
        // Y is up in Roentgen world space. Keep the eyes above the nose so the
        // generated face cannot read as vertically inverted.
        Add("eye_l", head, new Vector3(-0.032f * scale, 0.105f * scale, -0.094f * scale));
        Add("eye_r", head, new Vector3(0.032f * scale, 0.105f * scale, -0.094f * scale));
        int clavicleLeft = Add("clavicle_l", chest, new Vector3(-0.08f * scale, 0.12f * scale, 0f));
        int upperArmLeft = Add("upper_arm_l", clavicleLeft, new Vector3(-shoulder, 0f, 0f));
        int forearmLeft = Add("forearm_l", upperArmLeft, new Vector3(-upperArm, 0f, 0f));
        int handLeft = Add("hand_l", forearmLeft, new Vector3(-forearm, 0f, 0f));
        int clavicleRight = Add("clavicle_r", chest, new Vector3(0.08f * scale, 0.12f * scale, 0f));
        int upperArmRight = Add("upper_arm_r", clavicleRight, new Vector3(shoulder, 0f, 0f));
        int forearmRight = Add("forearm_r", upperArmRight, new Vector3(upperArm, 0f, 0f));
        int handRight = Add("hand_r", forearmRight, new Vector3(forearm, 0f, 0f));
        int thighLeft = Add("thigh_l", pelvis, new Vector3(-hip, -0.03f * scale, 0f));
        int shinLeft = Add("shin_l", thighLeft, new Vector3(0f, -leg, 0f));
        int footLeft = Add("foot_l", shinLeft, new Vector3(0f, -shin, 0f));
        Add("toe_l", footLeft, new Vector3(0f, -0.06f * scale, -0.16f * scale));
        int thighRight = Add("thigh_r", pelvis, new Vector3(hip, -0.03f * scale, 0f));
        int shinRight = Add("shin_r", thighRight, new Vector3(0f, -leg, 0f));
        int footRight = Add("foot_r", shinRight, new Vector3(0f, -shin, 0f));
        Add("toe_r", footRight, new Vector3(0f, -0.06f * scale, -0.16f * scale));
        Add("coat_tail_l", pelvis, new Vector3(-0.08f * scale, 0.05f * scale, 0.06f * scale));
        Add("coat_tail_r", pelvis, new Vector3(0.08f * scale, 0.05f * scale, 0.06f * scale));
        var sockets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["weapon_primary"] = handRight,
            ["weapon_secondary"] = handLeft,
            ["hand_grip_l"] = handLeft,
            ["hand_grip_r"] = handRight,
            ["backpack"] = chest,
            ["headgear"] = head,
            ["hip_l"] = thighLeft,
            ["hip_r"] = thighRight,
            ["chest_item"] = chest
        };
        return new SkeletonDefinition("roentgen.humanoid.v1", bones, sockets);
    }

    private static CharacterMeshProduct BuildBodyMesh(string name, SkeletonDefinition skeleton, CharacterAppearanceDescriptor appearance, Vector4 color, int sides, int latitudes)
    {
        float scale = appearance.HeightMillimeters / 1780f;
        float build = appearance.BuildPermille / 1000f;
        var builder = new ProceduralCharacterMeshBuilder();
        Matrix4x4[] world = skeleton.BindWorldMatrices.ToArray();
        Vector3 P(string bone) => Translation(world[skeleton.IndexOf(bone)]);
        int B(string bone) => skeleton.IndexOf(bone);
        builder.AddTaperedSegment(P("pelvis"), P("spine_upper"), 0.16f * scale * build, 0.18f * scale * build, B("pelvis"), B("spine_upper"), color, sides, 5);
        builder.AddTaperedSegment(P("spine_upper"), P("neck"), 0.19f * scale * build, 0.075f * scale, B("spine_upper"), B("neck"), color, sides, 4);
        // Bridge the neck-to-head bind gap. Previously the head silhouette floated
        // above the torso, which made the already-misordered face read even more
        // strongly as an upside-down head.
        builder.AddTaperedSegment(
            P("neck"),
            P("head") + new Vector3(0f, -0.025f * scale, 0f),
            0.062f * scale,
            0.055f * scale,
            B("neck"),
            B("head"),
            color,
            Math.Max(5, sides),
            3);
        builder.AddEllipsoid(P("head") + new Vector3(0f, 0.08f * scale, 0f), new Vector3(0.105f * scale, 0.135f * scale, 0.11f * scale), B("head"), color, sides + 2, latitudes);
        // Stylized facial landmarks remain broad and low-detail to avoid an uncanny photoreal target.
        // Roentgen uses Y-up coordinates: eyes belong above the nose.
        Vector4 eyeColor = new(0.08f, 0.095f, 0.09f, 1f);
        builder.AddEllipsoid(P("eye_l"), new Vector3(0.014f, 0.011f, 0.008f) * scale, B("eye_l"), eyeColor, 5, 3);
        builder.AddEllipsoid(P("eye_r"), new Vector3(0.014f, 0.011f, 0.008f) * scale, B("eye_r"), eyeColor, 5, 3);
        builder.AddTaperedSegment(
            P("head") + new Vector3(0f, 0.060f * scale, -0.090f * scale),
            P("head") + new Vector3(0f, 0.025f * scale, -0.150f * scale),
            0.018f * scale,
            0.012f * scale,
            B("head"),
            B("head"),
            color,
            Math.Max(4, sides - 2),
            3);
        AddLimb(builder, skeleton, world, "upper_arm_l", "forearm_l", "hand_l", 0.065f * scale * build, 0.05f * scale, color, sides);
        AddLimb(builder, skeleton, world, "upper_arm_r", "forearm_r", "hand_r", 0.065f * scale * build, 0.05f * scale, color, sides);
        AddLeg(builder, skeleton, world, "thigh_l", "shin_l", "foot_l", 0.09f * scale * build, 0.065f * scale, color, sides);
        AddLeg(builder, skeleton, world, "thigh_r", "shin_r", "foot_r", 0.09f * scale * build, 0.065f * scale, color, sides);
        builder.AddEllipsoid(P("hand_l"), new Vector3(0.055f, 0.04f, 0.035f) * scale, B("hand_l"), color, sides, Math.Max(3, latitudes - 1));
        builder.AddEllipsoid(P("hand_r"), new Vector3(0.055f, 0.04f, 0.035f) * scale, B("hand_r"), color, sides, Math.Max(3, latitudes - 1));
        builder.AddBox(P("foot_l") + new Vector3(0f, -0.025f * scale, -0.075f * scale), new Vector3(0.07f, 0.045f, 0.15f) * scale, B("foot_l"), color);
        builder.AddBox(P("foot_r") + new Vector3(0f, -0.025f * scale, -0.075f * scale), new Vector3(0.07f, 0.045f, 0.15f) * scale, B("foot_r"), color);
        return builder.Build(name);
    }

    private static CharacterMeshProduct BuildClothingMesh(string name, SkeletonDefinition skeleton, CharacterAppearanceDescriptor appearance, Vector4 color, int sides)
    {
        float scale = appearance.HeightMillimeters / 1780f;
        float build = appearance.BuildPermille / 1000f;
        var builder = new ProceduralCharacterMeshBuilder();
        Matrix4x4[] world = skeleton.BindWorldMatrices.ToArray();
        Vector3 P(string bone) => Translation(world[skeleton.IndexOf(bone)]);
        int B(string bone) => skeleton.IndexOf(bone);
        builder.AddTaperedSegment(P("pelvis") + new Vector3(0f, -0.04f * scale, 0f), P("neck") + new Vector3(0f, -0.02f * scale, 0f), 0.185f * scale * build, 0.205f * scale * build, B("pelvis"), B("neck"), color, sides, 6);
        builder.AddTaperedSegment(P("upper_arm_l"), P("forearm_l"), 0.073f * scale * build, 0.057f * scale, B("upper_arm_l"), B("forearm_l"), color, sides, 4);
        builder.AddTaperedSegment(P("upper_arm_r"), P("forearm_r"), 0.073f * scale * build, 0.057f * scale, B("upper_arm_r"), B("forearm_r"), color, sides, 4);
        builder.AddTaperedSegment(P("thigh_l"), P("shin_l"), 0.102f * scale * build, 0.073f * scale, B("thigh_l"), B("shin_l"), color, sides, 4);
        builder.AddTaperedSegment(P("thigh_r"), P("shin_r"), 0.102f * scale * build, 0.073f * scale, B("thigh_r"), B("shin_r"), color, sides, 4);

        Vector4 hairColor = HairColor(appearance.HairProfileIndex);
        Vector3 headCenter = P("head") + new Vector3(0f, 0.145f * scale, 0.01f * scale);
        if (appearance.HairProfileIndex % 4 == 0)
        {
            builder.AddEllipsoid(headCenter, new Vector3(0.112f, 0.07f, 0.115f) * scale, B("head"), hairColor, sides + 2, Math.Max(3, sides - 1));
        }
        else if (appearance.HairProfileIndex % 4 == 1)
        {
            builder.AddBox(headCenter + new Vector3(0f, 0.015f * scale, 0.01f * scale), new Vector3(0.11f, 0.045f, 0.12f) * scale, B("head"), hairColor);
        }
        else if (appearance.HairProfileIndex % 4 == 2)
        {
            builder.AddTaperedSegment(headCenter, headCenter + new Vector3(0f, -0.22f * scale, 0.09f * scale), 0.09f * scale, 0.035f * scale, B("head"), B("head"), hairColor, Math.Max(5, sides), 4);
        }

        // Layered utility silhouettes: deterministic backpack and belt pouches selected by the clothing profile.
        if (appearance.ClothingProfileIndex % 3 == 0)
        {
            builder.AddBox(P("chest") + new Vector3(0f, 0.015f * scale, 0.18f * scale), new Vector3(0.16f, 0.22f, 0.09f) * scale, B("chest"), color * new Vector4(0.75f, 0.75f, 0.75f, 1f));
        }
        builder.AddBox(P("pelvis") + new Vector3(-0.17f * scale, 0.03f * scale, 0.08f * scale), new Vector3(0.05f, 0.07f, 0.035f) * scale, B("pelvis"), color * new Vector4(0.8f, 0.8f, 0.8f, 1f));
        builder.AddBox(P("pelvis") + new Vector3(0.17f * scale, 0.03f * scale, 0.08f * scale), new Vector3(0.05f, 0.07f, 0.035f) * scale, B("pelvis"), color * new Vector4(0.8f, 0.8f, 0.8f, 1f));
        return builder.Build(name);
    }

    private static CharacterMeshProduct BuildFirstPersonArms(StableId characterId, SkeletonDefinition skeleton, CharacterAppearanceDescriptor appearance, Vector4 skinColor, Vector4 clothingColor)
    {
        float scale = appearance.HeightMillimeters / 1780f;
        var builder = new ProceduralCharacterMeshBuilder();
        Matrix4x4[] world = skeleton.BindWorldMatrices.ToArray();
        Vector3 P(string bone) => Translation(world[skeleton.IndexOf(bone)]);
        int B(string bone) => skeleton.IndexOf(bone);
        foreach (string side in new[] { "l", "r" })
        {
            string upper = $"upper_arm_{side}";
            string forearm = $"forearm_{side}";
            string hand = $"hand_{side}";
            builder.AddTaperedSegment(P(upper), P(forearm), 0.072f * scale, 0.055f * scale, B(upper), B(forearm), clothingColor, 8, 5);
            builder.AddTaperedSegment(P(forearm), P(hand), 0.056f * scale, 0.04f * scale, B(forearm), B(hand), skinColor, 8, 5);
            builder.AddEllipsoid(P(hand), new Vector3(0.058f, 0.042f, 0.038f) * scale, B(hand), skinColor, 8, 5);
        }

        return builder.Build($"{characterId}-first-person-arms");
    }

    private static void AddLimb(ProceduralCharacterMeshBuilder builder, SkeletonDefinition skeleton, IReadOnlyList<Matrix4x4> world, string upper, string lower, string hand, float upperRadius, float lowerRadius, Vector4 color, int sides)
    {
        Vector3 a = Translation(world[skeleton.IndexOf(upper)]);
        Vector3 b = Translation(world[skeleton.IndexOf(lower)]);
        Vector3 c = Translation(world[skeleton.IndexOf(hand)]);
        builder.AddTaperedSegment(a, b, upperRadius, lowerRadius, skeleton.IndexOf(upper), skeleton.IndexOf(lower), color, sides, 4);
        builder.AddTaperedSegment(b, c, lowerRadius, lowerRadius * 0.75f, skeleton.IndexOf(lower), skeleton.IndexOf(hand), color, sides, 4);
    }

    private static void AddLeg(ProceduralCharacterMeshBuilder builder, SkeletonDefinition skeleton, IReadOnlyList<Matrix4x4> world, string thigh, string shin, string foot, float upperRadius, float lowerRadius, Vector4 color, int sides)
    {
        Vector3 a = Translation(world[skeleton.IndexOf(thigh)]);
        Vector3 b = Translation(world[skeleton.IndexOf(shin)]);
        Vector3 c = Translation(world[skeleton.IndexOf(foot)]);
        builder.AddTaperedSegment(a, b, upperRadius, lowerRadius, skeleton.IndexOf(thigh), skeleton.IndexOf(shin), color, sides, 5);
        builder.AddTaperedSegment(b, c, lowerRadius, lowerRadius * 0.75f, skeleton.IndexOf(shin), skeleton.IndexOf(foot), color, sides, 5);
    }

    private static IReadOnlyList<CharacterTargetPart> BuildTargetParts(StableId characterId, SkeletonDefinition skeleton, CharacterAppearanceDescriptor appearance)
    {
        int height = appearance.HeightMillimeters;
        CharacterTargetPart Part(string name, AnatomicalRegion region, string bone, int radius, int length, int consequence) =>
            new(StableIdFactory.CreateChild(characterId, "target-part", name), name, region, bone, radius, length, consequence);
        return
        [
            Part("Head", AnatomicalRegion.Head, "head", height / 16, height / 8, 1000),
            Part("Torso", AnatomicalRegion.Torso, "chest", height / 9, height / 3, 850),
            Part("Left Arm", AnatomicalRegion.LeftArm, "upper_arm_l", height / 25, height / 3, 450),
            Part("Right Arm", AnatomicalRegion.RightArm, "upper_arm_r", height / 25, height / 3, 450),
            Part("Left Leg", AnatomicalRegion.LeftLeg, "thigh_l", height / 20, height / 2, 600),
            Part("Right Leg", AnatomicalRegion.RightLeg, "thigh_r", height / 20, height / 2, 600),
            Part("Equipped Weapon", AnatomicalRegion.EquippedWeapon, "hand_r", height / 28, height / 3, 500)
        ];
    }

    private static IReadOnlyDictionary<string, EquipmentPoseProfile> BuildEquipmentProfiles()
    {
        var profiles = new Dictionary<string, EquipmentPoseProfile>(StringComparer.Ordinal)
        {
            ["unarmed"] = new("unarmed", EquipmentGripKind.Unarmed, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 0f, -0.25f), Vector3.Zero, []),
            ["sidearm"] = new("sidearm", EquipmentGripKind.OneHanded, Vector3.Zero, new Vector3(-0.08f, 0f, 0.04f), Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 0f, -0.32f), new Vector3(0f, -0.08f, -0.05f), [new AnimationMarker("reload-open", 180), new AnimationMarker("magazine-detached", 350), new AnimationMarker("magazine-inserted", 720), new AnimationMarker("reload-complete", 1000)]),
            ["rifle"] = new("rifle", EquipmentGripKind.TwoHanded, Vector3.Zero, new Vector3(-0.32f, 0f, -0.06f), new Vector3(0f, 0.03f, 0.18f), Vector3.UnitZ, new Vector3(0f, 0.02f, -0.78f), new Vector3(-0.05f, -0.11f, -0.2f), [new AnimationMarker("magazine-detached", 300), new AnimationMarker("magazine-inserted", 690), new AnimationMarker("chamber-operated", 870), new AnimationMarker("reload-complete", 1000)]),
            ["melee-one-handed"] = new("melee-one-handed", EquipmentGripKind.OneHanded, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 0f, -0.65f), Vector3.Zero, [new AnimationMarker("strike-window-open", 420), new AnimationMarker("strike-window-close", 660), new AnimationMarker("melee-complete", 1000)]),
            ["tool"] = new("tool", EquipmentGripKind.OneHanded, Vector3.Zero, new Vector3(-0.12f, 0f, 0.04f), Vector3.Zero, Vector3.UnitZ, new Vector3(0f, 0f, -0.28f), Vector3.Zero, [])
        };
        return profiles;
    }

    private static Vector4 SkinColor(int index) => (index % 8) switch
    {
        0 => new Vector4(0.78f, 0.59f, 0.46f, 1f),
        1 => new Vector4(0.64f, 0.43f, 0.31f, 1f),
        2 => new Vector4(0.45f, 0.28f, 0.2f, 1f),
        3 => new Vector4(0.86f, 0.7f, 0.57f, 1f),
        4 => new Vector4(0.54f, 0.36f, 0.27f, 1f),
        5 => new Vector4(0.71f, 0.5f, 0.38f, 1f),
        6 => new Vector4(0.37f, 0.24f, 0.18f, 1f),
        _ => new Vector4(0.82f, 0.65f, 0.52f, 1f)
    };

    private static Vector4 HairColor(int index) => (index % 8) switch
    {
        0 => new Vector4(0.08f, 0.065f, 0.05f, 1f),
        1 => new Vector4(0.19f, 0.13f, 0.08f, 1f),
        2 => new Vector4(0.34f, 0.27f, 0.18f, 1f),
        3 => new Vector4(0.55f, 0.5f, 0.37f, 1f),
        4 => new Vector4(0.18f, 0.18f, 0.17f, 1f),
        5 => new Vector4(0.32f, 0.12f, 0.07f, 1f),
        6 => new Vector4(0.62f, 0.62f, 0.58f, 1f),
        _ => new Vector4(0.12f, 0.09f, 0.07f, 1f)
    };

    private static Vector4 ClothingColor(int index) => (index % 10) switch
    {
        0 => new Vector4(0.22f, 0.29f, 0.27f, 1f),
        1 => new Vector4(0.34f, 0.26f, 0.2f, 1f),
        2 => new Vector4(0.26f, 0.24f, 0.34f, 1f),
        3 => new Vector4(0.42f, 0.36f, 0.19f, 1f),
        4 => new Vector4(0.18f, 0.31f, 0.37f, 1f),
        5 => new Vector4(0.4f, 0.22f, 0.2f, 1f),
        6 => new Vector4(0.3f, 0.34f, 0.18f, 1f),
        7 => new Vector4(0.32f, 0.3f, 0.28f, 1f),
        8 => new Vector4(0.16f, 0.21f, 0.24f, 1f),
        _ => new Vector4(0.47f, 0.43f, 0.34f, 1f)
    };

    private static Vector3 Translation(Matrix4x4 matrix) => new(matrix.M41, matrix.M42, matrix.M43);
}
