using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roentgen.Audio;

/// <summary>Loads the Phase 18 audio section without coupling the audio runtime to the application shell.</summary>
public static class AudioSettingsLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static AudioSettings LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadFromJson(File.ReadAllText(path));
    }

    public static AudioSettings LoadFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        if (!document.RootElement.TryGetProperty("audio", out JsonElement audio))
            throw new InvalidDataException("The Roentgen settings document does not contain an 'audio' section.");
        AudioSettings? settings = audio.Deserialize<AudioSettings>(Options);
        return (settings ?? throw new InvalidDataException("The audio settings section could not be deserialized.")).Validate();
    }
}
