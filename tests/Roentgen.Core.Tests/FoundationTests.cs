using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Generation;
using Roentgen.Simulation;

namespace Roentgen.Core.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void ModuleCatalogRejectsDuplicateIds()
    {
        var catalog = new ModuleCatalog();
        var descriptor = new ModuleDescriptor("test", "Test", new Version(1, 0), []);
        catalog.Register(descriptor);
        Assert.Throws<InvalidOperationException>(() => catalog.Register(descriptor));
    }

    [Fact]
    public void FixedStepClockAdvancesThirtyTicksInHalfASecond()
    {
        var clock = new SimulationClock(60);
        int total = clock.Advance(TimeSpan.FromMilliseconds(500), maximumCatchUpTicks: 60);

        Assert.Equal(30, total);
        Assert.Equal<ulong>(30, clock.Tick);
    }

    [Fact]
    public void PausedClockDoesNotAdvance()
    {
        var clock = new SimulationClock(60);
        clock.Pause();
        Assert.Equal(0, clock.Advance(TimeSpan.FromSeconds(10)));
        Assert.Equal<ulong>(0, clock.Tick);
    }

    [Fact]
    public void DomainDerivationIsStableAndSeparated()
    {
        var seed = new CampaignSeed(7351982471UL);
        DomainSeed first = GenerationDomain.Derive(seed, "world.terrain");
        DomainSeed repeat = GenerationDomain.Derive(seed, "world.terrain");
        DomainSeed other = GenerationDomain.Derive(seed, "world.history");

        Assert.Equal(first, repeat);
        Assert.NotEqual(first.Value, other.Value);
    }
}
