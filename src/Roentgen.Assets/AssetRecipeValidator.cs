namespace Roentgen.Assets;

public enum AssetValidationSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2
}

public sealed record AssetValidationIssue(AssetValidationSeverity Severity, string Path, string Message);

public sealed record AssetValidationReport(IReadOnlyList<AssetValidationIssue> Issues)
{
    public bool IsValid => Issues.All(static issue => issue.Severity != AssetValidationSeverity.Error);
    public void ThrowIfInvalid()
    {
        AssetValidationIssue? error = Issues.FirstOrDefault(static issue => issue.Severity == AssetValidationSeverity.Error);
        if (error is not null) throw new InvalidOperationException($"{error.Path}: {error.Message}");
    }
}

public static class AssetRecipeValidator
{
    public static AssetValidationReport Validate(MeshRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var issues = new List<AssetValidationIssue>();
        TryValidate(recipe.Validate, "$", issues);

        IReadOnlyList<MeshPartRecipe>? parts = recipe.Parts;
        if (parts is null) return new AssetValidationReport(issues);

        if (parts.Count > 256)
        {
            issues.Add(new AssetValidationIssue(AssetValidationSeverity.Warning, "$.parts", "Recipe contains more than 256 parts."));
        }

        if (recipe.LodCount > 1 && parts.Count > 0 &&
            parts.All(static part => part is not null && part.MinimumLod == 0) &&
            parts.All(static part => part is not null && part.Segments <= 4))
        {
            issues.Add(new AssetValidationIssue(AssetValidationSeverity.Information, "$.lodCount", "Requested LODs may be topologically identical."));
        }

        foreach ((MeshPartRecipe? part, int index) in parts.Select(static (part, index) => (part, index)))
        {
            if (part is null) continue;
            if (part.Primitive is MeshPrimitiveKind.Extrusion && part.Polygon is not null)
            {
                float area = SignedArea(part.Polygon);
                if (area <= 0f)
                {
                    issues.Add(new AssetValidationIssue(AssetValidationSeverity.Error, $"$.parts[{index}].polygon", "Extrusion polygon must be counter-clockwise and non-degenerate."));
                }
                else if (!IsStrictlyConvex(part.Polygon))
                {
                    issues.Add(new AssetValidationIssue(AssetValidationSeverity.Error, $"$.parts[{index}].polygon", "Version 1 extrusion polygons must be strictly convex because the compiler uses deterministic fan triangulation."));
                }
            }

            if (part.MaterialSlot > 255)
            {
                issues.Add(new AssetValidationIssue(AssetValidationSeverity.Warning, $"$.parts[{index}].materialSlot", "Material slots above 255 increase batching pressure."));
            }
        }

        return new AssetValidationReport(issues);
    }

    public static AssetValidationReport Validate(MaterialRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var issues = new List<AssetValidationIssue>();
        TryValidate(recipe.Validate, "$", issues);
        if (recipe.Width > 0 && recipe.Height > 0)
        {
            long pixels = (long)recipe.Width * recipe.Height;
            if (pixels > 1024L * 1024L)
            {
                issues.Add(new AssetValidationIssue(AssetValidationSeverity.Warning, "$", "Material exceeds one megapixel and should be reserved for exceptional assets."));
            }
        }

        if (recipe.Layers is { Count: > 64 })
        {
            issues.Add(new AssetValidationIssue(AssetValidationSeverity.Warning, "$.layers", "More than 64 layers may violate generation budgets."));
        }

        return new AssetValidationReport(issues);
    }

    private static void TryValidate(Action validate, string path, List<AssetValidationIssue> issues)
    {
        try { validate(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            issues.Add(new AssetValidationIssue(AssetValidationSeverity.Error, path, exception.Message));
        }
    }

    private static float SignedArea(IReadOnlyList<AssetVector2> polygon)
    {
        float area = 0f;
        for (int index = 0; index < polygon.Count; index++)
        {
            AssetVector2 a = polygon[index];
            AssetVector2 b = polygon[(index + 1) % polygon.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        return area * 0.5f;
    }

    private static bool IsStrictlyConvex(IReadOnlyList<AssetVector2> polygon)
    {
        if (polygon.Count < 3) return false;
        const float epsilon = 1e-6f;
        for (int index = 0; index < polygon.Count; index++)
        {
            AssetVector2 a = polygon[index];
            AssetVector2 b = polygon[(index + 1) % polygon.Count];
            AssetVector2 c = polygon[(index + 2) % polygon.Count];
            float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (cross <= epsilon) return false;
        }

        return true;
    }
}
