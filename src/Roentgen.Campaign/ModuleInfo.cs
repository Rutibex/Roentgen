using Roentgen.Core;

namespace Roentgen.Campaign;

public static class ModuleInfo
{
    public static ModuleDescriptor Descriptor { get; } = new(
        "Roentgen.Campaign",
        "Deterministic regional history, campaign premise, milestone direction, revelations, irreversible choices, convergence, endings, epilogues, and automated completion validation.",
        new Version(0, 21, 0),
        ["Roentgen.Core", "Roentgen.Contracts", "Roentgen.Generation", "Roentgen.World", "Roentgen.Factions", "Roentgen.Narrative", "Roentgen.Rpg"]);
}
