using System.Numerics;
using Roentgen.Contracts;

namespace Roentgen.Animation;

public sealed class CharacterProductValidator : IValidator<GeneratedCharacterProduct>
{
    public ValidationReport Validate(GeneratedCharacterProduct value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var issues = new List<ValidationIssue>();
        ValidateSkeleton(value, issues);
        ValidateLods(value, issues);
        ValidateTargetParts(value, issues);
        ValidateEquipmentProfiles(value, issues);
        return new ValidationReport(!issues.Any(static issue => issue.IsCritical), issues);
    }

    private static void ValidateSkeleton(GeneratedCharacterProduct value, List<ValidationIssue> issues)
    {
        if (value.Skeleton.Bones.Count > 48)
        {
            issues.Add(new ValidationIssue("character.skeleton.bone-budget", "Skeleton exceeds the 48-bone runtime palette.", true));
        }

        if (value.Skeleton.Bones.Select(static bone => bone.Id).Distinct().Count() != value.Skeleton.Bones.Count)
        {
            issues.Add(new ValidationIssue("character.skeleton.duplicate-id", "Skeleton contains duplicate stable bone IDs.", true));
        }

        string[] required = value.Appearance.Species switch
        {
            CharacterSpecies.Human or CharacterSpecies.HumanoidMutant => ["root", "pelvis", "chest", "head", "hand_l", "hand_r", "foot_l", "foot_r"],
            CharacterSpecies.IndustrialQuadruped => ["root", "pelvis", "chest", "head", "front_l_foot", "front_r_foot", "rear_l_foot", "rear_r_foot"],
            _ => ["root", "pelvis", "chest", "head"]
        };
        foreach (string name in required)
        {
            if (value.Skeleton.IndexOf(name) < 0)
            {
                issues.Add(new ValidationIssue("character.skeleton.required-bone", $"Required bone '{name}' is missing.", true));
            }
        }
    }

    private static void ValidateLods(GeneratedCharacterProduct value, List<ValidationIssue> issues)
    {
        if (value.Lods.Count == 0)
        {
            issues.Add(new ValidationIssue("character.lod.empty", "Character has no renderable LOD products.", true));
            return;
        }

        if (value.Lods.Select(static lod => lod.LodLevel).Distinct().Count() != value.Lods.Count)
        {
            issues.Add(new ValidationIssue("character.lod.duplicate", "Character contains duplicate LOD levels.", true));
        }

        foreach (CharacterLodProduct lod in value.Lods)
        {
            ValidateMesh(lod.Body, value.Skeleton.Bones.Count, issues);
            if (lod.Clothing is not null)
            {
                ValidateMesh(lod.Clothing, value.Skeleton.Bones.Count, issues);
            }
        }

        if (value.Appearance.Species is (CharacterSpecies.Human or CharacterSpecies.HumanoidMutant))
        {
            if (value.FirstPersonArms is null)
            {
                issues.Add(new ValidationIssue("character.first-person-arms.missing", "Humanoid has no generated first-person arm product.", true));
            }
            else
            {
                ValidateMesh(value.FirstPersonArms, value.Skeleton.Bones.Count, issues);
            }
        }
    }

    private static void ValidateTargetParts(GeneratedCharacterProduct value, List<ValidationIssue> issues)
    {
        if (value.TargetParts.Count == 0)
        {
            issues.Add(new ValidationIssue("character.target-parts.empty", "Character has no target-part definitions.", true));
        }

        if (value.TargetParts.Select(static part => part.Id).Distinct().Count() != value.TargetParts.Count)
        {
            issues.Add(new ValidationIssue("character.target-parts.duplicate-id", "Character target parts contain duplicate stable IDs.", true));
        }

        foreach (CharacterTargetPart part in value.TargetParts)
        {
            if (value.Skeleton.IndexOf(part.BoneName) < 0)
            {
                issues.Add(new ValidationIssue("character.target-parts.bone", $"Target part '{part.Name}' references missing bone '{part.BoneName}'.", true));
            }

            if (part.RadiusMillimeters <= 0 || part.LengthMillimeters <= 0)
            {
                issues.Add(new ValidationIssue("character.target-parts.bounds", $"Target part '{part.Name}' has invalid dimensions.", true));
            }
        }
    }

    private static void ValidateEquipmentProfiles(GeneratedCharacterProduct value, List<ValidationIssue> issues)
    {
        if (value.Appearance.Species is (CharacterSpecies.Human or CharacterSpecies.HumanoidMutant) && !value.EquipmentProfiles.ContainsKey("unarmed"))
        {
            issues.Add(new ValidationIssue("character.equipment.unarmed", "Humanoid has no unarmed equipment pose profile.", true));
        }

        foreach ((string key, EquipmentPoseProfile profile) in value.EquipmentProfiles)
        {
            if (!string.Equals(key, profile.ProfileId, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue("character.equipment.profile-key", $"Equipment profile key '{key}' does not match profile ID '{profile.ProfileId}'.", true));
            }

            int previous = -1;
            foreach (AnimationMarker marker in profile.Markers)
            {
                if (marker.ProgressPermille < 0 || marker.ProgressPermille > 1000 || marker.ProgressPermille < previous)
                {
                    issues.Add(new ValidationIssue("character.equipment.marker-order", $"Equipment profile '{key}' contains an invalid or unordered marker '{marker.MarkerId}'.", true));
                }

                previous = marker.ProgressPermille;
            }
        }
    }

    private static void ValidateMesh(CharacterMeshProduct mesh, int boneCount, List<ValidationIssue> issues)
    {
        if (mesh.Indices.Any(index => index < 0 || index >= mesh.Vertices.Length))
        {
            issues.Add(new ValidationIssue("character.mesh.index", $"Mesh '{mesh.Name}' contains an invalid triangle index.", true));
        }

        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            CharacterVertex vertex = mesh.Vertices[index];
            if (!IsFinite(vertex.Position) || !IsFinite(vertex.Normal) || !IsFinite(vertex.Color) || !IsFinite(vertex.TextureCoordinate))
            {
                issues.Add(new ValidationIssue("character.mesh.non-finite", $"Mesh '{mesh.Name}' vertex {index} contains non-finite data.", true));
                continue;
            }

            float sum = vertex.Skin.Weights.X + vertex.Skin.Weights.Y + vertex.Skin.Weights.Z + vertex.Skin.Weights.W;
            if (vertex.Skin.Weights.X < 0f || vertex.Skin.Weights.Y < 0f || vertex.Skin.Weights.Z < 0f || vertex.Skin.Weights.W < 0f)
            {
                issues.Add(new ValidationIssue("character.skin.negative-weight", $"Mesh '{mesh.Name}' vertex {index} contains a negative skin weight.", true));
            }

            if (MathF.Abs(sum - 1f) > 0.001f)
            {
                issues.Add(new ValidationIssue("character.skin.weight-sum", $"Mesh '{mesh.Name}' vertex {index} weights sum to {sum}.", true));
            }

            foreach ((byte bone, float weight) in vertex.Skin.Enumerate())
            {
                if (weight > 0f && bone >= boneCount)
                {
                    issues.Add(new ValidationIssue("character.skin.bone-index", $"Mesh '{mesh.Name}' vertex {index} references invalid bone {bone}.", true));
                }
            }
        }
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool IsFinite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
}
