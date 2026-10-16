using Roentgen.Content;
using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Combat;

public sealed record Phase21TargetProfile(
    string Id,
    string Kind,
    IReadOnlyList<string> TargetPartKeys,
    IReadOnlyList<string> Consequences);

public static class Phase21TargetProfileCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, Phase21TargetProfile>> Profiles = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyCollection<Phase21TargetProfile> All => Profiles.Value.Values.ToArray();
    public static int CreatureProfileCount => Profiles.Value.Values.Count(static value => value.Kind == "creature");
    public static int RobotProfileCount => Profiles.Value.Values.Count(static value => value.Kind == "robot");

    public static Phase21TargetProfile Get(string id) => Profiles.Value.TryGetValue(id, out Phase21TargetProfile? profile)
        ? profile
        : throw new KeyNotFoundException($"Unknown Phase 21 target profile '{id}'.");

    public static IReadOnlyList<CharacterTargetPart> Materialize(string profileId, StableId actorId)
    {
        Phase21TargetProfile profile = Get(profileId);
        return profile.TargetPartKeys.Select((key, index) => BuildPart(actorId, key, index, profile.TargetPartKeys.Count)).ToArray();
    }

    private static IReadOnlyDictionary<string, Phase21TargetProfile> Create()
    {
        Phase21ContentRuntime content = Phase21ContentRuntime.CreateDefault();
        var profiles = new SortedDictionary<string, Phase21TargetProfile>(StringComparer.Ordinal);
        foreach (CreatureFamilyContentDefinition creature in content.Pack.CreatureFamilies)
            profiles.Add(creature.Id, new Phase21TargetProfile(creature.Id, "creature", creature.TargetParts, creature.InjuryConsequences));
        foreach (RobotChassisContentDefinition robot in content.Pack.RobotChassis)
            profiles.Add(robot.Id, new Phase21TargetProfile(robot.Id, "robot", robot.TargetParts, robot.ComponentConsequences));
        foreach (HumanoidArchetypeContentDefinition humanoid in content.Pack.HumanoidArchetypes)
            profiles.Add(humanoid.Id, new Phase21TargetProfile(humanoid.Id, "humanoid", StandardHumanoidParts, StandardHumanoidConsequences));

        if (profiles.Values.Count(static value => value.Kind == "creature") != ContentBudgets.CreatureFamilies)
            throw new InvalidDataException("The creature target-profile catalog is incomplete.");
        if (profiles.Values.Count(static value => value.Kind == "robot") != ContentBudgets.RobotChassis)
            throw new InvalidDataException("The robot target-profile catalog is incomplete.");
        if (profiles.Values.Any(static value => value.TargetPartKeys.Count < 4 || value.Consequences.Count < 2))
            throw new InvalidDataException("A target profile lacks consequential locational-damage content.");
        return profiles;
    }

    private static CharacterTargetPart BuildPart(StableId actorId, string key, int index, int total)
    {
        AnatomicalRegion region = RegionFor(key, index);
        int radius = region is AnatomicalRegion.Torso ? 300 : region is AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster ? 170 : 130;
        int length = region is AnatomicalRegion.Torso ? 650 : region is AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster ? 260 : 520;
        int weight = Math.Clamp(1200 - index * Math.Max(40, 500 / Math.Max(1, total)), 450, 1200);
        string display = string.Join(" ", key.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(static word => char.ToUpperInvariant(word[0]) + word[1..]));
        return new CharacterTargetPart(StableIdFactory.CreateChild(actorId, "target-part", key), display, region, BoneFor(region), radius, length, weight);
    }

    private static AnatomicalRegion RegionFor(string key, int index)
    {
        string normalized = key.ToLowerInvariant();
        if (normalized.Contains("head") || normalized.Contains("brain") || normalized.Contains("optic") || normalized.Contains("sensor") || normalized.Contains("processor")) return AnatomicalRegion.SensoryCluster;
        if (normalized.Contains("jaw")) return AnatomicalRegion.Jaw;
        if (normalized.Contains("weapon")) return AnatomicalRegion.EquippedWeapon;
        if (normalized.Contains("wing")) return index % 2 == 0 ? AnatomicalRegion.LeftArm : AnatomicalRegion.RightArm;
        if (normalized.Contains("front") || normalized.Contains("manipulator") || normalized.Contains("arm")) return index % 2 == 0 ? AnatomicalRegion.LeftArm : AnatomicalRegion.RightArm;
        if (normalized.Contains("hind") || normalized.Contains("rear") || normalized.Contains("mobility") || normalized.Contains("leg")) return index % 2 == 0 ? AnatomicalRegion.LeftLeg : AnatomicalRegion.RightLeg;
        if (normalized.Contains("power") || normalized.Contains("cooling") || normalized.Contains("abdomen") || normalized.Contains("torso") || normalized.Contains("core")) return AnatomicalRegion.Torso;
        return AnatomicalRegion.ExposedGrowth;
    }

    private static string BoneFor(AnatomicalRegion region) => region switch
    {
        AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster or AnatomicalRegion.Jaw => "head",
        AnatomicalRegion.LeftArm or AnatomicalRegion.FrontLeftLimb => "upper_arm_l",
        AnatomicalRegion.RightArm or AnatomicalRegion.FrontRightLimb => "upper_arm_r",
        AnatomicalRegion.LeftLeg or AnatomicalRegion.RearLeftLimb => "thigh_l",
        AnatomicalRegion.RightLeg or AnatomicalRegion.RearRightLimb => "thigh_r",
        AnatomicalRegion.EquippedWeapon => "weapon_socket",
        _ => "chest"
    };

    private static readonly string[] StandardHumanoidParts = ["head", "torso", "left-arm", "right-arm", "left-leg", "right-leg", "equipped-weapon"];
    private static readonly string[] StandardHumanoidConsequences = ["head trauma reduces awareness", "arm injury degrades weapon handling", "leg injury reduces movement", "torso trauma causes bleeding"];
}
