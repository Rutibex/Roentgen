using Roentgen.Rpg;

namespace Roentgen.Focus;

public sealed record FocusPerkDefinition(string Id, string Name, string Description, string ModifierKind, int ModifierPermille);

public static class Phase11FocusPerks
{
    public static IReadOnlyList<FocusPerkDefinition> All { get; } = Phase12Content.Perks
        .Where(perk => string.Equals(perk.Family, "roentgen-focus", StringComparison.Ordinal))
        .Select(perk => new FocusPerkDefinition(
            perk.Id,
            perk.Name,
            perk.Description,
            perk.Effects[0].EffectType,
            perk.Effects[0].Value))
        .ToArray();
}
