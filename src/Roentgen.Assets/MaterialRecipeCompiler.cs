using Roentgen.Core;

namespace Roentgen.Assets;

public sealed class MaterialRecipeCompiler
{
    public const string CompilerId = "roentgen.material-compiler.v1";

    public CompiledMaterialAsset Compile(MaterialRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        AssetRecipeValidator.Validate(recipe).ThrowIfInvalid();
        int pixelCount = checked(recipe.Width * recipe.Height);
        byte[] albedo = new byte[checked(pixelCount * 4)];
        byte[] surface = new byte[checked(pixelCount * 4)];
        byte[] emissive = new byte[checked(pixelCount * 4)];
        Fill(albedo, AssetColor.White);
        FillChannel(surface, ToByte(recipe.Roughness), ToByte(recipe.Metallic), 128, 255);
        Fill(emissive, AssetColor.Black);

        foreach (MaterialLayerRecipe layer in recipe.Layers)
        {
            ApplyLayer(recipe, layer, albedo, surface, emissive);
        }

        StateChecksum recipeFingerprint = RecipeFingerprint.Compute(recipe);
        StateChecksum compilationKey = RecipeFingerprint.ComputeCompilationKey(recipe, CompilerId);
        var provisional = new CompiledMaterialAsset(
            recipe.Id,
            MaterialRecipe.CurrentSchemaVersion,
            CompilerId,
            recipeFingerprint,
            compilationKey,
            StateChecksum.Zero,
            recipe.Roughness,
            recipe.Metallic,
            recipe.EmissiveStrength,
            new CompiledTextureChannel(recipe.Width, recipe.Height, albedo),
            new CompiledTextureChannel(recipe.Width, recipe.Height, surface),
            new CompiledTextureChannel(recipe.Width, recipe.Height, emissive));
        return provisional with { ContentChecksum = RecipeFingerprint.ComputeContent(provisional) };
    }

    private static void ApplyLayer(MaterialRecipe recipe, MaterialLayerRecipe layer, byte[] albedo, byte[] surface, byte[] emissive)
    {
        for (int y = 0; y < recipe.Height; y++)
        {
            for (int x = 0; x < recipe.Width; x++)
            {
                float u = (x + 0.5f) / recipe.Width;
                float v = (y + 0.5f) / recipe.Height;
                float mask = EvaluateMask(layer, x, y, u, v, recipe.Width, recipe.Height, recipe.Tileable);
                float amount = Math.Clamp(mask * layer.Opacity, 0f, 1f);
                if (amount <= 0f) continue;
                int offset = (y * recipe.Width + x) * 4;
                switch (layer.Channel)
                {
                    case MaterialChannel.Albedo:
                        AssetColor color = AssetColor.Lerp(layer.Primary, layer.Secondary, EvaluateColorBlend(layer, x, y, u, v));
                        Blend(albedo, offset, color, amount);
                        break;
                    case MaterialChannel.Roughness:
                        BlendScalar(surface, offset, 0, layer.Primary.R, amount);
                        break;
                    case MaterialChannel.Metallic:
                        BlendScalar(surface, offset, 1, layer.Primary.R, amount);
                        break;
                    case MaterialChannel.Height:
                        BlendScalar(surface, offset, 2, layer.Primary.R, amount);
                        break;
                    case MaterialChannel.Emissive:
                        AssetColor glow = AssetColor.Lerp(layer.Primary, layer.Secondary, EvaluateColorBlend(layer, x, y, u, v));
                        Blend(emissive, offset, glow, amount);
                        break;
                }
            }
        }
    }

    private static float EvaluateMask(MaterialLayerRecipe layer, int x, int y, float u, float v, int width, int height, bool tileable)
    {
        float scale = Math.Max(0.0001f, layer.Scale);
        return layer.Kind switch
        {
            MaterialLayerKind.Solid => 1f,
            MaterialLayerKind.VerticalGradient => v,
            MaterialLayerKind.Checker => (((int)MathF.Floor(u * scale) + (int)MathF.Floor(v * scale)) & 1) == 0 ? 1f : 0f,
            MaterialLayerKind.ValueNoise => SmoothNoise(u * scale, v * scale, layer.Seed, tileable ? Math.Max(1, (int)scale) : 0),
            MaterialLayerKind.RustSpots => SmoothStep(layer.Threshold, 1f, FractalNoise(u * scale, v * scale, layer.Seed, tileable ? Math.Max(1, (int)scale) : 0)),
            MaterialLayerKind.Scratches => ScratchMask(layer, x, y, width, height),
            MaterialLayerKind.WarningStripes => StripeMask(layer, u, v),
            MaterialLayerKind.EdgeWear => EdgeMask(u, v, layer.Threshold),
            MaterialLayerKind.Speckles => Hash01(x, y, layer.Seed) >= layer.Threshold ? 1f : 0f,
            _ => 0f
        };
    }

    private static float EvaluateColorBlend(MaterialLayerRecipe layer, int x, int y, float u, float v) => layer.Kind switch
    {
        MaterialLayerKind.Solid => 0f,
        MaterialLayerKind.VerticalGradient => v,
        MaterialLayerKind.Checker => (((int)MathF.Floor(u * layer.Scale) + (int)MathF.Floor(v * layer.Scale)) & 1) == 0 ? 0f : 1f,
        MaterialLayerKind.ValueNoise or MaterialLayerKind.RustSpots => FractalNoise(u * layer.Scale, v * layer.Scale, layer.Seed, 0),
        MaterialLayerKind.WarningStripes => StripeMask(layer, u, v),
        _ => Hash01(x, y, layer.Seed)
    };

    private static float ScratchMask(MaterialLayerRecipe layer, int x, int y, int width, int height)
    {
        float radians = layer.AngleDegrees * MathF.PI / 180f;
        float coordinate = (x / (float)width * MathF.Cos(radians) + y / (float)height * MathF.Sin(radians)) * layer.Scale;
        float line = MathF.Abs(Fract(coordinate) - 0.5f) * 2f;
        float irregularity = Hash01(x / 3, y / 3, layer.Seed);
        return line > Math.Clamp(layer.Threshold + irregularity * 0.15f, 0f, 0.99f) ? 1f : 0f;
    }

    private static float StripeMask(MaterialLayerRecipe layer, float u, float v)
    {
        float radians = layer.AngleDegrees * MathF.PI / 180f;
        float coordinate = (u * MathF.Cos(radians) + v * MathF.Sin(radians)) * layer.Scale;
        return Fract(coordinate) < Math.Clamp(layer.Threshold, 0.05f, 0.95f) ? 1f : 0f;
    }

    private static float EdgeMask(float u, float v, float threshold)
    {
        float edge = MathF.Min(MathF.Min(u, 1f - u), MathF.Min(v, 1f - v));
        return 1f - SmoothStep(0f, Math.Clamp(threshold, 0.001f, 0.5f), edge);
    }

    private static float FractalNoise(float x, float y, ulong seed, int period)
    {
        float value = 0f;
        float amplitude = 0.5714286f;
        float normalization = 0f;
        for (int octave = 0; octave < 4; octave++)
        {
            value += SmoothNoise(x, y, seed + (ulong)octave * 0x9E3779B97F4A7C15UL, period) * amplitude;
            normalization += amplitude;
            x *= 2f; y *= 2f; amplitude *= 0.5f;
            if (period > 0) period *= 2;
        }
        return value / normalization;
    }

    private static float SmoothNoise(float x, float y, ulong seed, int period)
    {
        int x0 = (int)MathF.Floor(x); int y0 = (int)MathF.Floor(y);
        int x1 = x0 + 1; int y1 = y0 + 1;
        float tx = SmoothCurve(x - x0); float ty = SmoothCurve(y - y0);
        if (period > 0)
        {
            x0 = Mod(x0, period); x1 = Mod(x1, period); y0 = Mod(y0, period); y1 = Mod(y1, period);
        }
        float a = Hash01(x0, y0, seed); float b = Hash01(x1, y0, seed);
        float c = Hash01(x0, y1, seed); float d = Hash01(x1, y1, seed);
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private static float Hash01(int x, int y, ulong seed)
    {
        ulong value = seed ^ (uint)x * 0x9E3779B185EBCA87UL ^ (uint)y * 0xC2B2AE3D27D4EB4FUL;
        value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL; value ^= value >> 27; value *= 0x94D049BB133111EBUL; value ^= value >> 31;
        return (value >> 40) * (1f / 16777216f);
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
    private static float SmoothCurve(float value) => value * value * (3f - 2f * value);
    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0) return value >= edge1 ? 1f : 0f;
        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
    private static float Lerp(float a, float b, float amount) => a + (b - a) * amount;
    private static float Fract(float value) => value - MathF.Floor(value);
    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);

    private static void Fill(byte[] target, AssetColor color) => FillChannel(target, color.R, color.G, color.B, color.A);
    private static void FillChannel(byte[] target, byte r, byte g, byte b, byte a)
    {
        for (int offset = 0; offset < target.Length; offset += 4)
        {
            target[offset] = r; target[offset + 1] = g; target[offset + 2] = b; target[offset + 3] = a;
        }
    }

    private static void Blend(byte[] target, int offset, AssetColor color, float amount)
    {
        target[offset] = BlendByte(target[offset], color.R, amount);
        target[offset + 1] = BlendByte(target[offset + 1], color.G, amount);
        target[offset + 2] = BlendByte(target[offset + 2], color.B, amount);
        target[offset + 3] = BlendByte(target[offset + 3], color.A, amount);
    }

    private static void BlendScalar(byte[] target, int offset, int channel, byte value, float amount) => target[offset + channel] = BlendByte(target[offset + channel], value, amount);
    private static byte BlendByte(byte a, byte b, float amount) => (byte)Math.Clamp((int)MathF.Round(a + (b - a) * amount), 0, 255);
}
