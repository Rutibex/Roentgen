using Roentgen.Contracts;

namespace Roentgen.Core;

public static class BuildIdentity
{
    public const string ProductName = ConstitutionVersion.Product;
    public const string DevelopmentPhase = "Phase 20 — Full Interface and Accessibility";
    public const int SimulationTicksPerSecond = 60;

    public static Version AssemblyVersion => typeof(BuildIdentity).Assembly.GetName().Version ?? new Version(0, 1, 0, 0);
}
