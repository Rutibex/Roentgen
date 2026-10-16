using Roentgen.App;

namespace Roentgen.App.SmokeTests;

public sealed class AppBootstrapTests
{
    [Fact]
    public void BootstrapRegistersFoundationModulesAndOptionalBridges()
    {
        var catalog = AppBootstrap.CreateModuleCatalog();
        Assert.True(catalog.Modules.Count >= 18);
        Assert.True(catalog.Contains("audio.playroma"));
        Assert.True(catalog.Contains("music.playroma"));
        Assert.True(catalog.Contains("assets"));
        Assert.True(catalog.Contains("rendering"));
        Assert.True(catalog.Contains("persistence"));
    }
}
