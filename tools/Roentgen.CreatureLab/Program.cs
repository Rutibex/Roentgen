using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Roentgen.Animation;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Generation;

namespace Roentgen.CreatureLab;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        try
        {
            string command = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
            return command switch
            {
                "humanoid" => GenerateHumanoid(args),
                "creature" => GenerateCreature(args),
                "animate" => Animate(args),
                "validate" => Validate(args),
                "validate-seeds" => ValidateSeeds(args),
                "skin-weights" => SkinWeights(args),
                "target-parts" => TargetParts(args),
                "injury" => Injury(args),
                "equipment" => Equipment(args),
                "lod" => Lod(args),
                "export-report" => ExportReport(args),
                "inspect-provenance" => InspectProvenance(args),
                _ => Help()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int GenerateHumanoid(string[] args)
    {
        GeneratedCharacterProduct product = Humanoid(args);
        WriteSummary(product);
        return 0;
    }

    private static int GenerateCreature(string[] args)
    {
        GeneratedCharacterProduct product = Creature(args);
        WriteSummary(product);
        return 0;
    }

    private static int Animate(string[] args)
    {
        GeneratedCharacterProduct product = SelectProduct(args);
        var runtime = new CharacterPoseRuntime();
        foreach (CharacterActionKind action in Enum.GetValues<CharacterActionKind>())
        {
            var input = new CharacterPoseInput(
                action,
                500,
                250,
                new Vector3(1.5f, 0f, 0.5f),
                0,
                -Vector3.UnitZ,
                new Vector3(0.2f, 0.1f, -1f),
                CharacterInjuryPresentation.None,
                "rifle",
                CharacterLodLevel.Full,
                LeftGroundNormal: Vector3.UnitY,
                RightGroundNormal: Vector3.UnitY);
            PoseBuffer pose = runtime.Evaluate(product, input);
            StateChecksum checksum = PoseChecksum(pose);
            Console.WriteLine($"{action,-24} {checksum}");
        }

        return 0;
    }

    private static int Validate(string[] args)
    {
        GeneratedCharacterProduct product = SelectProduct(args);
        ValidationReport report = new CharacterProductValidator().Validate(product);
        Console.WriteLine(report.IsValid ? "VALID" : "INVALID");
        foreach (ValidationIssue issue in report.Issues)
        {
            Console.WriteLine($"{(issue.IsCritical ? "ERROR" : "WARN")} {issue.Code}: {issue.Message}");
        }

        return report.IsValid ? 0 : 2;
    }

    private static int ValidateSeeds(string[] args)
    {
        int count = ReadInt(args, "--count", 1000);
        int failures = 0;
        var validator = new CharacterProductValidator();
        for (int index = 0; index < count; index++)
        {
            CampaignSeed campaignSeed = new(unchecked(0x524F454E5447454EUL + (ulong)index));
            StableId id = StableIdFactory.Create("creaturelab-batch", index.ToString(CultureInfo.InvariantCulture));
            GeneratedCharacterProduct product = index % 10 == 0
                ? QuadrupedCreatureGenerator.Generate(new CharacterGenerationRequest(campaignSeed, id, "batch-quadruped", CharacterSpecies.IndustrialQuadruped))
                : HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(campaignSeed, id, "batch-humanoid"));
            if (!validator.Validate(product).IsValid)
            {
                failures++;
            }
        }

        Console.WriteLine($"Generated: {count}");
        Console.WriteLine($"Failures:  {failures}");
        return failures == 0 ? 0 : 2;
    }

    private static int SkinWeights(string[] args)
    {
        GeneratedCharacterProduct product = SelectProduct(args);
        CharacterMeshProduct mesh = product.Lods[0].Body;
        double minimum = double.MaxValue;
        double maximum = double.MinValue;
        double average = 0;
        foreach (CharacterVertex vertex in mesh.Vertices)
        {
            double sum = vertex.Skin.Weights.X + vertex.Skin.Weights.Y + vertex.Skin.Weights.Z + vertex.Skin.Weights.W;
            minimum = Math.Min(minimum, sum);
            maximum = Math.Max(maximum, sum);
            average += sum;
        }

        average /= mesh.Vertices.Length;
        Console.WriteLine($"Vertices: {mesh.Vertices.Length}");
        Console.WriteLine($"Weight sum min/avg/max: {minimum:F6} / {average:F6} / {maximum:F6}");
        return 0;
    }

    private static int TargetParts(string[] args)
    {
        GeneratedCharacterProduct product = SelectProduct(args);
        foreach (CharacterTargetPart part in product.TargetParts)
        {
            Console.WriteLine($"{part.Region,-20} {part.Name,-22} bone={part.BoneName,-18} radius={part.RadiusMillimeters}mm consequence={part.ConsequenceWeightPermille}");
        }

        return 0;
    }

    private static int Injury(string[] args)
    {
        GeneratedCharacterProduct product = Humanoid(args);
        var runtime = new CharacterPoseRuntime();
        CharacterInjuryPresentation[] injuries =
        [
            CharacterInjuryPresentation.None,
            new CharacterInjuryPresentation(0, 250, 700, 0, 0, 0, 350, 0),
            new CharacterInjuryPresentation(0, 200, 0, 0, 750, 0, 500, 250),
            new CharacterInjuryPresentation(200, 600, 300, 300, 500, 500, 800, 650)
        ];
        foreach (CharacterInjuryPresentation injury in injuries)
        {
            PoseBuffer pose = runtime.Evaluate(product, new CharacterPoseInput(
                CharacterActionKind.Walk,
                500,
                350,
                new Vector3(1.8f, 0f, 0f),
                0,
                -Vector3.UnitZ,
                -Vector3.UnitZ,
                injury,
                "unarmed",
                CharacterLodLevel.Full));
            Console.WriteLine($"{injury} => {PoseChecksum(pose)}");
        }

        return 0;
    }

    private static int Equipment(string[] args)
    {
        GeneratedCharacterProduct product = Humanoid(args);
        foreach ((string key, EquipmentPoseProfile profile) in product.EquipmentProfiles.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"{key}: grip={profile.GripKind}, markers={profile.Markers.Count}");
            foreach (AnimationMarker marker in profile.Markers)
            {
                Console.WriteLine($"  {marker.ProgressPermille,4} {marker.MarkerId}");
            }
        }

        return 0;
    }

    private static int Lod(string[] args)
    {
        GeneratedCharacterProduct product = SelectProduct(args);
        foreach (CharacterLodProduct lod in product.Lods)
        {
            int clothingTriangles = lod.Clothing?.PrimitiveCount ?? 0;
            Console.WriteLine($"LOD {lod.LodLevel}: body={lod.Body.PrimitiveCount} triangles, clothing={clothingTriangles} triangles");
        }

        return 0;
    }

    private static int ExportReport(string[] args)
    {
        GeneratedCharacterProduct product = SelectProduct(args);
        string output = ReadString(args, "--out", $"character-{product.CharacterId}.json");
        var report = new
        {
            id = product.CharacterId.ToString(),
            product.Appearance,
            skeleton = new
            {
                product.Skeleton.SkeletonId,
                boneCount = product.Skeleton.Bones.Count,
                sockets = product.Skeleton.Sockets
            },
            lods = product.Lods.Select(static lod => new
            {
                lod.LodLevel,
                bodyVertices = lod.Body.Vertices.Length,
                bodyTriangles = lod.Body.PrimitiveCount,
                clothingTriangles = lod.Clothing?.PrimitiveCount ?? 0
            }),
            targetParts = product.TargetParts,
            provenance = product.Provenance
        };
        File.WriteAllText(output, JsonSerializer.Serialize(report, JsonOptions));
        Console.WriteLine(Path.GetFullPath(output));
        return 0;
    }

    private static int InspectProvenance(string[] args)
    {
        GeneratedCharacterProduct product = SelectProduct(args);
        Console.WriteLine(JsonSerializer.Serialize(product.Provenance, JsonOptions));
        return 0;
    }

    private static GeneratedCharacterProduct SelectProduct(string[] args) =>
        args.Contains("--creature", StringComparer.OrdinalIgnoreCase) ? Creature(args) : Humanoid(args);

    private static GeneratedCharacterProduct Humanoid(string[] args)
    {
        ulong seed = ReadUlong(args, "--seed", 0x524F454E5447454EUL);
        string key = ReadString(args, "--key", "creaturelab-humanoid");
        StableId id = StableIdFactory.Create("creaturelab", key);
        return HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(new CampaignSeed(seed), id, ReadString(args, "--archetype", "creaturelab-humanoid")));
    }

    private static GeneratedCharacterProduct Creature(string[] args)
    {
        ulong seed = ReadUlong(args, "--seed", 0x524F454E5447454EUL);
        string key = ReadString(args, "--key", "creaturelab-quadruped");
        StableId id = StableIdFactory.Create("creaturelab", key);
        return QuadrupedCreatureGenerator.Generate(new CharacterGenerationRequest(new CampaignSeed(seed), id, ReadString(args, "--archetype", "blackwater-industrial-quadruped"), CharacterSpecies.IndustrialQuadruped));
    }

    private static StateChecksum PoseChecksum(PoseBuffer pose)
    {
        var writer = new CanonicalWriter();
        writer.WriteCount(pose.Count);
        foreach (BoneTransform transform in pose.LocalTransforms)
        {
            writer.WriteSingle(transform.Translation.X);
            writer.WriteSingle(transform.Translation.Y);
            writer.WriteSingle(transform.Translation.Z);
            writer.WriteSingle(transform.Rotation.X);
            writer.WriteSingle(transform.Rotation.Y);
            writer.WriteSingle(transform.Rotation.Z);
            writer.WriteSingle(transform.Rotation.W);
        }

        return writer.ComputeChecksum();
    }

    private static void WriteSummary(GeneratedCharacterProduct product)
    {
        Console.WriteLine($"ID:          {product.CharacterId}");
        Console.WriteLine($"Archetype:   {product.Appearance.ArchetypeId}");
        Console.WriteLine($"Species:     {product.Appearance.Species}");
        Console.WriteLine($"Height:      {product.Appearance.HeightMillimeters} mm");
        Console.WriteLine($"Bones:       {product.Skeleton.Bones.Count}");
        Console.WriteLine($"LODs:        {product.Lods.Count}");
        Console.WriteLine($"Target parts:{product.TargetParts.Count}");
        Console.WriteLine($"Checksum:    {CharacterChecksum(product)}");
    }

    private static StateChecksum CharacterChecksum(GeneratedCharacterProduct product)
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

    private static string ReadString(string[] args, string name, string fallback)
    {
        int index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private static int ReadInt(string[] args, string name, int fallback) =>
        int.TryParse(ReadString(args, name, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    private static ulong ReadUlong(string[] args, string name, ulong fallback) =>
        ulong.TryParse(ReadString(args, name, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value) ? value : fallback;

    private static int Help()
    {
        Console.WriteLine("Roentgen CreatureLab — Phase 9");
        Console.WriteLine("Commands: humanoid, creature, animate, validate, validate-seeds, skin-weights, target-parts, injury, equipment, lod, export-report, inspect-provenance");
        Console.WriteLine("Options: --seed N --key NAME --archetype NAME --creature --count N --out FILE");
        return 0;
    }
}
