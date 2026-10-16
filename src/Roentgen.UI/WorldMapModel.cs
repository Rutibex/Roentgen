namespace Roentgen.UI;

public enum MapFilter { All, Settlements, Ruins, Infrastructure, Quests }

public sealed record MapMarker(string Id, string Name, float X, float Z, string Kind, bool QuestTarget = false, bool Discovered = true);
public sealed record MapRoad(IReadOnlyList<(float X, float Z)> Points);
public sealed record WorldMapModel(
    string Identity,
    int WidthMeters,
    int DepthMeters,
    int SampleCount,
    IReadOnlyList<uint> Colors,
    IReadOnlyList<MapMarker> Markers,
    IReadOnlyList<MapRoad> Roads,
    float PlayerX,
    float PlayerZ,
    float HeadingRadians,
    string? TrackedId,
    float Zoom = 1f,
    float PanX = 0.5f,
    float PanZ = 0.5f,
    string? SelectedId = null);
