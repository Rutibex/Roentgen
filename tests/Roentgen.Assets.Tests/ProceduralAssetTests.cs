using Roentgen.Core;

namespace Roentgen.Assets.Tests;

public sealed class ProceduralAssetTests
{
    [Fact]
    public void CatalogRecipesValidate()
    {
        Assert.All(ProceduralAssetCatalog.Meshes.Values, recipe => Assert.True(AssetRecipeValidator.Validate(recipe).IsValid));
        Assert.All(ProceduralAssetCatalog.Materials.Values, recipe => Assert.True(AssetRecipeValidator.Validate(recipe).IsValid));
    }

    [Fact]
    public void CatalogContainsRepresentativeVehicle()
    {
        Assert.True(ProceduralAssetCatalog.Meshes.TryGetValue("inspection-cart", out MeshRecipe? vehicle));
        MeshRecipe actual = Assert.IsType<MeshRecipe>(vehicle);
        Assert.Equal("vehicle", actual.Metadata["family"]);
        Assert.True(new MeshRecipeCompiler().Compile(actual).Lods[0].PrimitiveCount > 0);
    }

    [Fact]
    public void HeadlessGoldenImageRendererMatchesFrozenWireframe()
    {
        CompiledAssetVertex Vertex(float x, float y) => new(
            new AssetVector3(x, y, 0f),
            new AssetVector3(0f, 0f, 1f),
            AssetVector2.Zero,
            AssetColor.White,
            0);

        var lod = new CompiledMeshLod(
            0,
            [Vertex(-1f, -1f), Vertex(1f, -1f), Vertex(1f, 1f), Vertex(-1f, 1f)],
            [0, 1, 2, 0, 2, 3],
            new AssetBounds(new AssetVector3(-1f, -1f, 0f), new AssetVector3(1f, 1f, 0f)));
        byte[] preview = AssetGoldenImageRenderer.RenderWireframe(lod, 32, 32, 2);
        Assert.Equal("65b6465cea0c7b7deedb4e84bdbdce62edd60c3b508fe123b6acc7dcd50210ba", AssetGoldenImageRenderer.ComputeSha256Hex(preview));
        Assert.Equal(114, preview.Count(static value => value != 0));
    }

    [Fact]
    public void RepresentativeVehicleProducesStableNonemptyVisualPreview()
    {
        CompiledMeshLod vehicle = new MeshRecipeCompiler().Compile(ProceduralAssetCatalog.Meshes["inspection-cart"]).Lods[0];
        byte[] first = AssetGoldenImageRenderer.RenderWireframe(vehicle);
        byte[] second = AssetGoldenImageRenderer.RenderWireframe(vehicle);
        Assert.Equal(first, second);
        Assert.Contains(first, static value => value != 0);
    }

    [Fact]
    public void MeshCompilationIsDeterministic()
    {
        var compiler = new MeshRecipeCompiler();
        MeshRecipe recipe = ProceduralAssetCatalog.Meshes["scrap-rifle"];
        CompiledMeshAsset first = compiler.Compile(recipe);
        CompiledMeshAsset second = compiler.Compile(recipe);
        Assert.Equal(first.RecipeFingerprint, second.RecipeFingerprint);
        Assert.Equal(first.ContentChecksum, second.ContentChecksum);
        Assert.Equal(first.Lods.Select(static lod => lod.Vertices.Count), second.Lods.Select(static lod => lod.Vertices.Count));
        Assert.Equal(3, first.Lods.Count);
    }

    [Fact]
    public void LodCompilerReducesSegmentedGeometry()
    {
        CompiledMeshAsset asset = new MeshRecipeCompiler().Compile(ProceduralAssetCatalog.Meshes["water-tower"]);
        Assert.True(asset.Lods[0].Vertices.Count > asset.Lods[^1].Vertices.Count);
        Assert.True(asset.Lods[0].PrimitiveCount > asset.Lods[^1].PrimitiveCount);
    }

    [Fact]
    public void MaterialCompilationIsDeterministic()
    {
        var compiler = new MaterialRecipeCompiler();
        MaterialRecipe recipe = ProceduralAssetCatalog.Materials["rusted-steel"];
        CompiledMaterialAsset first = compiler.Compile(recipe);
        CompiledMaterialAsset second = compiler.Compile(recipe);
        Assert.Equal(first.ContentChecksum, second.ContentChecksum);
        Assert.Equal(first.Albedo.Rgba, second.Albedo.Rgba);
        Assert.Contains(first.Albedo.Rgba, value => value != first.Albedo.Rgba[0]);
    }

    [Fact]
    public void AtlasPackingIsStableAcrossInputOrder()
    {
        var compiler = new MaterialRecipeCompiler();
        CompiledMaterialAsset[] materials = ProceduralAssetCatalog.Materials.Values.Select(compiler.Compile).ToArray();
        var builder = new MaterialAtlasBuilder();
        CompiledMaterialAtlas first = builder.Build(materials);
        CompiledMaterialAtlas second = builder.Build(materials.Reverse());
        Assert.Equal(first.ContentChecksum, second.ContentChecksum);
        Assert.Equal(first.Regions.Keys, second.Regions.Keys);
    }

    [Fact]
    public void JsonRoundTripPreservesFingerprint()
    {
        MeshRecipe source = ProceduralAssetCatalog.Meshes["dead-tree"];
        MeshRecipe restored = AssetRecipeJson.DeserializeMesh(AssetRecipeJson.SerializeMesh(source));
        Assert.Equal(RecipeFingerprint.Compute(source), RecipeFingerprint.Compute(restored));
    }


    [Fact]
    public void CompilerIdentityContributesToCompilationKey()
    {
        MeshRecipe recipe = ProceduralAssetCatalog.Meshes["scrap-rifle"];
        StateChecksum current = RecipeFingerprint.ComputeCompilationKey(recipe, MeshRecipeCompiler.CompilerId);
        StateChecksum hypotheticalUpgrade = RecipeFingerprint.ComputeCompilationKey(recipe, "roentgen.mesh-compiler.v2");
        Assert.NotEqual(current, hypotheticalUpgrade);
        Assert.Equal(current, new MeshRecipeCompiler().Compile(recipe).CompilationKey);
    }

    [Fact]
    public void PrimitiveTopologyMatchesFrozenVectors()
    {
        var compiler = new MeshRecipeCompiler();
        var box = new MeshRecipe("test-box", 1,
            [MeshPartRecipe.Box("box", new AssetVector3(2f, 2f, 2f), AssetColor.White)],
            CollisionProxyKind.AxisAlignedBox, 1, new Dictionary<string, string>(StringComparer.Ordinal));
        CompiledMeshLod boxLod = compiler.Compile(box).Lods[0];
        Assert.Equal(24, boxLod.Vertices.Count);
        Assert.Equal(36, boxLod.Indices.Count);
        Assert.Equal(12, boxLod.PrimitiveCount);

        var cylinder = new MeshRecipe("test-cylinder", 1,
            [MeshPartRecipe.Cylinder("cylinder", 1f, 2f, 12, AssetColor.White)],
            CollisionProxyKind.CapsuleY, 1, new Dictionary<string, string>(StringComparer.Ordinal));
        CompiledMeshLod cylinderLod = compiler.Compile(cylinder).Lods[0];
        Assert.Equal(74, cylinderLod.Vertices.Count);
        Assert.Equal(144, cylinderLod.Indices.Count);
        Assert.Equal(48, cylinderLod.PrimitiveCount);
    }

    [Fact]
    public void ExtrusionRejectsConcaveFanInput()
    {
        var part = new MeshPartRecipe("concave", MeshPrimitiveKind.Extrusion, AssetVector3.One, 0f, 1f, 4, 2, [],
            [new AssetVector2(0f, 0f), new AssetVector2(2f, 0f), new AssetVector2(1f, 0.5f), new AssetVector2(2f, 2f), new AssetVector2(0f, 2f)],
            [], AssetTransform.Identity, AssetColor.White, 0, 0, UvProjectionKind.Primitive, []);
        var recipe = new MeshRecipe("concave", 1, [part], CollisionProxyKind.AxisAlignedBox, 1, new Dictionary<string, string>(StringComparer.Ordinal));
        AssetValidationReport report = AssetRecipeValidator.Validate(recipe);
        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Message.Contains("strictly convex", StringComparison.Ordinal));
    }

    [Fact]
    public void MemoryCacheEvictsLeastRecentlyUsedAsset()
    {
        var cache = new CompiledAssetCache<string>(10);
        StateChecksum a = new(1, 0, 0, 0); StateChecksum b = new(2, 0, 0, 0); StateChecksum c = new(3, 0, 0, 0);
        cache.Add(a, "a", 4); cache.Add(b, "b", 4);
        Assert.True(cache.TryGet(a, out _));
        cache.Add(c, "c", 4);
        Assert.True(cache.TryGet(a, out _));
        Assert.False(cache.TryGet(b, out _));
        Assert.True(cache.TryGet(c, out _));
    }

    [Fact]
    public void DiskCacheRejectsCorruptPayload()
    {
        string root = Path.Combine(Path.GetTempPath(), "roentgen-assets-tests", Path.GetRandomFileName());
        try
        {
            var cache = new AssetDiskCache(root);
            StateChecksum key = new(7, 8, 9, 10);
            string path = cache.Store("mesh", "test", key, key, [1, 2, 3, 4]);
            Assert.True(cache.TryRead("mesh", key, out byte[] payload));
            Assert.Equal([1, 2, 3, 4], payload);
            File.WriteAllBytes(path, [9, 9, 9, 9]);
            Assert.False(cache.TryRead("mesh", key, out _));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
