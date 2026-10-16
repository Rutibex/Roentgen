using System.Text;

namespace Roentgen.Assets;

public static class AssetExporters
{
    public static void WriteObj(CompiledMeshAsset mesh, string path, int lod = 0)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        CompiledMeshLod selected = mesh.Lods.Single(item => item.Level == lod);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine($"# Roentgen procedural mesh: {mesh.Id}");
        writer.WriteLine($"o {Sanitize(mesh.Id)}");
        foreach (CompiledAssetVertex vertex in selected.Vertices)
        {
            writer.WriteLine(FormattableString.Invariant($"v {vertex.Position.X:R} {vertex.Position.Y:R} {vertex.Position.Z:R}"));
        }
        foreach (CompiledAssetVertex vertex in selected.Vertices)
        {
            writer.WriteLine(FormattableString.Invariant($"vt {vertex.TextureCoordinate.X:R} {1f - vertex.TextureCoordinate.Y:R}"));
        }
        foreach (CompiledAssetVertex vertex in selected.Vertices)
        {
            writer.WriteLine(FormattableString.Invariant($"vn {vertex.Normal.X:R} {vertex.Normal.Y:R} {vertex.Normal.Z:R}"));
        }
        for (int index = 0; index < selected.Indices.Count; index += 3)
        {
            int a = selected.Indices[index] + 1; int b = selected.Indices[index + 1] + 1; int c = selected.Indices[index + 2] + 1;
            writer.WriteLine(FormattableString.Invariant($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}"));
        }
    }

    public static void WritePpm(CompiledTextureChannel texture, string path)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        byte[] header = Encoding.ASCII.GetBytes($"P6\n{texture.Width} {texture.Height}\n255\n");
        stream.Write(header);
        byte[] row = new byte[checked(texture.Width * 3)];
        for (int y = 0; y < texture.Height; y++)
        {
            for (int x = 0; x < texture.Width; x++)
            {
                int source = (y * texture.Width + x) * 4; int target = x * 3;
                row[target] = texture.Rgba[source]; row[target + 1] = texture.Rgba[source + 1]; row[target + 2] = texture.Rgba[source + 2];
            }
            stream.Write(row);
        }
    }

    private static string Sanitize(string value) => string.Concat(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_'));
}
