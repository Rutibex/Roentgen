using Roentgen.Core;

namespace Roentgen.Assets;

public static class RecipeFingerprint
{
    public static StateChecksum Compute(ICanonicalWritable recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var writer = new CanonicalWriter();
        recipe.WriteCanonical(writer);
        return writer.ComputeChecksum();
    }


    public static StateChecksum ComputeCompilationKey(ICanonicalWritable recipe, string compilerId)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(compilerId);
        var writer = new CanonicalWriter();
        writer.WriteString(compilerId);
        recipe.WriteCanonical(writer);
        return writer.ComputeChecksum();
    }

    public static StateChecksum ComputeContent(ICanonicalWritable compiled)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        var writer = new CanonicalWriter();
        compiled.WriteCanonical(writer);
        return writer.ComputeChecksum();
    }
}
