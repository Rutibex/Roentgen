using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roentgen.Assets;

public static class AssetRecipeJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string SerializeMesh(MeshRecipe recipe) => JsonSerializer.Serialize(recipe, Options);
    public static string SerializeMaterial(MaterialRecipe recipe) => JsonSerializer.Serialize(recipe, Options);

    public static MeshRecipe DeserializeMesh(string json)
    {
        MeshRecipe recipe = JsonSerializer.Deserialize<MeshRecipe>(json, Options) ?? throw new JsonException("Mesh recipe was null.");
        AssetRecipeValidator.Validate(recipe).ThrowIfInvalid();
        return recipe;
    }

    public static MaterialRecipe DeserializeMaterial(string json)
    {
        MaterialRecipe recipe = JsonSerializer.Deserialize<MaterialRecipe>(json, Options) ?? throw new JsonException("Material recipe was null.");
        AssetRecipeValidator.Validate(recipe).ThrowIfInvalid();
        return recipe;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            PropertyNameCaseInsensitive = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
