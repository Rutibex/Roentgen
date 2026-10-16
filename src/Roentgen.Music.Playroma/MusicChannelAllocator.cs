using PlayromaMusic.Core.Model;

namespace Roentgen.Music.Playroma;

public sealed record MusicChannelAllocationResult(bool Success, IReadOnlyList<string> Repairs, IReadOnlyList<string> Errors);

public sealed class MusicChannelAllocator
{
    public MusicChannelAllocationResult Allocate(SongDocument song)
    {
        ArgumentNullException.ThrowIfNull(song);
        List<string> repairs = [];
        List<string> errors = [];
        bool[] used = new bool[16];
        used[9] = true;
        foreach (SongTrack track in song.Tracks.Where(static value => value.IsPercussion || value.Role == TrackRole.Drums))
        {
            if (track.MidiChannel != 9) repairs.Add($"Moved percussion track '{track.Name}' from channel {track.MidiChannel} to channel 9.");
            track.MidiChannel = 9;
            track.IsPercussion = true;
        }

        foreach (SongTrack track in song.Tracks.Where(static value => !value.IsPercussion && value.Role != TrackRole.Drums).OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            int channel = FindFree(used, track.MidiChannel);
            if (channel < 0)
            {
                track.Muted = true;
                errors.Add($"Muted track '{track.Name}' because the 15 melodic MIDI channels were exhausted.");
                continue;
            }
            if (channel != track.MidiChannel) repairs.Add($"Reassigned track '{track.Name}' from channel {track.MidiChannel} to channel {channel}.");
            track.MidiChannel = channel;
            used[channel] = true;
        }
        return new MusicChannelAllocationResult(errors.Count == 0, repairs, errors);
    }

    private static int FindFree(bool[] used, int preferred)
    {
        if (preferred is >= 0 and < 16 && preferred != 9 && !used[preferred]) return preferred;
        for (int i = 0; i < used.Length; i++) if (i != 9 && !used[i]) return i;
        return -1;
    }
}
