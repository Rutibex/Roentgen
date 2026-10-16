using Roentgen.Animation;
using Roentgen.Contracts;

namespace Roentgen.Generation;

public sealed class CharacterGenerationRepairer : IRepairStage<GeneratedCharacterProduct>
{
    public string RepairId => "character-bounded-repair-v1";

    public RepairResult<GeneratedCharacterProduct> TryRepair(GeneratedCharacterProduct value, ValidationReport report, DomainSeed repairSeed)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(report);
        _ = repairSeed;
        if (report.IsValid)
        {
            return new RepairResult<GeneratedCharacterProduct>(false, value, "No repair required.");
        }

        // Mesh and skeleton constructors already reject unrecoverable structural corruption. The bounded
        // runtime repair normalizes skin weights and removes LOD products that do not pass validation.
        var validLods = new List<CharacterLodProduct>();
        foreach (CharacterLodProduct lod in value.Lods.OrderBy(static item => item.LodLevel))
        {
            try
            {
                CharacterMeshProduct body = Normalize(lod.Body);
                CharacterMeshProduct? clothing = lod.Clothing is null ? null : Normalize(lod.Clothing);
                validLods.Add(new CharacterLodProduct(body, clothing, lod.LodLevel));
            }
            catch (ArgumentException)
            {
                // The next lower-detail product is used as the deterministic fallback.
            }
        }

        if (validLods.Count == 0)
        {
            return new RepairResult<GeneratedCharacterProduct>(false, value, "No bounded repair candidate succeeded.");
        }

        GeneratedCharacterProduct repaired = value with { Lods = validLods };
        ValidationReport repairedReport = new CharacterProductValidator().Validate(repaired);
        return repairedReport.IsValid
            ? new RepairResult<GeneratedCharacterProduct>(true, repaired, "Normalized skin weights and removed invalid LOD products.")
            : new RepairResult<GeneratedCharacterProduct>(false, value, "Repair candidates did not satisfy validation.");
    }

    private static CharacterMeshProduct Normalize(CharacterMeshProduct source)
    {
        CharacterVertex[] vertices = source.Vertices.Select(static vertex => vertex with { Skin = vertex.Skin.Normalized() }).ToArray();
        return new CharacterMeshProduct(source.Name, vertices, source.Indices);
    }
}
