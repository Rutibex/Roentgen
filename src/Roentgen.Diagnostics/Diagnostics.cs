namespace Roentgen.Diagnostics;

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public interface IRoentgenLogger
{
    void Write(LogLevel level, string category, string message, Exception? exception = null);
}

public sealed class ConsoleRoentgenLogger(LogLevel minimumLevel = LogLevel.Information) : IRoentgenLogger
{
    public void Write(LogLevel level, string category, string message, Exception? exception = null)
    {
        if (level < minimumLevel)
        {
            return;
        }

        string line = $"{DateTimeOffset.UtcNow:O} [{level}] {category}: {message}";
        if (exception is not null)
        {
            line = $"{line}{Environment.NewLine}{exception}";
        }

        Console.WriteLine(line);
    }
}
