using PlayromaMusic.Audio.Scheduling;
using PlayromaMusic.Core.Model;

namespace Roentgen.Music.Playroma;

public sealed record AdaptiveCueDocument(
    MusicCuePlan Plan,
    ProjectDocument Composition,
    SongPlaybackSnapshot Playback,
    IReadOnlyDictionary<int, MusicStemKind> ChannelStems,
    long PrimaryLoopStartSample,
    long PrimaryLoopEndSample,
    bool Looping);
