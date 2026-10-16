using Roentgen.Contracts;

namespace Roentgen.Contracts.Tests;

public sealed class CanonicalContractTests
{
    [Fact]
    public void ConstitutionIdentityIsCanonical()
    {
        Assert.Equal("Roentgen", ConstitutionVersion.Product);
        Assert.Equal("Roentgen Focus", ConstitutionVersion.TacticalMode);
    }

    [Fact]
    public void AttributeAndSkillCountsMatchConstitution()
    {
        Assert.Equal(7, Enum.GetValues<AttributeId>().Length);
        Assert.Equal(14, Enum.GetValues<SkillId>().Length);
    }
}
