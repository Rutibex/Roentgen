using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

internal sealed class RenderTargetSet : IDisposable
{
    private int _width;
    private int _height;
    private int _shadowSize;

    public RenderTarget2D? SceneColor { get; private set; }
    public RenderTarget2D? ShadowMap { get; private set; }

    public void Ensure(GraphicsDevice graphicsDevice, int width, int height, int shadowSize)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (SceneColor is null || width != _width || height != _height)
        {
            SceneColor?.Dispose();
            SceneColor = new RenderTarget2D(
                graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.DiscardContents);
            _width = width;
            _height = height;
        }

        if (ShadowMap is null || shadowSize != _shadowSize)
        {
            ShadowMap?.Dispose();
            ShadowMap = new RenderTarget2D(
                graphicsDevice,
                shadowSize,
                shadowSize,
                false,
                SurfaceFormat.Color,
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.DiscardContents);
            _shadowSize = shadowSize;
        }
    }

    public void Dispose()
    {
        SceneColor?.Dispose();
        ShadowMap?.Dispose();
        SceneColor = null;
        ShadowMap = null;
    }
}
