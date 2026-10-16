using Roentgen.Assets;

namespace Roentgen.AssetLab;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Execute(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"AssetLab error: {exception.Message}");
            return 1;
        }
    }

    private static int Execute(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        Dictionary<string, string> options = ParseOptions(args.Skip(1));
        return args[0] switch
        {
            "catalog" => Catalog(),
            "generate-samples" => GenerateSamples(Get(options, "output", "samples/assets")),
            "validate-mesh" => ValidateMesh(Require(options, "input")),
            "validate-material" => ValidateMaterial(Require(options, "input")),
            "compile-mesh" => CompileMesh(Require(options, "input"), Require(options, "output"), options.GetValueOrDefault("obj")),
            "compile-material" => CompileMaterial(Require(options, "input"), Require(options, "output"), options.GetValueOrDefault("ppm")),
            "compile-catalog" => CompileCatalog(Get(options, "output", "artifacts/assetlab")),
            _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
        };
    }

    private static int Catalog()
    {
        Console.WriteLine("Meshes:");
        foreach ((string id, MeshRecipe recipe) in ProceduralAssetCatalog.Meshes)
        {
            Console.WriteLine($"  {id,-24} {recipe.Parts.Count,3} parts  {recipe.LodCount} LODs  {RecipeFingerprint.Compute(recipe)}");
        }
        Console.WriteLine("Materials:");
        foreach ((string id, MaterialRecipe recipe) in ProceduralAssetCatalog.Materials)
        {
            Console.WriteLine($"  {id,-24} {recipe.Width}x{recipe.Height}  {recipe.Layers.Count} layers  {RecipeFingerprint.Compute(recipe)}");
        }
        return 0;
    }

    private static int GenerateSamples(string output)
    {
        Directory.CreateDirectory(output);
        foreach ((string id, MeshRecipe recipe) in ProceduralAssetCatalog.Meshes)
        {
            File.WriteAllText(Path.Combine(output, id + ".mesh.json"), AssetRecipeJson.SerializeMesh(recipe));
        }
        foreach ((string id, MaterialRecipe recipe) in ProceduralAssetCatalog.Materials)
        {
            File.WriteAllText(Path.Combine(output, id + ".material.json"), AssetRecipeJson.SerializeMaterial(recipe));
        }
        Console.WriteLine($"Wrote {ProceduralAssetCatalog.Meshes.Count + ProceduralAssetCatalog.Materials.Count} recipes to {Path.GetFullPath(output)}.");
        return 0;
    }

    private static int ValidateMesh(string input)
    {
        MeshRecipe recipe = AssetRecipeJson.DeserializeMesh(File.ReadAllText(input));
        AssetValidationReport report = AssetRecipeValidator.Validate(recipe);
        PrintReport(report);
        return report.IsValid ? 0 : 2;
    }

    private static int ValidateMaterial(string input)
    {
        MaterialRecipe recipe = AssetRecipeJson.DeserializeMaterial(File.ReadAllText(input));
        AssetValidationReport report = AssetRecipeValidator.Validate(recipe);
        PrintReport(report);
        return report.IsValid ? 0 : 2;
    }

    private static int CompileMesh(string input, string output, string? obj)
    {
        MeshRecipe recipe = AssetRecipeJson.DeserializeMesh(File.ReadAllText(input));
        CompiledMeshAsset asset = new MeshRecipeCompiler().Compile(recipe);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllBytes(output, AssetBinaryPayload.Write(asset));
        if (!string.IsNullOrWhiteSpace(obj)) AssetExporters.WriteObj(asset, obj);
        Console.WriteLine($"Compiled {asset.Id}: {asset.Lods[0].Vertices.Count:N0} vertices, {asset.Lods[0].PrimitiveCount:N0} triangles, checksum {asset.ContentChecksum}.");
        return 0;
    }

    private static int CompileMaterial(string input, string output, string? ppm)
    {
        MaterialRecipe recipe = AssetRecipeJson.DeserializeMaterial(File.ReadAllText(input));
        CompiledMaterialAsset asset = new MaterialRecipeCompiler().Compile(recipe);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllBytes(output, AssetBinaryPayload.Write(asset));
        if (!string.IsNullOrWhiteSpace(ppm)) AssetExporters.WritePpm(asset.Albedo, ppm);
        Console.WriteLine($"Compiled {asset.Id}: {asset.Albedo.Width}x{asset.Albedo.Height}, checksum {asset.ContentChecksum}.");
        return 0;
    }

    private static int CompileCatalog(string output)
    {
        Directory.CreateDirectory(output);
        var meshCompiler = new MeshRecipeCompiler();
        var materialCompiler = new MaterialRecipeCompiler();
        foreach ((string id, MeshRecipe recipe) in ProceduralAssetCatalog.Meshes)
        {
            CompiledMeshAsset asset = meshCompiler.Compile(recipe);
            File.WriteAllBytes(Path.Combine(output, id + ".mesh.rgasset"), AssetBinaryPayload.Write(asset));
            AssetExporters.WriteObj(asset, Path.Combine(output, id + ".obj"));
        }
        var materials = new List<CompiledMaterialAsset>();
        foreach ((string id, MaterialRecipe recipe) in ProceduralAssetCatalog.Materials)
        {
            CompiledMaterialAsset asset = materialCompiler.Compile(recipe);
            materials.Add(asset);
            File.WriteAllBytes(Path.Combine(output, id + ".material.rgasset"), AssetBinaryPayload.Write(asset));
            AssetExporters.WritePpm(asset.Albedo, Path.Combine(output, id + ".ppm"));
        }
        CompiledMaterialAtlas atlas = new MaterialAtlasBuilder().Build(materials);
        AssetExporters.WritePpm(atlas.Albedo, Path.Combine(output, "catalog-atlas.ppm"));
        Console.WriteLine($"Compiled catalog to {Path.GetFullPath(output)}; atlas {atlas.Width}x{atlas.Height}, checksum {atlas.ContentChecksum}.");
        return 0;
    }

    private static void PrintReport(AssetValidationReport report)
    {
        if (report.Issues.Count == 0) Console.WriteLine("Valid; no issues.");
        foreach (AssetValidationIssue issue in report.Issues) Console.WriteLine($"{issue.Severity}: {issue.Path}: {issue.Message}");
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> values)
    {
        string[] args = values.ToArray();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Expected option, received '{key}'.");
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Option '{key}' requires a value.");
            result[key[2..]] = args[++index];
        }
        return result;
    }

    private static string Require(IReadOnlyDictionary<string, string> options, string key) => options.TryGetValue(key, out string? value) ? value : throw new ArgumentException($"Missing --{key}.");
    private static string Get(IReadOnlyDictionary<string, string> options, string key, string fallback) => options.TryGetValue(key, out string? value) ? value : fallback;

    private static void PrintHelp()
    {
        Console.WriteLine("Roentgen AssetLab — deterministic procedural asset authoring");
        Console.WriteLine("  catalog");
        Console.WriteLine("  generate-samples [--output <folder>]");
        Console.WriteLine("  validate-mesh --input <recipe.mesh.json>");
        Console.WriteLine("  validate-material --input <recipe.material.json>");
        Console.WriteLine("  compile-mesh --input <recipe> --output <asset> [--obj <preview.obj>]");
        Console.WriteLine("  compile-material --input <recipe> --output <asset> [--ppm <preview.ppm>]");
        Console.WriteLine("  compile-catalog [--output <folder>]");
    }
}
