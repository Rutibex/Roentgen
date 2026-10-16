using Roentgen.Contracts;

namespace Roentgen.World;

public sealed class TerrainChunkValidator : IValidator<TerrainChunkData>
{
    private readonly WorldGenerationProfile _profile;

    public TerrainChunkValidator(WorldGenerationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
    }

    public ValidationReport Validate(TerrainChunkData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var issues = new List<ValidationIssue>();
        if (value.Lods.Count != _profile.LodSampleCounts.Count)
        {
            issues.Add(new ValidationIssue("terrain.lod-count", "Chunk LOD count does not match the profile.", true));
        }

        for (int index = 0; index < value.Lods.Count; index++)
        {
            TerrainChunkLodData lod = value.Lods[index];
            if (lod.Lod != index || lod.SampleCount != _profile.LodSampleCounts[index])
            {
                issues.Add(new ValidationIssue("terrain.lod-shape", $"LOD {index} does not match the configured shape.", true));
            }

            foreach (int height in lod.Heights)
            {
                if (height < _profile.MinimumElevationCentimeters || height > _profile.MaximumElevationCentimeters)
                {
                    issues.Add(new ValidationIssue("terrain.height-range", $"LOD {index} contains out-of-range height {height}.", true));
                    break;
                }
            }
        }

        return new ValidationReport(issues.All(static issue => !issue.IsCritical), issues);
    }

    public static ValidationReport ValidateSeam(TerrainChunkData leftOrBack, TerrainChunkData rightOrFront, bool alongX)
    {
        ArgumentNullException.ThrowIfNull(leftOrBack);
        ArgumentNullException.ThrowIfNull(rightOrFront);
        var issues = new List<ValidationIssue>();
        TerrainChunkAddress expected = alongX
            ? new TerrainChunkAddress(leftOrBack.Address.X + 1, leftOrBack.Address.Z)
            : new TerrainChunkAddress(leftOrBack.Address.X, leftOrBack.Address.Z + 1);
        if (rightOrFront.Address != expected)
        {
            issues.Add(new ValidationIssue("terrain.seam-address", $"Chunks {leftOrBack.Address} and {rightOrFront.Address} are not the expected adjacent pair.", true));
        }

        int lodCount = Math.Min(leftOrBack.Lods.Count, rightOrFront.Lods.Count);
        for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
        {
            TerrainChunkLodData first = leftOrBack.Lods[lodIndex];
            TerrainChunkLodData second = rightOrFront.Lods[lodIndex];
            if (first.SampleCount != second.SampleCount)
            {
                issues.Add(new ValidationIssue("terrain.seam-shape", $"LOD {lodIndex} sample counts differ.", true));
                continue;
            }

            for (int sample = 0; sample < first.SampleCount; sample++)
            {
                int firstHeight = alongX ? first.HeightAt(first.SampleCount - 1, sample) : first.HeightAt(sample, first.SampleCount - 1);
                int secondHeight = alongX ? second.HeightAt(0, sample) : second.HeightAt(sample, 0);
                if (firstHeight != secondHeight)
                {
                    issues.Add(new ValidationIssue("terrain.seam-height", $"LOD {lodIndex} seam differs at sample {sample}: {firstHeight} != {secondHeight}.", true));
                    break;
                }

                TerrainSurfaceKind firstSurface = alongX ? first.SurfaceAt(first.SampleCount - 1, sample) : first.SurfaceAt(sample, first.SampleCount - 1);
                TerrainSurfaceKind secondSurface = alongX ? second.SurfaceAt(0, sample) : second.SurfaceAt(sample, 0);
                if (firstSurface != secondSurface)
                {
                    issues.Add(new ValidationIssue("terrain.seam-surface", $"LOD {lodIndex} seam surface differs at sample {sample}: {firstSurface} != {secondSurface}.", true));
                    break;
                }
            }
        }

        return new ValidationReport(issues.Count == 0, issues);
    }
}
