using System.Security.Cryptography;

namespace Roentgen.Assets;

/// <summary>
/// Produces a deterministic, platform-neutral wireframe preview for procedural-asset
/// visual regression tests. The preview deliberately avoids a graphics API so it can
/// run in headless CI before GPU-specific smoke tests.
/// </summary>
public static class AssetGoldenImageRenderer
{
    public static byte[] RenderWireframe(CompiledMeshLod lod, int width = 128, int height = 128, int margin = 4)
    {
        ArgumentNullException.ThrowIfNull(lod);
        if (width < 8 || height < 8 || margin < 0 || margin * 2 >= width - 1 || margin * 2 >= height - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Preview dimensions and margin are invalid.");
        }

        var projected = new ProjectedPoint[lod.Vertices.Count];
        float minimumX = float.PositiveInfinity;
        float minimumY = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float maximumY = float.NegativeInfinity;
        for (int index = 0; index < lod.Vertices.Count; index++)
        {
            AssetVector3 position = lod.Vertices[index].Position;
            float x = position.X - position.Z;
            float y = (position.X + position.Z) * 0.5f - position.Y;
            projected[index] = new ProjectedPoint(x, y);
            minimumX = MathF.Min(minimumX, x);
            minimumY = MathF.Min(minimumY, y);
            maximumX = MathF.Max(maximumX, x);
            maximumY = MathF.Max(maximumY, y);
        }

        float rangeX = MathF.Max(0.000001f, maximumX - minimumX);
        float rangeY = MathF.Max(0.000001f, maximumY - minimumY);
        int usableWidth = width - 1 - margin * 2;
        int usableHeight = height - 1 - margin * 2;
        var pixels = new byte[checked(width * height)];
        var screen = new ScreenPoint[projected.Length];
        for (int index = 0; index < projected.Length; index++)
        {
            ProjectedPoint point = projected[index];
            int x = margin + checked((int)MathF.Round((point.X - minimumX) / rangeX * usableWidth, MidpointRounding.AwayFromZero));
            int y = margin + checked((int)MathF.Round((maximumY - point.Y) / rangeY * usableHeight, MidpointRounding.AwayFromZero));
            screen[index] = new ScreenPoint(Math.Clamp(x, 0, width - 1), Math.Clamp(y, 0, height - 1));
        }

        for (int index = 0; index < lod.Indices.Count; index += 3)
        {
            ScreenPoint a = screen[lod.Indices[index]];
            ScreenPoint b = screen[lod.Indices[index + 1]];
            ScreenPoint c = screen[lod.Indices[index + 2]];
            DrawLine(pixels, width, height, a, b);
            DrawLine(pixels, width, height, b, c);
            DrawLine(pixels, width, height, c, a);
        }

        return pixels;
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> pixels) =>
        Convert.ToHexString(SHA256.HashData(pixels)).ToLowerInvariant();

    private static void DrawLine(byte[] pixels, int width, int height, ScreenPoint start, ScreenPoint end)
    {
        int x = start.X;
        int y = start.Y;
        int dx = Math.Abs(end.X - start.X);
        int stepX = start.X < end.X ? 1 : -1;
        int dy = -Math.Abs(end.Y - start.Y);
        int stepY = start.Y < end.Y ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            if (x >= 0 && y >= 0 && x < width && y < height)
            {
                pixels[y * width + x] = 255;
            }

            if (x == end.X && y == end.Y)
            {
                break;
            }

            int doubled = error * 2;
            if (doubled >= dy)
            {
                error += dy;
                x += stepX;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    private readonly record struct ProjectedPoint(float X, float Y);
    private readonly record struct ScreenPoint(int X, int Y);
}
