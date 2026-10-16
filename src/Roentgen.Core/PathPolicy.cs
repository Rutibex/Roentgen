namespace Roentgen.Core;

public static class PathPolicy
{
    public static string GetUserDataRoot()
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, BuildIdentity.ProductName);
    }

    public static string GetDirectory(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(GetUserDataRoot(), name);
    }
}
