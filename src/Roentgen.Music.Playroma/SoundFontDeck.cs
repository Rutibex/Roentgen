using PlayromaMusic.Audio.Backends;
using PlayromaMusic.Audio.Scheduling;

namespace Roentgen.Music.Playroma;

internal sealed class SoundFontDeck : IDisposable
{
    private readonly ISynthBackend _synth;
    private readonly float[] _render;
    private readonly bool[] _activeNotes = new bool[16 * 128];
    private readonly float[] _stemGains = [1f, 1f, 1f, 1f, 1f, 1f];
    private readonly int[] _channelVolumes = Enumerable.Repeat(100, 16).ToArray();
    private AdaptiveCueDocument? _cue;
    private int _eventIndex;
    private long _sample;
    private float _gain;
    private bool _running;
    private bool _completed;
    private int _activeNoteCount;
    private int _peakPolyphony;
    private bool _anySolo;

    public SoundFontDeck(RoentgenSoundFontAsset asset, int sampleRate, int blockFrames)
    {
        _synth = new MeltySynthBackend(sampleRate);
        _synth.LoadSoundFont(asset.FullPath);
        _render = new float[blockFrames * 2];
        _synth.Render(_render, blockFrames);
        _synth.Reset();
    }

    public AdaptiveCueDocument? Cue => _cue;
    public long SamplePosition => _sample;
    public long MusicalTick => _cue?.Playback.SampleToTick(_sample) ?? 0;
    public bool Running => _running;
    public bool Completed => _completed;
    public float Gain { get => _gain; set => _gain = Math.Clamp(value, 0f, 1.5f); }
    public int ActiveNotes => _activeNoteCount;
    public int PeakPolyphony => _peakPolyphony;

    public void Load(AdaptiveCueDocument cue, long startTick = 0)
    {
        ArgumentNullException.ThrowIfNull(cue);
        _synth.Reset();
        Array.Clear(_activeNotes);
        Array.Fill(_channelVolumes, 100);
        _activeNoteCount = 0;
        _peakPolyphony = 0;
        _cue = cue;
        _sample = Math.Clamp(cue.Playback.TickToSample(startTick), 0, cue.Playback.MusicalLengthSamples);
        _eventIndex = LowerBound(cue.Playback.Events, _sample);
        _anySolo = cue.Playback.Tracks.Values.Any(static value => value.Solo);
        ApplyStateUntil(_sample);
        _completed = false;
        _running = true;
        ApplyStemVolumes();
    }

    public void Stop()
    {
        _synth.AllNotesOff();
        Array.Clear(_activeNotes);
        _activeNoteCount = 0;
        _running = false;
        _completed = true;
    }

    public void SetStemGain(MusicStemKind stem, float gain)
    {
        float normalized = Math.Clamp(gain, 0f, 1f);
        if (Math.Abs(_stemGains[(int)stem] - normalized) < 0.001f) return;
        _stemGains[(int)stem] = normalized;
        if (_running && _cue is not null) ApplyStemVolume(stem);
    }

    public void Render(Span<float> destination, int frames)
    {
        destination[..(frames * 2)].Clear();
        if (!_running || _cue is null || frames == 0) return;
        int outputOffsetFrames = 0;
        while (outputOffsetFrames < frames && _running)
        {
            ProcessEventsAtCurrentSample();
            long limit = _cue.Looping ? _cue.PrimaryLoopEndSample : _cue.Playback.TotalLengthSamples;
            long nextEvent = _eventIndex < _cue.Playback.Events.Count ? _cue.Playback.Events[_eventIndex].Sample : long.MaxValue;
            long segmentEnd = Math.Min(limit, nextEvent);
            int available = frames - outputOffsetFrames;
            int segmentFrames = segmentEnd <= _sample ? 0 : (int)Math.Min(available, segmentEnd - _sample);
            if (segmentFrames > 0)
            {
                Span<float> temp = _render.AsSpan(0, segmentFrames * 2);
                temp.Clear();
                _synth.Render(temp, segmentFrames);
                int sampleOffset = outputOffsetFrames * 2;
                for (int i = 0; i < temp.Length; i++) destination[sampleOffset + i] += temp[i] * _gain;
                _sample += segmentFrames;
                outputOffsetFrames += segmentFrames;
            }
            else if (_sample >= limit)
            {
                if (_cue.Looping) Loop();
                else Stop();
            }
            else if (nextEvent <= _sample)
            {
                ProcessEventsAtCurrentSample();
            }
            else
            {
                break;
            }
        }
    }

    public void Dispose() => _synth.Dispose();

    private void ProcessEventsAtCurrentSample()
    {
        if (_cue is null) return;
        IReadOnlyList<PlaybackEvent> events = _cue.Playback.Events;
        while (_eventIndex < events.Count && events[_eventIndex].Sample <= _sample)
        {
            Apply(events[_eventIndex]);
            _eventIndex++;
        }
    }

    private void Apply(PlaybackEvent item)
    {
        if (_cue is null || !_cue.Playback.Tracks.TryGetValue(item.TrackId, out PlaybackTrackState? track)) return;
        if (track.Muted || (_anySolo && !track.Solo)) return;
        switch (item.Kind)
        {
            case PlaybackEventKind.BankSelect:
                _synth.ControlChange(item.Channel, item.Data1, item.Data2);
                break;
            case PlaybackEventKind.ProgramChange:
                _synth.ProgramChange(item.Channel, item.Data1);
                break;
            case PlaybackEventKind.ControlChange:
                if (item.Data1 == 7)
                {
                    int channel = Math.Clamp(item.Channel, 0, 15);
                    _channelVolumes[channel] = Math.Clamp(item.Data2, 0, 127);
                    float stem = ResolveStemGain(channel);
                    _synth.ControlChange(channel, 7, Math.Clamp((int)Math.Round(_channelVolumes[channel] * stem), 0, 127));
                }
                else _synth.ControlChange(item.Channel, item.Data1, item.Data2);
                break;
            case PlaybackEventKind.NoteOn:
                _synth.NoteOn(item.Channel, item.Data1, item.Data2);
                SetNote(item.Channel, item.Data1, true);
                break;
            case PlaybackEventKind.NoteOff:
                _synth.NoteOff(item.Channel, item.Data1);
                SetNote(item.Channel, item.Data1, false);
                break;
        }
    }

    private void SetNote(int channel, int note, bool active)
    {
        int index = Math.Clamp(channel, 0, 15) * 128 + Math.Clamp(note, 0, 127);
        if (_activeNotes[index] == active) return;
        _activeNotes[index] = active;
        _activeNoteCount += active ? 1 : -1;
        _activeNoteCount = Math.Max(0, _activeNoteCount);
        _peakPolyphony = Math.Max(_peakPolyphony, _activeNoteCount);
    }

    private void Loop()
    {
        if (_cue is null) return;
        _synth.AllNotesOff();
        Array.Clear(_activeNotes);
        _activeNoteCount = 0;
        _sample = _cue.PrimaryLoopStartSample;
        _eventIndex = LowerBound(_cue.Playback.Events, _sample);
        ApplyStateUntil(_sample);
        ApplyStemVolumes();
    }

    private void ApplyStateUntil(long sample)
    {
        if (_cue is null) return;
        foreach (PlaybackEvent item in _cue.Playback.Events)
        {
            if (item.Sample > sample) break;
            if (item.Kind is PlaybackEventKind.BankSelect or PlaybackEventKind.ProgramChange or PlaybackEventKind.ControlChange) Apply(item);
        }
    }

    private void ApplyStemVolumes()
    {
        if (_cue is null) return;
        foreach (MusicStemKind stem in Enum.GetValues<MusicStemKind>()) ApplyStemVolume(stem);
    }

    private void ApplyStemVolume(MusicStemKind stem)
    {
        if (_cue is null) return;
        foreach ((int channel, MusicStemKind channelStem) in _cue.ChannelStems)
        {
            if (channelStem != stem) continue;
            int normalizedChannel = Math.Clamp(channel, 0, 15);
            int value = Math.Clamp((int)Math.Round(_channelVolumes[normalizedChannel] * _stemGains[(int)stem]), 0, 127);
            _synth.ControlChange(normalizedChannel, 7, value);
        }
    }

    private float ResolveStemGain(int channel) => _cue is not null && _cue.ChannelStems.TryGetValue(channel, out MusicStemKind stem) ? _stemGains[(int)stem] : 1f;

    private static int LowerBound(IReadOnlyList<PlaybackEvent> events, long sample)
    {
        int low = 0;
        int high = events.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (events[middle].Sample < sample) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
