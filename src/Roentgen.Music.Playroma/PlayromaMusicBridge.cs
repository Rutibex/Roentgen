using Roentgen.Core;

namespace Roentgen.Music.Playroma;

public sealed class PlayromaMusicBridge : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "music.playroma",
        "Integrated Playroma Music and GeneralUser GS SoundFont Runtime",
        new Version(0, 19, 0),
        ["music", "audio.playroma"]);

    public string Status => "Playroma Music Core, Generators, SoundFont scheduling, and MeltySynth runtime are integrated into Roentgen.";
}
