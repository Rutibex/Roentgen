namespace Roentgen.Rendering;

public sealed record RenderingOptions
{
    public float FieldOfViewDegrees { get; init; } = 70f;
    public float NearPlane { get; init; } = 0.08f;
    public float FarPlane { get; init; } = 1600f;
    public int ShadowMapSize { get; init; } = 2048;
    public float ShadowDistance { get; init; } = 180f;
    public float FogStart { get; init; } = 110f;
    public float FogEnd { get; init; } = 620f;
    public float Exposure { get; init; } = 1f;
    public float Gamma { get; init; } = 2.2f;
    public float VignetteStrength { get; init; } = 0.16f;
    public bool EnableShadows { get; init; } = true;
    public bool EnablePostProcessing { get; init; } = true;
    public bool EnableFrustumCulling { get; init; } = true;
    public int MaximumUploadsPerFrame { get; init; } = 8;
    public long MaximumUploadBytesPerFrame { get; init; } = 8L * 1024L * 1024L;
    public double MaximumUploadMillisecondsPerFrame { get; init; } = 2.5d;
    public int MaximumPendingUploadOperations { get; init; } = 512;
    public long MaximumPendingUploadBytes { get; init; } = 256L * 1024L * 1024L;

    public void Validate()
    {
        if (FieldOfViewDegrees is < 35f or > 120f)
        {
            throw new ArgumentOutOfRangeException(nameof(FieldOfViewDegrees));
        }

        if (NearPlane <= 0f || FarPlane <= NearPlane)
        {
            throw new ArgumentOutOfRangeException(nameof(FarPlane), "Far plane must exceed a positive near plane.");
        }

        if (ShadowMapSize is < 256 or > 8192 || (ShadowMapSize & (ShadowMapSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ShadowMapSize), "Shadow map size must be a power of two from 256 through 8192.");
        }

        if (FogEnd <= FogStart)
        {
            throw new ArgumentOutOfRangeException(nameof(FogEnd), "Fog end must exceed fog start.");
        }

        if (Exposure <= 0f || Gamma <= 0f || VignetteStrength is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(Exposure));
        }

        if (MaximumUploadsPerFrame is < 1 or > 256 || MaximumUploadBytesPerFrame < 1 || MaximumUploadMillisecondsPerFrame <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumUploadsPerFrame));
        }

        if (MaximumPendingUploadOperations < MaximumUploadsPerFrame || MaximumPendingUploadBytes < MaximumUploadBytesPerFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumPendingUploadOperations), "Pending upload capacity must cover at least one frame budget.");
        }
    }
}
