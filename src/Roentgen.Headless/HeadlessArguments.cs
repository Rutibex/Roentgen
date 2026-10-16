using System.Globalization;

namespace Roentgen.Headless;

public sealed class HeadlessArguments
{
    private readonly Dictionary<string, string> _options;
    private readonly HashSet<string> _flags;

    private HeadlessArguments(string command, Dictionary<string, string> options, HashSet<string> flags)
    {
        Command = command;
        _options = options;
        _flags = flags;
    }

    public string Command { get; }

    public static HeadlessArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string command = args.Length == 0 ? "help" : args[0].Trim().ToLowerInvariant();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index++)
        {
            string token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected positional argument '{token}'.");
            }

            string option = token[2..];
            int equals = option.IndexOf('=');
            if (equals >= 0)
            {
                string key = option[..equals];
                string value = option[(equals + 1)..];
                AddOption(options, key, value);
            }
            else if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                AddOption(options, option, args[++index]);
            }
            else
            {
                if (!flags.Add(option))
                {
                    throw new ArgumentException($"Duplicate flag '--{option}'.");
                }
            }
        }

        return new HeadlessArguments(command, options, flags);
    }

    public bool HasFlag(string name) => _flags.Contains(name);

    public string GetString(string name, string defaultValue) =>
        _options.TryGetValue(name, out string? value) ? value : defaultValue;

    public string? GetOptionalString(string name) => _options.TryGetValue(name, out string? value) ? value : null;

    public int GetInt32(string name, int defaultValue)
    {
        if (!_options.TryGetValue(name, out string? value)) return defaultValue;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw new ArgumentException($"Option '--{name}' requires a 32-bit integer.");
    }

    public ulong GetUInt64(string name, ulong defaultValue)
    {
        if (!_options.TryGetValue(name, out string? value)) return defaultValue;
        return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed)
            ? parsed
            : throw new ArgumentException($"Option '--{name}' requires an unsigned 64-bit integer.");
    }

    private static void AddOption(Dictionary<string, string> options, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || !options.TryAdd(key, value))
        {
            throw new ArgumentException($"Duplicate or empty option '--{key}'.");
        }
    }
}
