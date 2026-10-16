namespace Roentgen.Assets;

public static class ProceduralAssetCatalog
{
    public static IReadOnlyDictionary<string, MeshRecipe> Meshes { get; } = BuildMeshes();
    public static IReadOnlyDictionary<string, MaterialRecipe> Materials { get; } = BuildMaterials();

    private static IReadOnlyDictionary<string, MeshRecipe> BuildMeshes()
    {
        AssetColor steel = new(132, 120, 104);
        AssetColor darkSteel = new(76, 72, 67);
        AssetColor wood = new(111, 74, 49);
        AssetColor concrete = new(151, 151, 142);
        AssetColor phosphor = new(72, 210, 166);
        var meshes = new SortedDictionary<string, MeshRecipe>(StringComparer.Ordinal)
        {
            ["scrap-rifle"] = new MeshRecipe(
                "scrap-rifle", 1,
                [
                    MeshPartRecipe.Box("receiver", new AssetVector3(0.55f, 0.17f, 0.14f), darkSteel).At(new AssetVector3(0f, 0.04f, 0f)),
                    MeshPartRecipe.Cylinder("barrel", 0.035f, 0.72f, 12, steel).Rotate(new AssetVector3(90f, 0f, 0f)).At(new AssetVector3(0f, 0.06f, -0.55f)),
                    MeshPartRecipe.Box("stock", new AssetVector3(0.24f, 0.18f, 0.42f), wood).At(new AssetVector3(0f, -0.01f, 0.42f)).Rotate(new AssetVector3(-9f, 0f, 0f)).WithModifiers(new MeshModifierRecipe(MeshModifierKind.Taper, AssetVector3.Zero, -0.35f)),
                    MeshPartRecipe.Box("grip", new AssetVector3(0.11f, 0.28f, 0.10f), wood).At(new AssetVector3(0f, -0.18f, 0.12f)).Rotate(new AssetVector3(-15f, 0f, 0f)),
                    MeshPartRecipe.Box("sight", new AssetVector3(0.06f, 0.06f, 0.16f), steel, minimumLod: 1).At(new AssetVector3(0f, 0.17f, -0.05f))
                ],
                CollisionProxyKind.CompoundBoxes, 3,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["family"] = "weapon", ["style"] = "improvised" }),

            ["diagnostic-terminal"] = new MeshRecipe(
                "diagnostic-terminal", 1,
                [
                    MeshPartRecipe.Box("body", new AssetVector3(1.3f, 1.55f, 0.75f), concrete),
                    MeshPartRecipe.Box("screen", new AssetVector3(0.92f, 0.58f, 0.05f), phosphor, 1).At(new AssetVector3(0f, 0.27f, -0.40f)).Rotate(new AssetVector3(-7f, 0f, 0f)),
                    MeshPartRecipe.Box("keyboard", new AssetVector3(0.95f, 0.12f, 0.42f), darkSteel).At(new AssetVector3(0f, -0.46f, -0.45f)).Rotate(new AssetVector3(12f, 0f, 0f)),
                    MeshPartRecipe.Cylinder("dial-left", 0.07f, 0.05f, 12, steel, minimumLod: 1).Rotate(new AssetVector3(90f, 0f, 0f)).At(new AssetVector3(-0.43f, -0.12f, -0.41f)),
                    MeshPartRecipe.Cylinder("dial-right", 0.07f, 0.05f, 12, steel, minimumLod: 1).Rotate(new AssetVector3(90f, 0f, 0f)).At(new AssetVector3(0.43f, -0.12f, -0.41f))
                ],
                CollisionProxyKind.AxisAlignedBox, 3,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["family"] = "machine", ["interaction"] = "terminal" }),

            ["water-tower"] = new MeshRecipe(
                "water-tower", 1,
                [
                    new MeshPartRecipe("tank", MeshPrimitiveKind.Lathe, AssetVector3.One, 1f, 1f, 24, 4,
                        [new AssetVector2(0.15f, -1.1f), new AssetVector2(1.25f, -0.8f), new AssetVector2(1.45f, 0f), new AssetVector2(1.25f, 0.8f), new AssetVector2(0.15f, 1.1f)], [], [],
                        AssetTransform.Identity, steel, 0, 0, UvProjectionKind.CylindricalY,
                        [new MeshModifierRecipe(MeshModifierKind.NoiseDisplace, AssetVector3.Zero, 0.025f, 2.5f, 0xA11CEUL)]).At(new AssetVector3(0f, 6.2f, 0f)),
                    MeshPartRecipe.Cylinder("leg-a", 0.09f, 6f, 8, darkSteel).At(new AssetVector3(-0.78f, 3f, -0.78f)).Rotate(new AssetVector3(0f, 0f, -7f)),
                    MeshPartRecipe.Cylinder("leg-b", 0.09f, 6f, 8, darkSteel).At(new AssetVector3(0.78f, 3f, -0.78f)).Rotate(new AssetVector3(0f, 0f, 7f)),
                    MeshPartRecipe.Cylinder("leg-c", 0.09f, 6f, 8, darkSteel).At(new AssetVector3(-0.78f, 3f, 0.78f)).Rotate(new AssetVector3(0f, 0f, -7f)),
                    MeshPartRecipe.Cylinder("leg-d", 0.09f, 6f, 8, darkSteel).At(new AssetVector3(0.78f, 3f, 0.78f)).Rotate(new AssetVector3(0f, 0f, 7f)),
                    MeshPartRecipe.Cylinder("pipe", 0.08f, 5.2f, 8, steel, minimumLod: 1).At(new AssetVector3(0f, 3.6f, 0f))
                ],
                CollisionProxyKind.CompoundBoxes, 4,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["family"] = "landmark", ["scale"] = "large" }),

            ["concrete-barrier"] = new MeshRecipe(
                "concrete-barrier", 1,
                [
                    new MeshPartRecipe("barrier", MeshPrimitiveKind.Wedge, new AssetVector3(2.4f, 1.15f, 0.72f), 0f, 0f, 4, 2, [], [], [], AssetTransform.Identity,
                        concrete, 0, 0, UvProjectionKind.PlanarZ,
                        [new MeshModifierRecipe(MeshModifierKind.NoiseDisplace, AssetVector3.Zero, 0.035f, 3.2f, 0xB4AUL)])
                ],
                CollisionProxyKind.AxisAlignedBox, 2,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["family"] = "cover" }),

            ["inspection-cart"] = new MeshRecipe(
                "inspection-cart", 1,
                [
                    MeshPartRecipe.Box("chassis", new AssetVector3(2.8f, 0.42f, 1.55f), darkSteel).At(new AssetVector3(0f, 0.72f, 0f)),
                    MeshPartRecipe.Box("cab", new AssetVector3(1.15f, 1.25f, 1.38f), steel).At(new AssetVector3(-0.55f, 1.48f, 0f)),
                    MeshPartRecipe.Box("equipment-bed", new AssetVector3(1.15f, 0.72f, 1.38f), concrete).At(new AssetVector3(0.88f, 1.15f, 0f)),
                    MeshPartRecipe.Box("diagnostic-panel", new AssetVector3(0.08f, 0.46f, 0.74f), phosphor, minimumLod: 1).At(new AssetVector3(-1.17f, 1.58f, 0f)),
                    MeshPartRecipe.Cylinder("wheel-front-left", 0.38f, 0.22f, 12, darkSteel).Rotate(new AssetVector3(0f, 0f, 90f)).At(new AssetVector3(-0.82f, 0.42f, -0.82f)),
                    MeshPartRecipe.Cylinder("wheel-front-right", 0.38f, 0.22f, 12, darkSteel).Rotate(new AssetVector3(0f, 0f, 90f)).At(new AssetVector3(-0.82f, 0.42f, 0.82f)),
                    MeshPartRecipe.Cylinder("wheel-rear-left", 0.38f, 0.22f, 12, darkSteel).Rotate(new AssetVector3(0f, 0f, 90f)).At(new AssetVector3(0.88f, 0.42f, -0.82f)),
                    MeshPartRecipe.Cylinder("wheel-rear-right", 0.38f, 0.22f, 12, darkSteel).Rotate(new AssetVector3(0f, 0f, 90f)).At(new AssetVector3(0.88f, 0.42f, 0.82f))
                ],
                CollisionProxyKind.CompoundBoxes, 3,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["family"] = "vehicle", ["role"] = "industrial-inspection" }),

            ["dead-tree"] = new MeshRecipe(
                "dead-tree", 1,
                [
                    new MeshPartRecipe("trunk", MeshPrimitiveKind.Tube, AssetVector3.One, 0.23f, 1f, 10, 2, [], [],
                        [new AssetVector3(0f, 0f, 0f), new AssetVector3(0.1f, 1.8f, 0f), new AssetVector3(-0.18f, 3.5f, 0.12f), new AssetVector3(0.05f, 5.2f, -0.08f)],
                        AssetTransform.Identity, wood, 0, 0, UvProjectionKind.Primitive,
                        [new MeshModifierRecipe(MeshModifierKind.NoiseDisplace, AssetVector3.Zero, 0.06f, 4f, 0xD34DUL)]),
                    new MeshPartRecipe("branch-a", MeshPrimitiveKind.Tube, AssetVector3.One, 0.11f, 1f, 8, 2, [], [],
                        [new AssetVector3(0f, 3.1f, 0f), new AssetVector3(1.0f, 4.0f, 0.15f), new AssetVector3(1.7f, 4.45f, -0.1f)], AssetTransform.Identity, wood, 0, 0, UvProjectionKind.Primitive, []),
                    new MeshPartRecipe("branch-b", MeshPrimitiveKind.Tube, AssetVector3.One, 0.09f, 1f, 8, 2, [], [],
                        [new AssetVector3(-0.1f, 4.0f, 0f), new AssetVector3(-0.9f, 4.65f, -0.2f), new AssetVector3(-1.45f, 4.9f, 0.15f)], AssetTransform.Identity, wood, 0, 1, UvProjectionKind.Primitive, [])
                ],
                CollisionProxyKind.CapsuleY, 3,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["family"] = "vegetation" })
        };
        return meshes;
    }

    private static IReadOnlyDictionary<string, MaterialRecipe> BuildMaterials()
    {
        var result = new SortedDictionary<string, MaterialRecipe>(StringComparer.Ordinal)
        {
            ["rusted-steel"] = new MaterialRecipe("rusted-steel", 1, 128, 128, 0.72f, 0.42f, 0f, true,
                [
                    new MaterialLayerRecipe("base", MaterialLayerKind.Solid, MaterialChannel.Albedo, new AssetColor(91, 91, 85), new AssetColor(91, 91, 85), 1f, 1f, 0f, 0f, 1),
                    new MaterialLayerRecipe("grain", MaterialLayerKind.ValueNoise, MaterialChannel.Albedo, new AssetColor(62, 62, 58), new AssetColor(123, 116, 102), 0.32f, 12f, 0f, 0f, 3),
                    new MaterialLayerRecipe("rust", MaterialLayerKind.RustSpots, MaterialChannel.Albedo, new AssetColor(99, 43, 25), new AssetColor(174, 83, 39), 0.84f, 7f, 0.58f, 0f, 19),
                    new MaterialLayerRecipe("scratches", MaterialLayerKind.Scratches, MaterialChannel.Albedo, new AssetColor(188, 179, 154), AssetColor.White, 0.52f, 40f, 0.83f, 11f, 23),
                    new MaterialLayerRecipe("rough", MaterialLayerKind.RustSpots, MaterialChannel.Roughness, new AssetColor(244, 0, 0), AssetColor.White, 0.78f, 7f, 0.52f, 0f, 19)
                ], new Dictionary<string, string>(StringComparer.Ordinal) { ["category"] = "metal" }),

            ["weathered-concrete"] = new MaterialRecipe("weathered-concrete", 1, 128, 128, 0.92f, 0f, 0f, true,
                [
                    new MaterialLayerRecipe("base", MaterialLayerKind.Solid, MaterialChannel.Albedo, new AssetColor(153, 153, 143), new AssetColor(153, 153, 143), 1f, 1f, 0f, 0f, 1),
                    new MaterialLayerRecipe("aggregate", MaterialLayerKind.Speckles, MaterialChannel.Albedo, new AssetColor(73, 74, 70), AssetColor.White, 0.5f, 1f, 0.975f, 0f, 55),
                    new MaterialLayerRecipe("cloud", MaterialLayerKind.ValueNoise, MaterialChannel.Albedo, new AssetColor(112, 114, 108), new AssetColor(181, 180, 165), 0.34f, 9f, 0f, 0f, 8),
                    new MaterialLayerRecipe("wear", MaterialLayerKind.EdgeWear, MaterialChannel.Albedo, new AssetColor(204, 199, 178), AssetColor.White, 0.26f, 1f, 0.09f, 0f, 5)
                ], new Dictionary<string, string>(StringComparer.Ordinal) { ["category"] = "masonry" }),

            ["diagnostic-phosphor"] = new MaterialRecipe("diagnostic-phosphor", 1, 64, 64, 0.22f, 0.08f, 1.8f, true,
                [
                    new MaterialLayerRecipe("base", MaterialLayerKind.Solid, MaterialChannel.Albedo, new AssetColor(18, 63, 51), new AssetColor(18, 63, 51), 1f, 1f, 0f, 0f, 1),
                    new MaterialLayerRecipe("scanlines", MaterialLayerKind.WarningStripes, MaterialChannel.Albedo, new AssetColor(64, 216, 170), new AssetColor(24, 97, 77), 0.54f, 64f, 0.33f, 90f, 6),
                    new MaterialLayerRecipe("glow", MaterialLayerKind.ValueNoise, MaterialChannel.Emissive, new AssetColor(25, 113, 89), new AssetColor(99, 255, 205), 0.92f, 6f, 0f, 0f, 31)
                ], new Dictionary<string, string>(StringComparer.Ordinal) { ["category"] = "emissive" }),

            ["aged-wood"] = new MaterialRecipe("aged-wood", 1, 128, 128, 0.88f, 0f, 0f, true,
                [
                    new MaterialLayerRecipe("base", MaterialLayerKind.VerticalGradient, MaterialChannel.Albedo, new AssetColor(62, 39, 27), new AssetColor(116, 79, 47), 1f, 1f, 0f, 0f, 2),
                    new MaterialLayerRecipe("grain", MaterialLayerKind.WarningStripes, MaterialChannel.Albedo, new AssetColor(40, 25, 18), new AssetColor(151, 103, 60), 0.28f, 28f, 0.14f, 7f, 14),
                    new MaterialLayerRecipe("splinters", MaterialLayerKind.Scratches, MaterialChannel.Albedo, new AssetColor(185, 139, 82), AssetColor.White, 0.36f, 46f, 0.87f, 3f, 99)
                ], new Dictionary<string, string>(StringComparer.Ordinal) { ["category"] = "organic" })
        };
        return result;
    }
}
