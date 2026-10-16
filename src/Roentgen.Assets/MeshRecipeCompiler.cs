using System.Numerics;
using Roentgen.Core;

namespace Roentgen.Assets;

public sealed record MeshCompilationOptions(
    int MaximumVertices = 1_000_000,
    int MaximumIndices = 3_000_000,
    bool RecalculateNormals = true,
    bool GenerateCollision = true)
{
    public void Validate()
    {
        if (MaximumVertices < 3 || MaximumIndices < 3) throw new ArgumentOutOfRangeException(nameof(MaximumVertices));
    }
}

public sealed class MeshRecipeCompiler
{
    public const string CompilerId = "roentgen.mesh-compiler.v1";

    public CompiledMeshAsset Compile(MeshRecipe recipe, MeshCompilationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        options ??= new MeshCompilationOptions();
        options.Validate();
        AssetRecipeValidator.Validate(recipe).ThrowIfInvalid();
        StateChecksum recipeFingerprint = RecipeFingerprint.Compute(recipe);
        StateChecksum compilationKey = RecipeFingerprint.ComputeCompilationKey(recipe, CompilerId);
        var lods = new List<CompiledMeshLod>(recipe.LodCount);
        var partBounds = new List<AssetBounds>();
        for (int level = 0; level < recipe.LodCount; level++)
        {
            CompiledMeshLod lod = CompileLod(recipe, level, options, level == 0 ? partBounds : null);
            lods.Add(lod);
        }

        CollisionProxyAsset collision = options.GenerateCollision
            ? CollisionProxyBuilder.Build(recipe.Collision, lods[0].Bounds, partBounds)
            : CollisionProxyBuilder.Build(CollisionProxyKind.None, lods[0].Bounds, []);
        var provisional = new CompiledMeshAsset(recipe.Id, MeshRecipe.CurrentSchemaVersion, CompilerId, recipeFingerprint, compilationKey, StateChecksum.Zero, lods, collision);
        StateChecksum content = RecipeFingerprint.ComputeContent(provisional);
        return provisional with { ContentChecksum = content };
    }

    private static CompiledMeshLod CompileLod(MeshRecipe recipe, int level, MeshCompilationOptions options, List<AssetBounds>? partBounds)
    {
        var vertices = new List<MutableVertex>();
        var indices = new List<int>();
        int divisor = 1 << level;
        foreach (MeshPartRecipe part in recipe.Parts.OrderBy(static item => item.Id, StringComparer.Ordinal))
        {
            int availableDetail = recipe.LodCount - 1 - level;
            if (part.MinimumLod > availableDetail) continue;
            var localVertices = new List<MutableVertex>();
            var localIndices = new List<int>();
            int segments = Math.Max(3, part.Segments / divisor);
            int rings = Math.Max(2, part.Rings / divisor);
            BuildPrimitive(part, segments, rings, localVertices, localIndices);
            ApplyModifiers(localVertices, localIndices, part.Modifiers);
            CompactVertices(localVertices, localIndices);
            if (localIndices.Count == 0) continue;
            ApplyTransform(localVertices, part.Transform);
            if (part.UvProjection != UvProjectionKind.Primitive) ApplyUvProjection(localVertices, part.UvProjection);
            RecalculateNormals(localVertices, localIndices);
            AssetBounds bounds = CalculateBounds(localVertices);
            partBounds?.Add(bounds);
            int vertexOffset = vertices.Count;
            vertices.AddRange(localVertices);
            indices.AddRange(localIndices.Select(index => checked(index + vertexOffset)));
            if (vertices.Count > options.MaximumVertices || indices.Count > options.MaximumIndices)
            {
                throw new InvalidOperationException($"Compiled mesh '{recipe.Id}' exceeded configured topology budget.");
            }
        }

        if (vertices.Count < 3 || indices.Count < 3) throw new InvalidOperationException($"LOD {level} of '{recipe.Id}' produced no triangles.");
        if (options.RecalculateNormals) RecalculateNormals(vertices, indices);
        AssetBounds finalBounds = CalculateBounds(vertices);
        CompiledAssetVertex[] compiled = vertices.Select(static vertex => new CompiledAssetVertex(
            AssetVector3.From(vertex.Position),
            AssetVector3.From(Vector3.Normalize(vertex.Normal)),
            AssetVector2.From(vertex.Uv),
            vertex.Color,
            vertex.MaterialSlot)).ToArray();
        return new CompiledMeshLod(level, compiled, indices.ToArray(), finalBounds);
    }

    private static void BuildPrimitive(MeshPartRecipe part, int segments, int rings, List<MutableVertex> vertices, List<int> indices)
    {
        switch (part.Primitive)
        {
            case MeshPrimitiveKind.Box: BuildBox(part, vertices, indices); break;
            case MeshPrimitiveKind.Cylinder: BuildCylinder(part, segments, vertices, indices, 1f); break;
            case MeshPrimitiveKind.Cone: BuildCylinder(part, segments, vertices, indices, 0f); break;
            case MeshPrimitiveKind.UvSphere: BuildSphere(part, segments, rings, vertices, indices); break;
            case MeshPrimitiveKind.Wedge: BuildWedge(part, vertices, indices); break;
            case MeshPrimitiveKind.Lathe: BuildLathe(part, segments, vertices, indices); break;
            case MeshPrimitiveKind.Extrusion: BuildExtrusion(part, vertices, indices); break;
            case MeshPrimitiveKind.Tube: BuildTube(part, segments, vertices, indices); break;
            default: throw new ArgumentOutOfRangeException(nameof(part.Primitive));
        }
    }

    private static void BuildBox(MeshPartRecipe part, List<MutableVertex> vertices, List<int> indices)
    {
        Vector3 h = part.Size.ToNumerics() * 0.5f;
        AddQuad(vertices, indices, new(-h.X, -h.Y, h.Z), new(h.X, -h.Y, h.Z), new(h.X, h.Y, h.Z), new(-h.X, h.Y, h.Z), part);
        AddQuad(vertices, indices, new(h.X, -h.Y, -h.Z), new(-h.X, -h.Y, -h.Z), new(-h.X, h.Y, -h.Z), new(h.X, h.Y, -h.Z), part);
        AddQuad(vertices, indices, new(-h.X, -h.Y, -h.Z), new(-h.X, -h.Y, h.Z), new(-h.X, h.Y, h.Z), new(-h.X, h.Y, -h.Z), part);
        AddQuad(vertices, indices, new(h.X, -h.Y, h.Z), new(h.X, -h.Y, -h.Z), new(h.X, h.Y, -h.Z), new(h.X, h.Y, h.Z), part);
        AddQuad(vertices, indices, new(-h.X, h.Y, h.Z), new(h.X, h.Y, h.Z), new(h.X, h.Y, -h.Z), new(-h.X, h.Y, -h.Z), part);
        AddQuad(vertices, indices, new(-h.X, -h.Y, -h.Z), new(h.X, -h.Y, -h.Z), new(h.X, -h.Y, h.Z), new(-h.X, -h.Y, h.Z), part);
    }

    private static void BuildWedge(MeshPartRecipe part, List<MutableVertex> vertices, List<int> indices)
    {
        Vector3 h = part.Size.ToNumerics() * 0.5f;
        Vector3 a = new(-h.X, -h.Y, -h.Z); Vector3 b = new(h.X, -h.Y, -h.Z);
        Vector3 c = new(h.X, -h.Y, h.Z); Vector3 d = new(-h.X, -h.Y, h.Z);
        Vector3 e = new(-h.X, h.Y, h.Z); Vector3 f = new(h.X, h.Y, h.Z);
        AddQuad(vertices, indices, a, b, c, d, part);
        AddQuad(vertices, indices, d, c, f, e, part);
        AddQuad(vertices, indices, a, d, e, a, part, triangular: true);
        AddQuad(vertices, indices, b, f, c, b, part, triangular: true);
        AddQuad(vertices, indices, a, e, f, b, part);
    }

    private static void BuildCylinder(MeshPartRecipe part, int segments, List<MutableVertex> vertices, List<int> indices, float topScale)
    {
        float half = part.Height * 0.5f;
        for (int segment = 0; segment < segments; segment++)
        {
            float a0 = MathF.Tau * segment / segments;
            float a1 = MathF.Tau * (segment + 1) / segments;
            Vector3 p0 = new(MathF.Sin(a0) * part.Radius, -half, MathF.Cos(a0) * part.Radius);
            Vector3 p1 = new(MathF.Sin(a1) * part.Radius, -half, MathF.Cos(a1) * part.Radius);
            Vector3 p2 = new(MathF.Sin(a1) * part.Radius * topScale, half, MathF.Cos(a1) * part.Radius * topScale);
            Vector3 p3 = new(MathF.Sin(a0) * part.Radius * topScale, half, MathF.Cos(a0) * part.Radius * topScale);
            AddQuad(vertices, indices, p0, p1, p2, p3, part, u0: segment / (float)segments, u1: (segment + 1f) / segments);
        }

        AddFan(vertices, indices, Vector3.UnitY * -half, part.Radius, segments, false, part);
        if (topScale > 0.0001f) AddFan(vertices, indices, Vector3.UnitY * half, part.Radius * topScale, segments, true, part);
    }

    private static void BuildSphere(MeshPartRecipe part, int segments, int rings, List<MutableVertex> vertices, List<int> indices)
    {
        int start = vertices.Count;
        for (int ring = 0; ring <= rings; ring++)
        {
            float v = ring / (float)rings;
            float latitude = (v - 0.5f) * MathF.PI;
            float y = MathF.Sin(latitude) * part.Height * 0.5f;
            float radial = MathF.Cos(latitude) * part.Radius;
            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float angle = u * MathF.Tau;
                Vector3 position = new(MathF.Sin(angle) * radial, y, MathF.Cos(angle) * radial);
                vertices.Add(new MutableVertex(position, Vector3.Normalize(position), new Vector2(u, 1f - v), part.Color, checked((ushort)part.MaterialSlot)));
            }
        }

        int stride = segments + 1;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = start + ring * stride + segment;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                indices.AddRange([a, d, b, a, c, d]);
            }
        }
    }

    private static void BuildLathe(MeshPartRecipe part, int segments, List<MutableVertex> vertices, List<int> indices)
    {
        int start = vertices.Count;
        for (int row = 0; row < part.Profile.Count; row++)
        {
            AssetVector2 profile = part.Profile[row];
            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float angle = u * MathF.Tau;
                Vector3 position = new(MathF.Sin(angle) * profile.X, profile.Y, MathF.Cos(angle) * profile.X);
                vertices.Add(NewVertex(position, new Vector2(u, row / (float)Math.Max(1, part.Profile.Count - 1)), part));
            }
        }

        int stride = segments + 1;
        for (int row = 0; row < part.Profile.Count - 1; row++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = start + row * stride + segment;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                indices.AddRange([a, d, b, a, c, d]);
            }
        }
    }

    private static void BuildExtrusion(MeshPartRecipe part, List<MutableVertex> vertices, List<int> indices)
    {
        float half = part.Height * 0.5f;
        int count = part.Polygon.Count;
        int bottomStart = vertices.Count;
        for (int index = 0; index < count; index++)
        {
            AssetVector2 point = part.Polygon[index];
            vertices.Add(NewVertex(new Vector3(point.X, -half, point.Y), point.ToNumerics(), part));
        }
        int topStart = vertices.Count;
        for (int index = 0; index < count; index++)
        {
            AssetVector2 point = part.Polygon[index];
            vertices.Add(NewVertex(new Vector3(point.X, half, point.Y), point.ToNumerics(), part));
        }
        for (int index = 1; index < count - 1; index++)
        {
            indices.AddRange([bottomStart, bottomStart + index + 1, bottomStart + index]);
            indices.AddRange([topStart, topStart + index, topStart + index + 1]);
        }
        for (int index = 0; index < count; index++)
        {
            int next = (index + 1) % count;
            AddQuadByIndex(indices, bottomStart + index, bottomStart + next, topStart + next, topStart + index);
        }
    }

    private static void BuildTube(MeshPartRecipe part, int segments, List<MutableVertex> vertices, List<int> indices)
    {
        int start = vertices.Count;
        for (int pathIndex = 0; pathIndex < part.Path.Count; pathIndex++)
        {
            Vector3 center = part.Path[pathIndex].ToNumerics();
            Vector3 previous = part.Path[Math.Max(0, pathIndex - 1)].ToNumerics();
            Vector3 next = part.Path[Math.Min(part.Path.Count - 1, pathIndex + 1)].ToNumerics();
            Vector3 tangent = Vector3.Normalize(next - previous);
            Vector3 reference = MathF.Abs(Vector3.Dot(tangent, Vector3.UnitY)) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(reference, tangent));
            Vector3 up = Vector3.Normalize(Vector3.Cross(tangent, right));
            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float angle = u * MathF.Tau;
                Vector3 radial = right * MathF.Cos(angle) + up * MathF.Sin(angle);
                vertices.Add(new MutableVertex(center + radial * part.Radius, radial, new Vector2(u, pathIndex / (float)Math.Max(1, part.Path.Count - 1)), part.Color, checked((ushort)part.MaterialSlot)));
            }
        }

        int stride = segments + 1;
        for (int pathIndex = 0; pathIndex < part.Path.Count - 1; pathIndex++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = start + pathIndex * stride + segment;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                indices.AddRange([a, d, b, a, c, d]);
            }
        }
    }

    private static void ApplyModifiers(List<MutableVertex> vertices, List<int> indices, IReadOnlyList<MeshModifierRecipe> modifiers)
    {
        foreach (MeshModifierRecipe modifier in modifiers)
        {
            AssetBounds bounds = CalculateBounds(vertices);
            Vector3 min = bounds.Minimum.ToNumerics();
            Vector3 max = bounds.Maximum.ToNumerics();
            float rangeY = Math.Max(0.0001f, max.Y - min.Y);
            switch (modifier.Kind)
            {
                case MeshModifierKind.Translate:
                    for (int i = 0; i < vertices.Count; i++) vertices[i].Position += modifier.Vector.ToNumerics();
                    break;
                case MeshModifierKind.Scale:
                    Vector3 scale = modifier.Vector.ToNumerics();
                    for (int i = 0; i < vertices.Count; i++) vertices[i].Position *= scale;
                    break;
                case MeshModifierKind.RotateEulerDegrees:
                    ApplyMatrix(vertices, CreateRotation(modifier.Vector));
                    break;
                case MeshModifierKind.Taper:
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        float t = (vertices[i].Position.Y - min.Y) / rangeY;
                        float factor = 1f + modifier.Amount * t;
                        vertices[i].Position = new Vector3(vertices[i].Position.X * factor, vertices[i].Position.Y, vertices[i].Position.Z * factor);
                    }
                    break;
                case MeshModifierKind.TwistDegrees:
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        float t = (vertices[i].Position.Y - min.Y) / rangeY;
                        float radians = MathF.PI / 180f * modifier.Amount * t;
                        float cosine = MathF.Cos(radians); float sine = MathF.Sin(radians);
                        Vector3 p = vertices[i].Position;
                        vertices[i].Position = new Vector3(p.X * cosine - p.Z * sine, p.Y, p.X * sine + p.Z * cosine);
                    }
                    break;
                case MeshModifierKind.BendDegrees:
                    Bend(vertices, modifier.Amount, min.Y, max.Y);
                    break;
                case MeshModifierKind.NoiseDisplace:
                    NoiseDisplace(vertices, modifier.Amount, modifier.ScaleOrDefault(), modifier.Seed);
                    break;
                case MeshModifierKind.ClipBelow:
                    ClipTriangles(vertices, indices, modifier.Amount, keepAbove: true);
                    break;
                case MeshModifierKind.ClipAbove:
                    ClipTriangles(vertices, indices, modifier.Amount, keepAbove: false);
                    break;
            }
        }
    }

    private static void ApplyTransform(List<MutableVertex> vertices, AssetTransform transform)
    {
        Matrix4x4 matrix = Matrix4x4.CreateScale(transform.Scale.ToNumerics()) * CreateRotation(transform.RotationDegrees) * Matrix4x4.CreateTranslation(transform.Translation.ToNumerics());
        ApplyMatrix(vertices, matrix);
    }

    private static Matrix4x4 CreateRotation(AssetVector3 degrees)
    {
        const float factor = MathF.PI / 180f;
        return Matrix4x4.CreateRotationX(degrees.X * factor) * Matrix4x4.CreateRotationY(degrees.Y * factor) * Matrix4x4.CreateRotationZ(degrees.Z * factor);
    }

    private static void ApplyMatrix(List<MutableVertex> vertices, Matrix4x4 matrix)
    {
        for (int index = 0; index < vertices.Count; index++) vertices[index].Position = Vector3.Transform(vertices[index].Position, matrix);
    }

    private static void Bend(List<MutableVertex> vertices, float degrees, float minY, float maxY)
    {
        float length = Math.Max(0.0001f, maxY - minY);
        float total = degrees * MathF.PI / 180f;
        if (MathF.Abs(total) < 0.0001f) return;
        float radius = length / total;
        for (int index = 0; index < vertices.Count; index++)
        {
            Vector3 p = vertices[index].Position;
            float normalized = (p.Y - minY) / length;
            float angle = total * normalized;
            float radial = radius + p.X;
            vertices[index].Position = new Vector3(MathF.Sin(angle) * radial, minY + (1f - MathF.Cos(angle)) * radius, p.Z);
        }
    }

    private static void NoiseDisplace(List<MutableVertex> vertices, float amount, float scale, ulong seed)
    {
        for (int index = 0; index < vertices.Count; index++)
        {
            Vector3 p = vertices[index].Position;
            int x = (int)MathF.Floor(p.X * scale * 97f);
            int y = (int)MathF.Floor(p.Y * scale * 101f);
            int z = (int)MathF.Floor(p.Z * scale * 103f);
            float noise = HashNoise(x, y, z, seed) * 2f - 1f;
            Vector3 direction = p.LengthSquared() > 0.0001f ? Vector3.Normalize(p) : Vector3.UnitY;
            vertices[index].Position += direction * (noise * amount);
        }
    }

    private static float HashNoise(int x, int y, int z, ulong seed)
    {
        ulong value = seed ^ (uint)x * 0x9E3779B185EBCA87UL ^ (uint)y * 0xC2B2AE3D27D4EB4FUL ^ (uint)z * 0x165667B19E3779F9UL;
        value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL; value ^= value >> 27; value *= 0x94D049BB133111EBUL; value ^= value >> 31;
        return (value >> 40) * (1f / 16777216f);
    }

    private static void ClipTriangles(List<MutableVertex> vertices, List<int> indices, float y, bool keepAbove)
    {
        var filtered = new List<int>(indices.Count);
        for (int index = 0; index < indices.Count; index += 3)
        {
            bool keep = false;
            for (int corner = 0; corner < 3; corner++)
            {
                float value = vertices[indices[index + corner]].Position.Y;
                keep |= keepAbove ? value >= y : value <= y;
            }
            if (keep) filtered.AddRange([indices[index], indices[index + 1], indices[index + 2]]);
        }
        indices.Clear();
        indices.AddRange(filtered);
    }

    private static void CompactVertices(List<MutableVertex> vertices, List<int> indices)
    {
        if (indices.Count == 0)
        {
            vertices.Clear();
            return;
        }

        var remap = new Dictionary<int, int>();
        var compact = new List<MutableVertex>();
        for (int index = 0; index < indices.Count; index++)
        {
            int oldIndex = indices[index];
            if (!remap.TryGetValue(oldIndex, out int newIndex))
            {
                newIndex = compact.Count;
                remap.Add(oldIndex, newIndex);
                compact.Add(vertices[oldIndex]);
            }

            indices[index] = newIndex;
        }

        vertices.Clear();
        vertices.AddRange(compact);
    }

    private static void ApplyUvProjection(List<MutableVertex> vertices, UvProjectionKind projection)
    {
        AssetBounds bounds = CalculateBounds(vertices);
        Vector3 min = bounds.Minimum.ToNumerics();
        Vector3 size = bounds.Size.ToNumerics();
        size.X = Math.Max(size.X, 0.0001f); size.Y = Math.Max(size.Y, 0.0001f); size.Z = Math.Max(size.Z, 0.0001f);
        for (int index = 0; index < vertices.Count; index++)
        {
            Vector3 p = vertices[index].Position;
            vertices[index].Uv = projection switch
            {
                UvProjectionKind.PlanarX => new Vector2((p.Z - min.Z) / size.Z, 1f - (p.Y - min.Y) / size.Y),
                UvProjectionKind.PlanarY => new Vector2((p.X - min.X) / size.X, 1f - (p.Z - min.Z) / size.Z),
                UvProjectionKind.PlanarZ => new Vector2((p.X - min.X) / size.X, 1f - (p.Y - min.Y) / size.Y),
                UvProjectionKind.CylindricalY => new Vector2(MathF.Atan2(p.X, p.Z) / MathF.Tau + 0.5f, 1f - (p.Y - min.Y) / size.Y),
                UvProjectionKind.Spherical => SphericalUv(p),
                _ => vertices[index].Uv
            };
        }
    }

    private static Vector2 SphericalUv(Vector3 position)
    {
        Vector3 n = position.LengthSquared() > 0.0001f ? Vector3.Normalize(position) : Vector3.UnitY;
        return new Vector2(MathF.Atan2(n.X, n.Z) / MathF.Tau + 0.5f, MathF.Acos(Math.Clamp(n.Y, -1f, 1f)) / MathF.PI);
    }

    private static void RecalculateNormals(List<MutableVertex> vertices, IReadOnlyList<int> indices)
    {
        for (int index = 0; index < vertices.Count; index++) vertices[index].Normal = Vector3.Zero;
        for (int index = 0; index < indices.Count; index += 3)
        {
            int ia = indices[index]; int ib = indices[index + 1]; int ic = indices[index + 2];
            Vector3 a = vertices[ia].Position; Vector3 b = vertices[ib].Position; Vector3 c = vertices[ic].Position;
            Vector3 normal = Vector3.Cross(c - a, b - a);
            if (normal.LengthSquared() < 0.0000001f) continue;
            vertices[ia].Normal += normal; vertices[ib].Normal += normal; vertices[ic].Normal += normal;
        }
        for (int index = 0; index < vertices.Count; index++)
        {
            vertices[index].Normal = vertices[index].Normal.LengthSquared() < 0.000001f ? Vector3.UnitY : Vector3.Normalize(vertices[index].Normal);
        }
    }

    private static AssetBounds CalculateBounds(IReadOnlyList<MutableVertex> vertices)
    {
        Vector3 min = vertices[0].Position; Vector3 max = min;
        for (int index = 1; index < vertices.Count; index++) { min = Vector3.Min(min, vertices[index].Position); max = Vector3.Max(max, vertices[index].Position); }
        return new AssetBounds(AssetVector3.From(min), AssetVector3.From(max));
    }

    private static MutableVertex NewVertex(Vector3 position, Vector2 uv, MeshPartRecipe part) =>
        new(position, Vector3.UnitY, uv, part.Color, checked((ushort)part.MaterialSlot));

    private static void AddQuad(List<MutableVertex> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, MeshPartRecipe part, bool triangular = false, float u0 = 0f, float u1 = 1f)
    {
        int start = vertices.Count;
        vertices.Add(NewVertex(a, new Vector2(u0, 1f), part));
        vertices.Add(NewVertex(b, new Vector2(u1, 1f), part));
        vertices.Add(NewVertex(c, new Vector2(u1, 0f), part));
        if (!triangular) vertices.Add(NewVertex(d, new Vector2(u0, 0f), part));
        if (triangular) indices.AddRange([start, start + 2, start + 1]);
        else indices.AddRange([start, start + 2, start + 1, start, start + 3, start + 2]);
    }

    private static void AddFan(List<MutableVertex> vertices, List<int> indices, Vector3 center, float radius, int segments, bool top, MeshPartRecipe part)
    {
        int centerIndex = vertices.Count;
        vertices.Add(NewVertex(center, new Vector2(0.5f), part));
        int ringStart = vertices.Count;
        for (int segment = 0; segment < segments; segment++)
        {
            float angle = MathF.Tau * segment / segments;
            Vector3 p = new(MathF.Sin(angle) * radius, center.Y, MathF.Cos(angle) * radius);
            vertices.Add(NewVertex(p, new Vector2(p.X / radius * 0.5f + 0.5f, p.Z / radius * 0.5f + 0.5f), part));
        }
        for (int segment = 0; segment < segments; segment++)
        {
            int current = ringStart + segment; int next = ringStart + (segment + 1) % segments;
            if (top) indices.AddRange([centerIndex, current, next]); else indices.AddRange([centerIndex, next, current]);
        }
    }

    private static void AddQuadByIndex(List<int> indices, int a, int b, int c, int d) => indices.AddRange([a, c, b, a, d, c]);

    private sealed class MutableVertex(Vector3 position, Vector3 normal, Vector2 uv, AssetColor color, ushort materialSlot)
    {
        public Vector3 Position = position;
        public Vector3 Normal = normal;
        public Vector2 Uv = uv;
        public AssetColor Color = color;
        public ushort MaterialSlot = materialSlot;
    }
}

internal static class MeshModifierRecipeExtensions
{
    public static float ScaleOrDefault(this MeshModifierRecipe modifier) => MathF.Abs(modifier.Secondary) < 0.0001f ? 1f : modifier.Secondary;
}
