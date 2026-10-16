namespace Roentgen.Rendering;

public sealed record RendererTelemetry(
    long FrameNumber,
    int SubmittedItems,
    int VisibleItems,
    int CulledItems,
    int DrawCalls,
    int Triangles,
    int ShadowDrawCalls,
    int UploadsCompleted,
    int UploadsSkipped,
    int UploadsFailed,
    long UploadBytesCompleted,
    int UploadsPending,
    long UploadBytesPending,
    long UploadsRejected,
    double UploadMilliseconds,
    double CpuMilliseconds,
    bool ShadowsEnabled,
    bool PostProcessingEnabled);

public sealed class RendererTelemetryCollector
{
    private RendererTelemetry _latest = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, false);
    public RendererTelemetry Latest => _latest;
    internal void Publish(RendererTelemetry telemetry) => _latest = telemetry;
}
