using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Roentgen.Diagnostics;
using Roentgen.Assets;

namespace Roentgen.Rendering;

public sealed class RendererFoundation : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ContentManager _content;
    private readonly RenderingOptions _options;
    private readonly IRoentgenLogger _logger;
    private readonly RenderThreadGuard _threadGuard;
    private readonly RenderTargetSet _targets = new();
    private readonly RenderQueueBuilder _queueBuilder = new();
    private readonly RenderResourceUploadQueue _uploadQueue;
    private readonly SpriteBatch _spriteBatch;
    private readonly DebugLineRenderer _debugLines;
    private readonly BasicEffect _fallbackEffect;
    private readonly RasterizerState _solidRasterizer;
    private readonly RasterizerState _doubleSidedRasterizer;
    private readonly Texture2D _whiteTexture;
    private readonly SurfaceTextureLibrary _surfaceTextures;
    private readonly Effect? _forwardEffect;
    private readonly Effect? _shadowEffect;
    private readonly Effect? _postEffect;
    private readonly Effect? _characterEffect;
    private readonly Effect? _characterShadowEffect;
    private long _frameNumber;
    private bool _disposed;

    public RendererFoundation(
        GraphicsDevice graphicsDevice,
        ContentManager content,
        RenderingOptions options,
        IRoentgenLogger logger,
        RenderResourceUploadQueue? uploadQueue = null,
        RenderThreadGuard? threadGuard = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        options.Validate();
        _graphicsDevice = graphicsDevice;
        _content = content;
        _options = options;
        _logger = logger;
        _uploadQueue = uploadQueue ?? new RenderResourceUploadQueue(options.MaximumPendingUploadOperations, options.MaximumPendingUploadBytes);
        _threadGuard = threadGuard ?? new RenderThreadGuard();
        _threadGuard.BindCurrentThread();
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _debugLines = new DebugLineRenderer(graphicsDevice);
        _fallbackEffect = new BasicEffect(graphicsDevice)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            TextureEnabled = true,
            VertexColorEnabled = true,
            FogEnabled = true
        };
        _solidRasterizer = new RasterizerState { CullMode = CullMode.CullCounterClockwiseFace, FillMode = FillMode.Solid };
        _doubleSidedRasterizer = new RasterizerState { CullMode = CullMode.None, FillMode = FillMode.Solid };
        _surfaceTextures = new SurfaceTextureLibrary(graphicsDevice);
        _whiteTexture = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _whiteTexture.SetData(new[] { Color.White });
        _forwardEffect = TryLoadEffect("Effects/ForwardLit");
        _shadowEffect = TryLoadEffect("Effects/ShadowDepth");
        _postEffect = TryLoadEffect("Effects/PostProcess");
        _characterEffect = TryLoadEffect("Effects/CharacterSkinning");
        _characterShadowEffect = TryLoadEffect("Effects/CharacterShadowSkinning");
        Telemetry = new RendererTelemetryCollector();
    }

    public RendererTelemetryCollector Telemetry { get; }
    public RenderResourceUploadQueue UploadQueue => _uploadQueue;
    public RenderThreadGuard ThreadGuard => _threadGuard;

    public GpuMesh UploadNow(MeshData meshData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GpuMesh.Upload(_graphicsDevice, _threadGuard, meshData);
    }

    public GpuMesh UploadNow(CompiledMeshAsset asset, int lod = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return UploadNow(ProceduralAssetRenderingBridge.ToMeshData(asset, lod));
    }

    public GpuMaterialAsset UploadNow(CompiledMaterialAsset asset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ProceduralAssetRenderingBridge.UploadMaterial(_graphicsDevice, _threadGuard, asset);
    }

    public Texture2D UploadNow(CompiledTextureChannel channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ProceduralAssetRenderingBridge.UploadTexture(_graphicsDevice, _threadGuard, channel);
    }

    public CharacterGpuAsset UploadCharacterNow(Roentgen.Animation.GeneratedCharacterProduct product)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(product);
        return CharacterGpuAsset.Upload(_graphicsDevice, _threadGuard, product);
    }

    public void Draw(RenderFrameSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);
        _threadGuard.AssertAccess();
        var stopwatch = Stopwatch.StartNew();
        Viewport viewport = _graphicsDevice.Viewport;
        if (viewport.Width < 1 || viewport.Height < 1)
        {
            // There is no drawable snapshot to protect when the viewport is unavailable,
            // so it is safe to continue servicing the resource queue.
            _ = DrainUploads();
            return;
        }

        _targets.Ensure(_graphicsDevice, viewport.Width, viewport.Height, _options.ShadowMapSize);
        float aspect = viewport.Width / (float)Math.Max(1, viewport.Height);
        RenderQueueResult queue = _queueBuilder.Build(snapshot, aspect, _options.EnableFrustumCulling);
        Matrix lightViewProjection = CreateLightViewProjection(snapshot);
        int shadowDraws = 0;
        if (_options.EnableShadows && _shadowEffect is not null && _targets.ShadowMap is not null)
        {
            shadowDraws = DrawShadowPass(queue.VisibleItems, lightViewProjection, _targets.ShadowMap);
            shadowDraws += DrawCharacterShadowPass(snapshot.CharacterItems, lightViewProjection);
        }

        RenderTarget2D? scene = _options.EnablePostProcessing ? _targets.SceneColor : null;
        _graphicsDevice.SetRenderTarget(scene);
        _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, snapshot.ClearColor, 1f, 0);
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
        int drawCalls = 0;
        int triangles = 0;
        foreach (RenderItem item in queue.VisibleItems)
        {
            // A snapshot can legitimately contain a mesh retired by an external owner
            // immediately before Draw.  Never bind a retired GPU resource.
            if (item.Mesh.IsDisposed)
            {
                continue;
            }

            DrawOpaqueItem(item, snapshot, queue.Camera, lightViewProjection);
            drawCalls++;
            triangles += item.Mesh.PrimitiveCount;
        }

        foreach (CharacterRenderItem character in snapshot.CharacterItems)
        {
            DrawCharacterItem(character, snapshot, queue.Camera, lightViewProjection);
            CharacterGpuLod lod = character.Asset.SelectLod(character.Lod);
            drawCalls++;
            triangles += lod.Body.Skinned.PrimitiveCount;
            if (lod.Clothing is not null)
            {
                drawCalls++;
                triangles += lod.Clothing.Skinned.PrimitiveCount;
            }
        }

        _debugLines.Draw(_graphicsDevice, snapshot.DebugLines, queue.Camera);
        if (scene is not null)
        {
            _graphicsDevice.SetRenderTarget(null);
            DrawPostProcess(scene);
        }

        // Resource uploads, LOD replacements, and unloads are allowed to dispose meshes.
        // Drain only after every pass has finished consuming this frame's snapshot.
        // Draining at the start of Draw could retire a mesh that scene.Build() had already
        // captured, producing an ObjectDisposedException in GpuMesh.Bind.
        RenderUploadDrainResult uploadResult = DrainUploads();

        stopwatch.Stop();
        _frameNumber++;
        Telemetry.Publish(new RendererTelemetry(
            _frameNumber,
            snapshot.OpaqueItems.Count + snapshot.CharacterItems.Count,
            queue.VisibleItems.Count + snapshot.CharacterItems.Count,
            queue.CulledItemCount,
            drawCalls,
            triangles,
            shadowDraws,
            uploadResult.Completed,
            uploadResult.Skipped,
            uploadResult.Failed,
            uploadResult.UploadedBytes,
            _uploadQueue.PendingCount,
            _uploadQueue.PendingBytes,
            _uploadQueue.Snapshot.RejectedCount,
            uploadResult.ElapsedMilliseconds,
            stopwatch.Elapsed.TotalMilliseconds,
            _options.EnableShadows && _shadowEffect is not null,
            scene is not null));
    }

    private RenderUploadDrainResult DrainUploads() => _uploadQueue.Drain(
        _graphicsDevice,
        _threadGuard,
        new RenderUploadBudget(
            _options.MaximumUploadsPerFrame,
            _options.MaximumUploadBytesPerFrame,
            _options.MaximumUploadMillisecondsPerFrame));

    private int DrawShadowPass(IReadOnlyList<RenderItem> items, Matrix lightViewProjection, RenderTarget2D shadowMap)
    {
        _graphicsDevice.SetRenderTarget(shadowMap);
        _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1f, 0);
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.RasterizerState = _solidRasterizer;
        int draws = 0;
        foreach (RenderItem item in items)
        {
            if (!item.CastsShadow || item.Mesh.IsDisposed) continue;
            item.Mesh.Bind(_graphicsDevice);
            EffectBindings.Set(_shadowEffect!, "World", item.World);
            EffectBindings.Set(_shadowEffect!, "LightViewProjection", lightViewProjection);
            foreach (EffectPass pass in _shadowEffect!.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, item.Mesh.PrimitiveCount);
            }

            draws++;
        }

        return draws;
    }

    private int DrawCharacterShadowPass(IReadOnlyList<CharacterRenderItem> characters, Matrix lightViewProjection)
    {
        int draws = 0;
        foreach (CharacterRenderItem item in characters)
        {
            if (!item.CastsShadow)
            {
                continue;
            }

            CharacterGpuLod lod = item.Asset.SelectLod(item.Lod);
            draws += DrawCharacterShadowMesh(lod.Body, item, lightViewProjection);
            if (lod.Clothing is not null)
            {
                draws += DrawCharacterShadowMesh(lod.Clothing, item, lightViewProjection);
            }
        }

        return draws;
    }

    private int DrawCharacterShadowMesh(CharacterGpuMeshPair mesh, CharacterRenderItem item, Matrix lightViewProjection)
    {
        if (_characterShadowEffect is not null)
        {
            mesh.Skinned.Bind(_graphicsDevice);
            EffectBindings.Set(_characterShadowEffect, "World", item.World);
            EffectBindings.Set(_characterShadowEffect, "LightViewProjection", lightViewProjection);
            EffectBindings.Set(_characterShadowEffect, "Bones", CharacterRenderingBridge.ToXnaMatrices(item.Pose.BuildSkinMatrices()));
            foreach (EffectPass pass in _characterShadowEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.Skinned.PrimitiveCount);
            }

            return 1;
        }

        mesh.Fallback.Update(item.Pose);
        mesh.Fallback.Bind(_graphicsDevice);
        if (_shadowEffect is null)
        {
            return 0;
        }

        EffectBindings.Set(_shadowEffect, "World", item.World);
        EffectBindings.Set(_shadowEffect, "LightViewProjection", lightViewProjection);
        foreach (EffectPass pass in _shadowEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.Fallback.PrimitiveCount);
        }

        return 1;
    }

    private void DrawCharacterItem(CharacterRenderItem item, RenderFrameSnapshot snapshot, CameraMatrices camera, Matrix lightViewProjection)
    {
        CharacterGpuLod lod = item.Asset.SelectLod(item.Lod);
        DrawCharacterMesh(lod.Body, item, snapshot, camera, lightViewProjection);
        if (lod.Clothing is not null)
        {
            DrawCharacterMesh(lod.Clothing, item, snapshot, camera, lightViewProjection);
        }
    }

    private void DrawCharacterMesh(CharacterGpuMeshPair mesh, CharacterRenderItem item, RenderFrameSnapshot snapshot, CameraMatrices camera, Matrix lightViewProjection)
    {
        _graphicsDevice.RasterizerState = _doubleSidedRasterizer;
        if (_characterEffect is not null)
        {
            mesh.Skinned.Bind(_graphicsDevice);
            EffectBindings.Set(_characterEffect, "World", item.World);
            EffectBindings.Set(_characterEffect, "View", camera.View);
            EffectBindings.Set(_characterEffect, "Projection", camera.Projection);
            EffectBindings.Set(_characterEffect, "LightViewProjection", lightViewProjection);
            EffectBindings.Set(_characterEffect, "Bones", CharacterRenderingBridge.ToXnaMatrices(item.Pose.BuildSkinMatrices()));
            EffectBindings.Set(_characterEffect, "CameraPosition", snapshot.Camera.Position);
            EffectBindings.Set(_characterEffect, "LightDirection", snapshot.Sun.Direction);
            EffectBindings.Set(_characterEffect, "LightColor", snapshot.Sun.Color.ToVector3() * snapshot.Sun.Intensity);
            EffectBindings.Set(_characterEffect, "AmbientIntensity", item.Highlighted ? 0.72f : 0.28f);
            EffectBindings.Set(_characterEffect, "FogColor", snapshot.Fog.Color.ToVector4());
            EffectBindings.Set(_characterEffect, "FogStart", snapshot.Fog.Start);
            EffectBindings.Set(_characterEffect, "FogEnd", snapshot.Fog.End);
            EffectBindings.Set(_characterEffect, "ShadowMap", _targets.ShadowMap ?? _whiteTexture);
            EffectBindings.Set(_characterEffect, "AlbedoTexture", _whiteTexture);
            EffectBindings.Set(_characterEffect, "ShadowStrength", item.ReceivesShadow && _options.EnableShadows ? 0.55f : 0f);
            foreach (EffectPass pass in _characterEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.Skinned.PrimitiveCount);
            }

            return;
        }

        mesh.Fallback.Update(item.Pose);
        mesh.Fallback.Bind(_graphicsDevice);
        _fallbackEffect.World = item.World;
        _fallbackEffect.View = camera.View;
        _fallbackEffect.Projection = camera.Projection;
        _fallbackEffect.DiffuseColor = Vector3.One;
        _fallbackEffect.EmissiveColor=item.Highlighted?new Vector3(0.26f,0.24f,0.08f):Vector3.Zero;
        _fallbackEffect.SpecularColor=new Vector3(0.025f);
        _fallbackEffect.SpecularPower=12f;
        _fallbackEffect.Alpha = 1f;
        _fallbackEffect.Texture = _whiteTexture;
        _fallbackEffect.FogColor = snapshot.Fog.Color.ToVector3();
        _fallbackEffect.FogStart = snapshot.Fog.Start;
        _fallbackEffect.FogEnd = snapshot.Fog.End;
        _fallbackEffect.AmbientLightColor = new Vector3(0.28f);
        _fallbackEffect.DirectionalLight0.Enabled = true;
        _fallbackEffect.DirectionalLight0.Direction = snapshot.Sun.Direction;
        _fallbackEffect.DirectionalLight0.DiffuseColor = snapshot.Sun.Color.ToVector3() * snapshot.Sun.Intensity;
        foreach (EffectPass pass in _fallbackEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.Fallback.PrimitiveCount);
        }
    }

    private void DrawOpaqueItem(RenderItem item, RenderFrameSnapshot snapshot, CameraMatrices camera, Matrix lightViewProjection)
    {
        item.Material.Validate();
        item.Mesh.Bind(_graphicsDevice);
        _graphicsDevice.RasterizerState = item.Material.DoubleSided ? _doubleSidedRasterizer : _solidRasterizer;
        Texture2D texture = item.Material.AlbedoTexture ?? _surfaceTextures.Resolve(item.Material.Id) ?? _whiteTexture;
        if (_forwardEffect is not null)
        {
            EffectBindings.Set(_forwardEffect, "World", item.World);
            EffectBindings.Set(_forwardEffect, "WorldInverseTranspose", Matrix.Transpose(Matrix.Invert(item.World)));
            EffectBindings.Set(_forwardEffect, "View", camera.View);
            EffectBindings.Set(_forwardEffect, "Projection", camera.Projection);
            EffectBindings.Set(_forwardEffect, "LightViewProjection", lightViewProjection);
            EffectBindings.Set(_forwardEffect, "CameraPosition", snapshot.Camera.Position);
            EffectBindings.Set(_forwardEffect, "LightDirection", snapshot.Sun.Direction);
            EffectBindings.Set(_forwardEffect, "LightColor", snapshot.Sun.Color.ToVector3() * snapshot.Sun.Intensity);
            EffectBindings.Set(_forwardEffect, "AlbedoColor", item.Material.Albedo.ToVector4());
            EffectBindings.Set(_forwardEffect, "Roughness", item.Material.Roughness);
            EffectBindings.Set(_forwardEffect, "Metallic", item.Material.Metallic);
            EffectBindings.Set(_forwardEffect, "EmissiveStrength", item.Material.EmissiveStrength);
            EffectBindings.Set(_forwardEffect, "AmbientIntensity", 0.28f);
            EffectBindings.Set(_forwardEffect, "FogColor", snapshot.Fog.Color.ToVector4());
            EffectBindings.Set(_forwardEffect, "FogStart", snapshot.Fog.Start);
            EffectBindings.Set(_forwardEffect, "FogEnd", snapshot.Fog.End);
            EffectBindings.Set(_forwardEffect, "ShadowStrength", item.ReceivesShadow && _options.EnableShadows ? 0.55f : 0f);
            EffectBindings.Set(_forwardEffect, "AlbedoTexture", texture);
            EffectBindings.Set(_forwardEffect, "ShadowMap", _targets.ShadowMap ?? _whiteTexture);
            foreach (EffectPass pass in _forwardEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, item.Mesh.PrimitiveCount);
            }

            return;
        }

        _fallbackEffect.World = item.World;
        _fallbackEffect.View = camera.View;
        _fallbackEffect.Projection = camera.Projection;
        _fallbackEffect.DiffuseColor = item.Material.Albedo.ToVector3();
        _fallbackEffect.SpecularColor = Vector3.Lerp(new Vector3(0.035f),item.Material.Albedo.ToVector3()*0.45f,item.Material.Metallic)*(1f-item.Material.Roughness*0.7f);
        _fallbackEffect.SpecularPower = MathHelper.Lerp(96f,8f,item.Material.Roughness);
        _fallbackEffect.EmissiveColor = item.Material.Albedo.ToVector3()*item.Material.EmissiveStrength;
        _fallbackEffect.Alpha = item.Material.Albedo.A / 255f;
        _fallbackEffect.Texture = texture;
        _fallbackEffect.FogColor = snapshot.Fog.Color.ToVector3();
        _fallbackEffect.FogStart = snapshot.Fog.Start;
        _fallbackEffect.FogEnd = snapshot.Fog.End;
        _fallbackEffect.AmbientLightColor = new Microsoft.Xna.Framework.Vector3(0.28f);
        _fallbackEffect.DirectionalLight0.Enabled = true;
        _fallbackEffect.DirectionalLight0.Direction = snapshot.Sun.Direction;
        _fallbackEffect.DirectionalLight0.DiffuseColor = snapshot.Sun.Color.ToVector3() * snapshot.Sun.Intensity;
        foreach (EffectPass pass in _fallbackEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, item.Mesh.PrimitiveCount);
        }
    }

    private void DrawPostProcess(RenderTarget2D scene)
    {
        _graphicsDevice.Clear(Color.Black);
        if (_postEffect is not null)
        {
            EffectBindings.Set(_postEffect, "SceneTexture", scene);
            EffectBindings.Set(_postEffect, "Exposure", _options.Exposure);
            EffectBindings.Set(_postEffect, "Gamma", _options.Gamma);
            EffectBindings.Set(_postEffect, "VignetteStrength", _options.VignetteStrength);
            EffectBindings.Set(_postEffect, "InverseResolution", new Vector2(1f / scene.Width, 1f / scene.Height));
        }

        _spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            _postEffect);
        _spriteBatch.Draw(scene, _graphicsDevice.Viewport.Bounds, Color.White);
        _spriteBatch.End();
    }

    private Matrix CreateLightViewProjection(RenderFrameSnapshot snapshot)
    {
        Vector3 cameraForward = snapshot.Camera.Forward;
        if (cameraForward.LengthSquared() < 0.000001f) cameraForward = Vector3.Forward;
        cameraForward.Normalize();
        Vector3 center = snapshot.Camera.Position + cameraForward * (_options.ShadowDistance * 0.45f);
        Vector3 lightPosition = center - snapshot.Sun.Direction * _options.ShadowDistance;
        Vector3 up = Math.Abs(Vector3.Dot(snapshot.Sun.Direction, Vector3.Up)) > 0.95f ? Vector3.Forward : Vector3.Up;
        Matrix view = Matrix.CreateLookAt(lightPosition, center, up);
        Matrix projection = Matrix.CreateOrthographic(_options.ShadowDistance, _options.ShadowDistance, 0.1f, _options.ShadowDistance * 3f);
        return view * projection;
    }

    private Effect? TryLoadEffect(string assetName)
    {
        try
        {
            return _content.Load<Effect>(assetName);
        }
        catch (ContentLoadException exception)
        {
            _logger.Write(LogLevel.Warning, nameof(RendererFoundation), $"Effect '{assetName}' unavailable; renderer will use its fallback path where possible.", exception);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _threadGuard.AssertAccess();
        _disposed = true;
        _targets.Dispose();
        _debugLines.Dispose();
        _fallbackEffect.Dispose();
        _solidRasterizer.Dispose();
        _doubleSidedRasterizer.Dispose();
        _whiteTexture.Dispose();
        _surfaceTextures.Dispose();
        _spriteBatch.Dispose();
    }
}
