using System.Security.Cryptography;
using PlayromaMusic.Audio.SoundFonts;

namespace Roentgen.Music.Playroma;

public sealed record RoentgenSoundFontAsset(string AssetId, string FullPath, string Name, string Sha256, long Length)
{
    public const string DefaultAssetId = "generaluser-gs-2.0.3-beta";

    public static RoentgenSoundFontAsset Load(MusicSettings settings, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string relative = string.IsNullOrWhiteSpace(settings.DeveloperSoundFontOverride)
            ? settings.SoundFontRelativePath
            : settings.DeveloperSoundFontOverride;
        string path = Path.IsPathRooted(relative) ? relative : Path.Combine(baseDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Roentgen's bundled SoundFont was not found.", path);
        if (!SoundFontManager.HasSoundFontHeader(path)) throw new InvalidDataException("Roentgen's music asset is not a valid RIFF SoundFont bank.");
        using FileStream stream = File.OpenRead(path);
        string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        bool isOverride = !string.IsNullOrWhiteSpace(settings.DeveloperSoundFontOverride);
        if (!isOverride && !hash.Equals(settings.ExpectedSoundFontSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Bundled SoundFont fingerprint mismatch. Expected {settings.ExpectedSoundFontSha256}, found {hash}.");
        var catalog = new SoundFontPresetCatalogReader().Read(path);
        return new RoentgenSoundFontAsset(DefaultAssetId, path, catalog.SoundFontName, hash, new FileInfo(path).Length);
    }
}
