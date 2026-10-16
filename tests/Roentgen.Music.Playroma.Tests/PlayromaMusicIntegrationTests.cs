using Xunit;
using PlayromaMusic.Core.Model;
using PlayromaMusic.Core.Orchestration;
using Roentgen.Music;
using Roentgen.Music.Playroma;

namespace Roentgen.Music.Playroma.Tests;

public sealed class PlayromaMusicIntegrationTests
{
    private static string Root => FindRoot();
    [Fact] public void BundledSoundFontMatchesPinnedFingerprint()
    {
        RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(new MusicSettings(), Root);
        Assert.Equal("9575028c7a1f589f5770fccc8cff2734566af40cd26ed836944e9a5152688cfe", asset.Sha256);
    }

    [Fact] public void CuratedCatalogHas287PresetsAndExcludesSoundEffects()
    {
        var settings = new MusicSettings();
        RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(settings, Root);
        InstrumentCatalogDocument catalog = RoentgenPresetCatalog.Load(settings, Root, asset);
        Assert.Equal(287, catalog.Presets.Count);
        Assert.All(catalog.Presets.Where(static value => value.Family == InstrumentFamily.SoundEffects), static value => Assert.True(value.IsExcluded));
    }

    [Fact] public void ChannelAllocatorReservesNineForDrums()
    {
        var song = new SongDocument { Tracks = [new SongTrack { Id = "lead", Name = "Lead", MidiChannel = 9 }, new SongTrack { Id = "drums", Name = "Drums", Role = TrackRole.Drums, MidiChannel = 1, IsPercussion = true }] };
        MusicChannelAllocationResult result = new MusicChannelAllocator().Allocate(song);
        Assert.True(result.Success);
        Assert.Equal(9, song.Tracks[1].MidiChannel);
        Assert.NotEqual(9, song.Tracks[0].MidiChannel);
    }

    [Fact] public void SameSeedProducesSameCueFingerprint()
    {
        var settings = new MusicSettings();
        RoentgenSoundFontAsset asset = RoentgenSoundFontAsset.Load(settings, Root);
        InstrumentCatalogDocument catalog = RoentgenPresetCatalog.Load(settings, Root, asset);
        var generator = new RoentgenCueGenerator(catalog, asset, settings.SampleRate);
        string first = generator.PrepareDocument(MusicSituation.WastelandExploration, MusicContext.Default, 1234).Plan.Fingerprint;
        string second = generator.PrepareDocument(MusicSituation.WastelandExploration, MusicContext.Default, 1234).Plan.Fingerprint;
        Assert.Equal(first, second);
    }

    private static string FindRoot()
    {
        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++) { if (File.Exists(Path.Combine(current, "content", "music", "soundfonts", "default.sf2"))) return current; DirectoryInfo? parent = Directory.GetParent(current); if (parent is null) break; current = parent.FullName; }
        return AppContext.BaseDirectory;
    }
}
