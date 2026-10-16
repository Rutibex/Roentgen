using Roentgen.Contracts;

namespace Roentgen.Rpg;

public static class Phase21RpgContent
{
    public static IReadOnlyList<BackgroundDefinition> Backgrounds { get; } = CreateBackgrounds();
    public static IReadOnlyList<TraitDefinition> Traits { get; } = CreateTraits();
    public static IReadOnlyList<PerkDefinition> Perks { get; } = CreatePerks();

    public static BackgroundTraitCatalog CreateBackgroundTraitCatalog() => new(Backgrounds, Traits);
    public static PerkCatalog CreatePerkCatalog() => new(Perks);

    private static IReadOnlyList<BackgroundDefinition> CreateBackgrounds()
    {
        var result = Phase12Content.Backgrounds.ToList();
        result.AddRange([
            Background("bunker-resident", "Bunker Resident", "Grew up maintaining sealed spaces where every resource and every secret had a ledger.", AttributeId.Vitality, AttributeId.Presence, SkillId.Security, SkillId.Science, "bunker-kit", ["tool.field-kit", "consumable.clean-water", "armor.field-respirator"], "interaction.solution.unlock"),
            Background("agricultural-worker", "Agricultural Worker", "Kept settlement food systems alive through failed seasons and contaminated soil.", AttributeId.Physique, AttributeId.Intellect, SkillId.Fieldcraft, SkillId.Athletics, "agriculture-kit", ["tool.field-kit", "consumable.field-ration", "consumable.clean-water"], "world.discovery.reveal"),
            Background("authority-clerk", "Authority Clerk", "Processed credentials, warrants, inventories, and obligations inside a surviving bureaucracy.", AttributeId.Intellect, AttributeId.Reflex, SkillId.Commerce, SkillId.Security, "clerk-kit", ["document.clinic-repair-manual", "currency.diagnostic-scrip", "consumable.electrolyte"], "dialogue.option.unlock"),
            Background("contamination-surveyor", "Contamination Surveyor", "Mapped exposure zones and learned which instruments lied under difficult field conditions.", AttributeId.Awareness, AttributeId.Presence, SkillId.Science, SkillId.Fieldcraft, "surveyor-kit", ["armor.field-respirator", "consumable.contamination-binder", "tool.field-kit"], "world.discovery.reveal"),
            Background("rail-worker", "Rail Worker", "Maintained track, freight equipment, and dangerous access routes through the Broken Meridian.", AttributeId.Physique, AttributeId.Fortune, SkillId.Athletics, SkillId.Engineering, "rail-kit", ["tool.field-kit", "weapon.improvised-melee", "armor.field-clothing"], "interaction.solution.unlock"),
            Background("waterworks-operator", "Waterworks Operator", "Operated pumps, filters, chemical balances, and emergency bypasses in failing treatment works.", AttributeId.Intellect, AttributeId.Reflex, SkillId.Engineering, SkillId.Science, "waterworks-kit", ["tool.field-kit", "consumable.clean-water", "consumable.antitoxin"], "interaction.solution.unlock"),
            Background("faction-defector", "Faction Defector", "Left an institution with training, enemies, and knowledge that can still open doors.", AttributeId.Presence, AttributeId.Vitality, SkillId.Stealth, SkillId.Influence, "defector-kit", ["weapon.self-loading-pistol", "armor.field-clothing", "currency.diagnostic-scrip"], "dialogue.option.unlock"),
            Background("remains-cataloguer", "Remains Cataloguer", "Recorded the dead, preserved evidence, and learned what bodies reveal after institutions fail.", AttributeId.Intellect, AttributeId.Physique, SkillId.Medicine, SkillId.Science, "cataloguer-kit", ["tool.medical-kit", "consumable.diagnostic-dye", "armor.field-respirator"], "skill.option.unlock"),
            Background("observatory-assistant", "Observatory Assistant", "Maintained highland optics, signal equipment, weather records, and buried instrument arrays.", AttributeId.Awareness, AttributeId.Physique, SkillId.Science, SkillId.Security, "observatory-kit", ["tool.field-kit", "ammo.energy-cell", "armor.field-clothing"], "world.discovery.reveal"),
            Background("independent-hunter", "Independent Hunter", "Survived between settlements by reading tracks, habits, weather, and dangerous silences.", AttributeId.Reflex, AttributeId.Intellect, SkillId.Fieldcraft, SkillId.Ballistics, "hunter-kit", ["weapon.hunting-rifle", "ammo.rifle-cartridge", "consumable.field-ration"], "combat.action.unlock")
        ]);
        return result;
    }

    private static IReadOnlyList<TraitDefinition> CreateTraits()
    {
        var result = Phase12Content.Traits.ToList();
        result.AddRange([
            Trait("storm-blooded", "Storm Blooded", "Electrical weather sharpens your reactions but worsens delicate medical work.", [Skill("storm-reflex", SkillId.Athletics, 5)], [Skill("storm-medicine", SkillId.Medicine, -3)], []),
            Trait("night-eyes", "Night Eyes", "Low light reveals more detail, while bright diagnostic overlays are harder to tolerate.", [Capability("world.discovery.reveal", "low-light-detail", 1)], [Derived("night-focus", DerivedStatId.MaximumFocusMilliunits, -500)], []),
            Trait("ledger-minded", "Ledger Minded", "Trade obligations are easier to track, but improvisational influence suffers.", [Skill("ledger-commerce", SkillId.Commerce, 6)], [Skill("ledger-influence", SkillId.Influence, -3)], []),
            Trait("oath-bound", "Oath Bound", "Promises create stronger social leverage, but breaking one is more damaging.", [Capability("dialogue.option.unlock", "binding-promise", 1)], [Skill("oath-deception", SkillId.Stealth, -2)], []),
            Trait("restless", "Restless", "Travel and exploration recover momentum quickly, while stationary recovery is weaker.", [Derived("restless-move", DerivedStatId.MovementSpeedPermille, 60)], [Derived("restless-stamina", DerivedStatId.MaximumStamina, -6)], []),
            Trait("slow-healer", "Slow Healer", "Lasting injuries are easier to understand but take longer to recover from.", [Capability("focus.knowledge.unlock", "injury-history", 1)], [Skill("slow-heal", SkillId.Medicine, -4)], []),
            Trait("clean-hands", "Clean Hands", "Sterile procedures and evidence handling improve, but field scavenging yields less.", [Skill("clean-medicine", SkillId.Medicine, 5)], [Skill("clean-fieldcraft", SkillId.Fieldcraft, -3)], []),
            Trait("rough-voice", "Rough Voice", "Threats carry weight, while careful mediation is more difficult.", [Capability("dialogue.option.unlock", "rough-intimidation", 1)], [Skill("rough-influence", SkillId.Influence, -3)], ["unsettling-calm"]),
            Trait("diagnostic-dreams", "Diagnostic Dreams", "Rare exposure patterns reveal themselves in sleep, but rest is less restorative.", [Capability("world.discovery.reveal", "dream-evidence", 1)], [Derived("dream-stamina", DerivedStatId.MaximumStamina, -8)], []),
            Trait("route-memory", "Route Memory", "Previously traveled routes remain clear under pressure, but unfamiliar interiors are distracting.", [Capability("world.discovery.reveal", "remembered-route", 1)], [Skill("route-security", SkillId.Security, -2)], []),
            Trait("contamination-tolerance", "Contamination Tolerance", "Exposure accumulates more slowly, but refined treatment has reduced effect.", [Capability("interaction.failure.transform", "contamination-tolerance", 1)], [Skill("tolerance-medicine", SkillId.Medicine, -3)], []),
            Trait("fragile-frame", "Fragile Frame", "Movement and concealment improve at the cost of health and carrying capacity.", [Skill("fragile-stealth", SkillId.Stealth, 6)], [Derived("fragile-health", DerivedStatId.MaximumHealth, -12), Derived("fragile-carry", DerivedStatId.CarryCapacityGrams, -8000)], ["pack-frame"]),
            Trait("jury-rigger", "Jury Rigger", "Damaged equipment can be made temporarily functional, while pristine repairs are less efficient.", [Capability("interaction.solution.unlock", "temporary-repair", 1)], [Skill("jury-science", SkillId.Science, -2)], []),
            Trait("unsettling-calm", "Unsettling Calm", "Fear and surrender dialogue options appear sooner, while warm first impressions suffer.", [Capability("dialogue.option.unlock", "calm-pressure", 1)], [Skill("calm-influence", SkillId.Influence, -2)], ["rough-voice"])
        ]);
        return result;
    }

    private static IReadOnlyList<PerkDefinition> CreatePerks()
    {
        var result = Phase12Content.Perks.ToList();
        string[] families = ["sidearms", "rifles", "shotguns", "heavy-weapons", "directed-energy", "explosives", "melee", "unarmed", "roentgen-focus", "survival", "field-medicine", "engineering", "science", "security", "crafting", "commerce", "influence", "companions", "exploration", "radiological", "faction", "capstone"];
        string[] behaviorEffects = ["combat.action.unlock", "combat.targeting.unlock", "focus.knowledge.unlock", "interaction.solution.unlock", "world.discovery.reveal", "dialogue.option.unlock", "economy.option.unlock"];
        int ordinal = 0;
        foreach (string family in families)
        {
            int additions = family switch
            {
                "roentgen-focus" or "capstone" or "sidearms" or "rifles" => 4,
                _ => 3
            };
            for (int tier = 1; tier <= additions; tier++)
            {
                ordinal++;
                bool numerical = ordinal % 5 == 0;
                string id = $"phase21-{family}-{tier:00}";
                string effectType = numerical ? "skill.effective.modify" : behaviorEffects[ordinal % behaviorEffects.Length];
                SkillId skill = SkillForFamily(family, ordinal);
                var requirements = new List<PerkRequirement>
                {
                    new(PerkRequirementKind.Level, "level", Math.Min(48, tier * 6 + ordinal / 8)),
                    new(PerkRequirementKind.Skill, skill.ToString(), Math.Min(90, 25 + tier * 12))
                };
                if (tier > 1) requirements.Add(new(PerkRequirementKind.PriorPerk, $"phase21-{family}-{tier - 1:00}", 1));
                result.Add(new PerkDefinition(
                    id,
                    $"{DisplayFamily(family)} {tier}",
                    numerical
                        ? $"Refines the numerical performance of the {DisplayFamily(family).ToLowerInvariant()} build at a high-level threshold."
                        : $"Unlocks a new {DisplayFamily(family).ToLowerInvariant()} behavior, route, or interaction.",
                    family,
                    1,
                    numerical ? PerkBalanceClass.Numerical : tier % 2 == 0 ? PerkBalanceClass.OptionUnlock : PerkBalanceClass.Behavior,
                    requirements,
                    [new PerkEffectDefinition(effectType, $"{family}.tier.{tier}", numerical ? 5 + tier : 1, new Dictionary<string, string> { ["phase"] = "21", ["tier"] = tier.ToString(System.Globalization.CultureInfo.InvariantCulture) })]));
            }
        }
        if (result.Count != 110) throw new InvalidOperationException($"Phase 21 perk catalog must contain 110 perks; generated {result.Count}.");
        return result;
    }

    private static SkillId SkillForFamily(string family, int ordinal) => family switch
    {
        "sidearms" or "rifles" or "shotguns" or "heavy-weapons" => SkillId.Ballistics,
        "directed-energy" or "radiological" => SkillId.DirectedEnergy,
        "explosives" => SkillId.Demolitions,
        "melee" => SkillId.Melee,
        "unarmed" => SkillId.Unarmed,
        "survival" or "exploration" => SkillId.Fieldcraft,
        "field-medicine" => SkillId.Medicine,
        "engineering" or "crafting" => SkillId.Engineering,
        "science" or "roentgen-focus" => SkillId.Science,
        "security" => SkillId.Security,
        "commerce" => SkillId.Commerce,
        "influence" or "companions" or "faction" => SkillId.Influence,
        _ => (SkillId)(ordinal % Enum.GetValues<SkillId>().Length)
    };

    private static string DisplayFamily(string value) => string.Join(" ", value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(static word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static BackgroundDefinition Background(string id, string name, string description, AttributeId plus, AttributeId minus, SkillId skillA, SkillId skillB, string package, string[] items, string benefit) =>
        new(id, name, description, new Dictionary<AttributeId, int> { [plus] = 1, [minus] = -1 }, new Dictionary<SkillId, int> { [skillA] = 5, [skillB] = 5 }, [$"knowledge.{id}"], [$"dialogue.{id}"], new StartingEquipmentGrant(package, items), benefit);

    private static TraitDefinition Trait(string id, string name, string description, ProgressionModifier[] benefits, ProgressionModifier[] drawbacks, string[] incompatible) =>
        new(id, name, description, benefits, drawbacks, incompatible, [$"trait.{id}"]);
    private static ProgressionModifier Skill(string id, SkillId skill, int value) => new(id, id, null, skill, null, value, 1000, true);
    private static ProgressionModifier Derived(string id, DerivedStatId stat, int value) => new(id, id, null, null, stat, value, 1000, true);
    private static ProgressionModifier Capability(string id, string target, int value) => new(id, target, null, null, null, value, 1000, true);
}
