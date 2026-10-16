using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roentgen.Music;

public static class MusicSnapshotV19Serializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static byte[] Write(MusicSnapshotV19 snapshot) => JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);

    public static MusicSnapshotV19 Read(ReadOnlySpan<byte> bytes)
    {
        MusicSnapshotV19 snapshot = JsonSerializer.Deserialize<MusicSnapshotV19>(bytes, Options)
            ?? throw new InvalidDataException("Music snapshot did not contain a valid document.");
        if (snapshot.SchemaVersion != 19) throw new InvalidDataException($"Unsupported music snapshot schema {snapshot.SchemaVersion}.");
        return snapshot;
    }
}
