using Roentgen.Core;

namespace Roentgen.Audio.Playroma;

public sealed class PlayromaSoundBridge : IRoentgenModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "roentgen.audio.playroma",
        "Integrated Playroma Sound Runtime",
        new Version(0, 18, 0),
        ["roentgen.audio", "playroma.sound.core", "playroma.sound.dsp", "playroma.sound.graph", "playroma.sound.generators", "playroma.sound.streaming"]);
}
