using System.Text.Json;

namespace Roentgen.Architecture.Tests;

public sealed class RepositoryArchitectureTests
{
    [Fact]
    public void DeclaredProjectGraphIsAcyclic()
    {
        string root = FindRepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "eng", "projects.json")));
        Dictionary<string, string[]> graph = document.RootElement
            .GetProperty("runtimeProjects")
            .EnumerateArray()
            .ToDictionary(
                project => project.GetProperty("name").GetString()!,
                project => project.GetProperty("references").EnumerateArray().Select(value => value.GetString()!).ToArray(),
                StringComparer.Ordinal);

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string project in graph.Keys)
        {
            Visit(project, graph, visiting, visited);
        }
    }

    [Fact]
    public void MonoGameReferencesAreRestrictedToApprovedProjects()
    {
        string root = FindRepositoryRoot();
        string[] allowed = ["Roentgen.App", "Roentgen.Rendering", "Roentgen.UI"];
        foreach (string projectFile in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(projectFile);
            bool referencesMonoGame = File.ReadAllText(projectFile).Contains("MonoGame.Framework", StringComparison.Ordinal);
            Assert.True(!referencesMonoGame || allowed.Contains(projectName, StringComparer.Ordinal),
                $"{projectName} references MonoGame outside the approved boundary.");
        }
    }

    private static void Visit(string name, IReadOnlyDictionary<string, string[]> graph, ISet<string> visiting, ISet<string> visited)
    {
        if (visited.Contains(name))
        {
            return;
        }

        Assert.True(visiting.Add(name), $"Project cycle detected at {name}.");
        foreach (string dependency in graph[name])
        {
            Assert.True(graph.ContainsKey(dependency), $"{name} references undeclared project {dependency}.");
            Visit(dependency, graph, visiting, visited);
        }

        visiting.Remove(name);
        visited.Add(name);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "eng", "projects.json")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
