using Roentgen.Contracts;

namespace Roentgen.Rpg;

public static class Phase12Content
{
    public static BackgroundTraitCatalog CreateBackgroundTraitCatalog() => new(Backgrounds, Traits);
    public static PerkCatalog CreatePerkCatalog() => new(Perks);

    public static IReadOnlyList<BackgroundDefinition> Backgrounds { get; } =
    [
        Background("perimeter-scout", "Perimeter Scout", "Mapped contaminated approaches and watched for movement beyond settlement lights.", AttributeId.Awareness, AttributeId.Presence, SkillId.Fieldcraft, SkillId.Stealth, "scout-kit", ["fieldcraft-map", "light-rifle", "water-test-strip"], "world.discovery.reveal"),
        Background("clinic-orderly", "Clinic Orderly", "Learned practical treatment while keeping failing diagnostic wards operating.", AttributeId.Vitality, AttributeId.Fortune, SkillId.Medicine, SkillId.Science, "clinic-kit", ["medical-roll", "diagnostic-lens", "antiseptic"], "skill.option.unlock"),
        Background("relay-technician", "Relay Technician", "Maintained signal relays, power converters, and access interlocks.", AttributeId.Intellect, AttributeId.Physique, SkillId.Engineering, SkillId.Security, "relay-technician-kit", ["multimeter", "wire-spool", "compact-sidearm"], "interaction.solution.unlock"),
        Background("quarry-mechanic", "Quarry Mechanic", "Repaired extraction machinery with whatever survived the shift.", AttributeId.Physique, AttributeId.Presence, SkillId.Engineering, SkillId.Melee, "quarry-kit", ["mechanic-tools", "cutting-bar", "work-coat"], "combat.action.unlock"),
        Background("caravan-factor", "Caravan Factor", "Balanced manifests, obligations, and danger along unstable trade routes.", AttributeId.Presence, AttributeId.Vitality, SkillId.Commerce, SkillId.Influence, "factor-kit", ["trade-ledger", "concealed-pistol", "sample-case"], "economy.option.unlock"),
        Background("salvage-inspector", "Salvage Inspector", "Classified structural hazards and valuable machinery inside ruins.", AttributeId.Awareness, AttributeId.Reflex, SkillId.Science, SkillId.Security, "inspector-kit", ["inspection-lamp", "chalk-set", "respirator"], "world.discovery.reveal"),
        Background("settlement-mediator", "Settlement Mediator", "Resolved disputes before shortages turned them into bloodshed.", AttributeId.Presence, AttributeId.Physique, SkillId.Influence, SkillId.Medicine, "mediator-kit", ["obligation-ledger", "formal-coat", "medical-pouch"], "dialogue.option.unlock"),
        Background("demolition-recovery-worker", "Demolition Recovery Worker", "Entered unstable industrial sites after controlled and uncontrolled collapses.", AttributeId.Reflex, AttributeId.Fortune, SkillId.Demolitions, SkillId.Athletics, "recovery-kit", ["breaching-charge", "protective-hood", "survey-hammer"], "interaction.failure.transform")
    ];

    public static IReadOnlyList<TraitDefinition> Traits { get; } =
    [
        Trait("methodical", "Methodical", "Deliberate actions are steadier but take longer.", [M("methodical-accuracy", SkillId.Ballistics, 5)], [D("methodical-speed", DerivedStatId.ReloadSpeedPermille, -100)], ["impulsive"]),
        Trait("adaptable", "Adaptable", "Unfamiliar procedures can be attempted at a penalty rather than remaining unavailable.", [Tag("skill.option.unlock", "untrained-attempt", 1)], [M("adaptable-specialization", SkillId.Science, -2)], []),
        Trait("impulsive", "Impulsive", "Rapid reactions are favored over careful diagnosis.", [D("impulsive-initiative", DerivedStatId.Initiative, 8)], [M("impulsive-science", SkillId.Science, -5)], ["methodical"]),
        Trait("scar-tissue", "Scar Tissue", "Severe trauma causes less stagger but takes longer to heal.", [Tag("combat.injury.rule", "stagger-resistance", 200)], [M("scar-medicine", SkillId.Medicine, -4)], []),
        Trait("radiographic-sensitivity", "Radiographic Sensitivity", "Diagnostic details resolve sooner, but Focus recovers more slowly.", [Tag("focus.knowledge.unlock", "early-internals", 1)], [D("sensitivity-recovery", DerivedStatId.FocusRecoveryPerSecondMilliunits, -500)], []),
        Trait("iron-stomach", "Iron Stomach", "Questionable field food is safer, while refined medicines are less effective.", [Tag("interaction.failure.transform", "unsafe-food", 1)], [M("iron-medicine", SkillId.Medicine, -3)], []),
        Trait("commanding", "Commanding", "First impressions improve, but subtle deception is more difficult.", [M("commanding-influence", SkillId.Influence, 6)], [M("commanding-stealth", SkillId.Stealth, -3)], ["unassuming"]),
        Trait("unassuming", "Unassuming", "You pass beneath notice but struggle to dominate a room.", [M("unassuming-stealth", SkillId.Stealth, 6)], [M("unassuming-influence", SkillId.Influence, -4)], ["commanding"]),
        Trait("last-reserve", "Last Reserve", "Low-health actions gain emergency options at the cost of maximum health.", [Tag("combat.action.unlock", "last-reserve", 1)], [D("last-reserve-health", DerivedStatId.MaximumHealth, -10)], []),
        Trait("heavy-handed", "Heavy Handed", "Melee impacts are stronger, while fine security work suffers.", [M("heavy-melee", SkillId.Melee, 6)], [M("heavy-security", SkillId.Security, -5)], ["fine-touch"]),
        Trait("fine-touch", "Fine Touch", "Delicate bypass work is easier, while heavy melee is weaker.", [M("fine-security", SkillId.Security, 6)], [M("fine-melee", SkillId.Melee, -5)], ["heavy-handed"]),
        Trait("field-naturalist", "Field Naturalist", "Biological signs and habitats expose additional solutions.", [Tag("interaction.solution.unlock", "ecology-route", 1)], [M("naturalist-commerce", SkillId.Commerce, -3)], []),
        Trait("machine-empathy", "Machine Empathy", "Damaged machines reveal behavioral clues, but social first impressions suffer.", [Tag("interaction.solution.unlock", "machine-behavior", 1)], [M("machine-presence", SkillId.Influence, -3)], []),
        Trait("lucky-breaks", "Lucky Breaks", "A failed check may expose a bounded alternate opportunity, but planned critical reserve is lower.", [Tag("interaction.failure.transform", "fortune-opportunity", 1)], [D("lucky-critical", DerivedStatId.CriticalReserveMilliunits, -500)], []),
        Trait("pack-frame", "Pack Frame", "You carry more but move slightly slower.", [D("pack-capacity", DerivedStatId.CarryCapacityGrams, 12000)], [D("pack-movement", DerivedStatId.MovementSpeedPermille, -70)], []),
        Trait("quiet-breath", "Quiet Breath", "Sustained stealth and aiming are easier, but sprint stamina is reduced.", [M("quiet-stealth", SkillId.Stealth, 5)], [D("quiet-stamina", DerivedStatId.MaximumStamina, -10)], [])
    ];

    public static IReadOnlyList<PerkDefinition> Perks { get; } =
    [
        P("sidearm-snapline", "Sidearm Snapline", "Queue or execute a quick sidearm response after drawing.", "ballistics", PerkBalanceClass.Behavior, [R(SkillId.Ballistics, 35)], [E("combat.action.unlock", "sidearm-snapline", 1)]),
        P("supported-fire", "Supported Fire", "Braced rifle attacks retain accuracy through sustained fire.", "ballistics", PerkBalanceClass.Hybrid, [R(SkillId.Ballistics, 50)], [E("combat.recoil.modify", "rifle", -180), E("combat.action.unlock", "brace", 1)]),
        P("breach-geometry", "Breach Geometry", "Shotguns and compact firearms gain a doorway breaching action.", "ballistics", PerkBalanceClass.OptionUnlock, [R(SkillId.Ballistics, 45), R(SkillId.Security, 30)], [E("interaction.solution.unlock", "ballistic-breach", 1)]),
        P("charge-discipline", "Charge Discipline", "Hold a directed-energy charge for a controlled release.", "directed-energy", PerkBalanceClass.Behavior, [R(SkillId.DirectedEnergy, 40)], [E("combat.action.unlock", "held-charge", 1)]),
        P("arc-conduction", "Arc Conduction", "Use conductive surfaces to redirect an electrical attack.", "directed-energy", PerkBalanceClass.OptionUnlock, [R(SkillId.DirectedEnergy, 55), R(SkillId.Science, 40)], [E("combat.targeting.unlock", "conductive-ricochet", 1)]),
        P("calculated-fuse", "Calculated Fuse", "Choose short, standard, or delayed explosive fuses.", "demolitions", PerkBalanceClass.Behavior, [R(SkillId.Demolitions, 35)], [E("combat.action.unlock", "select-fuse", 1)]),
        P("fragment-mapping", "Fragment Mapping", "Preview likely fragment exposure before committing an explosive.", "demolitions", PerkBalanceClass.OptionUnlock, [R(SkillId.Demolitions, 50), R(AttributeId.Awareness, 6)], [E("focus.knowledge.unlock", "fragment-map", 1)]),
        P("trap-reversal", "Trap Reversal", "Convert a disabled trap rather than merely disarming it.", "demolitions", PerkBalanceClass.OptionUnlock, [R(SkillId.Demolitions, 45), R(SkillId.Security, 35)], [E("interaction.solution.unlock", "trap-reversal", 1)]),
        P("guard-break", "Guard Break", "A committed melee strike can break an active block.", "melee", PerkBalanceClass.Behavior, [R(SkillId.Melee, 35)], [E("combat.action.unlock", "guard-break", 1)]),
        P("countermeasure", "Countermeasure", "A successful block opens a short counterattack window.", "melee", PerkBalanceClass.Behavior, [R(SkillId.Melee, 45), R(AttributeId.Reflex, 6)], [E("combat.action.unlock", "counterattack", 1)]),
        P("limb-hook", "Limb Hook", "Melee targeting can intentionally pull or impair a limb.", "melee", PerkBalanceClass.OptionUnlock, [R(SkillId.Melee, 50)], [E("combat.targeting.unlock", "limb-hook", 1)]),
        P("clinch-control", "Clinch Control", "Unarmed attacks can suppress an opponent's equipped weapon.", "unarmed", PerkBalanceClass.Behavior, [R(SkillId.Unarmed, 45)], [E("combat.action.unlock", "weapon-clinch", 1)]),
        Focus("rapid-intake", "Rapid Intake", "External target structures are identified immediately.", "focus.knowledge.unlock", "external-structure", 1),
        Focus("layer-separation", "Layer Separation", "Reveals armor layers and their condition.", "focus.knowledge.unlock", "armor-layers", 1),
        Focus("stress-mapping", "Stress Mapping", "Reveals trauma and the next functional threshold.", "focus.knowledge.unlock", "stress-threshold", 1),
        Focus("trauma-projection", "Trauma Projection", "Shows likely injuries and status effects.", "focus.knowledge.unlock", "trauma-projection", 1),
        Focus("component-isolation", "Component Isolation", "Permits exposed weapon and machine-component targeting.", "combat.targeting.unlock", "machine-components", 1),
        Focus("obscured-anatomy", "Obscured Anatomy", "Permits low-confidence targeting through partial concealment.", "combat.targeting.unlock", "obscured-parts", 1),
        Focus("sequence-compression", "Sequence Compression", "Repeated actions with one weapon cost less Focus.", "focus.cost.modify", "repeat-weapon", -100),
        Focus("continuity-reserve", "Continuity Reserve", "Raises bounded Focus queue capacity.", "focus.queue.modify", "capacity", 4),
        Focus("recoil-reconstruction", "Recoil Reconstruction", "Improves forecasts under accumulated recoil.", "focus.knowledge.unlock", "recoil-forecast", 1),
        Focus("exposure-geometry", "Exposure Geometry", "Shows explosive exposure and cover reduction.", "focus.knowledge.unlock", "explosive-exposure", 1),
        Focus("target-transfer", "Target Transfer", "Transfers an unstarted action after a target dies.", "focus.fallback.unlock", "target-transfer", 1),
        Focus("interrupt-reserve", "Interrupt Reserve", "Refunds additional reserved Focus after interruption.", "focus.cost.modify", "interruption-refund", 250),
        P("quiet-entry", "Quiet Entry", "Open a secured route without creating the ordinary noise event.", "stealth-security", PerkBalanceClass.OptionUnlock, [R(SkillId.Stealth, 35), R(SkillId.Security, 30)], [E("interaction.solution.unlock", "quiet-entry", 1)]),
        P("shadow-route", "Shadow Route", "Reveal traversal routes chosen for concealment rather than speed.", "stealth-security", PerkBalanceClass.OptionUnlock, [R(SkillId.Stealth, 50)], [E("world.discovery.reveal", "shadow-route", 1)]),
        P("alarm-ghost", "Alarm Ghost", "Bypass an alarm while leaving its monitoring state apparently normal.", "stealth-security", PerkBalanceClass.OptionUnlock, [R(SkillId.Security, 55), R(SkillId.Science, 35)], [E("interaction.solution.unlock", "alarm-ghost", 1)]),
        P("prepared-exit", "Prepared Exit", "Mark a retreat route that remains available after an alarm.", "stealth-security", PerkBalanceClass.Behavior, [R(SkillId.Stealth, 45), R(SkillId.Fieldcraft, 35)], [E("combat.action.unlock", "prepared-retreat", 1)]),
        P("field-jury-rig", "Field Jury-Rig", "Perform a temporary repair without a full workbench.", "technical", PerkBalanceClass.OptionUnlock, [R(SkillId.Engineering, 35)], [E("interaction.solution.unlock", "field-repair", 1)]),
        P("diagnostic-override", "Diagnostic Override", "Turn a diagnosed machine component into a control route.", "technical", PerkBalanceClass.OptionUnlock, [R(SkillId.Science, 50), R(SkillId.Engineering, 40)], [E("interaction.solution.unlock", "machine-control", 1)]),
        P("power-reroute", "Power Reroute", "Redirect a local power grid instead of repairing its original path.", "technical", PerkBalanceClass.OptionUnlock, [R(SkillId.Engineering, 50)], [E("interaction.solution.unlock", "power-reroute", 1)]),
        P("reverse-analysis", "Reverse Analysis", "A dismantled device can reveal a procedure or recipe principle.", "technical", PerkBalanceClass.OptionUnlock, [R(SkillId.Science, 45)], [E("world.discovery.reveal", "reverse-analysis", 1)]),
        P("triage-protocol", "Triage Protocol", "Stabilize a critical patient without a complete medical station.", "medicine-fieldcraft", PerkBalanceClass.OptionUnlock, [R(SkillId.Medicine, 35)], [E("interaction.solution.unlock", "field-stabilize", 1)]),
        P("anatomical-exploit", "Anatomical Exploit", "A fully diagnosed biological weakness becomes a special combat action.", "medicine-fieldcraft", PerkBalanceClass.Behavior, [R(SkillId.Medicine, 50)], [E("combat.action.unlock", "anatomical-exploit", 1)]),
        P("clean-harvest", "Clean Harvest", "Recover biological materials without contaminating them.", "medicine-fieldcraft", PerkBalanceClass.OptionUnlock, [R(SkillId.Fieldcraft, 40), R(SkillId.Medicine, 30)], [E("interaction.solution.unlock", "clean-harvest", 1)]),
        P("expedition-camp", "Expedition Camp", "Establish a camp that creates recovery and preparation options.", "medicine-fieldcraft", PerkBalanceClass.OptionUnlock, [R(SkillId.Fieldcraft, 45)], [E("interaction.solution.unlock", "expedition-camp", 1)]),
        P("leverage-ledger", "Leverage Ledger", "Use evidence, obligations, or known crimes instead of raw persuasion.", "social-commerce", PerkBalanceClass.OptionUnlock, [R(SkillId.Influence, 35), R(SkillId.Commerce, 30)], [E("dialogue.option.unlock", "leverage-ledger", 1)]),
        P("conditional-surrender", "Conditional Surrender", "Create a combat-to-dialogue transition when morale breaks.", "social-commerce", PerkBalanceClass.Behavior, [R(SkillId.Influence, 50)], [E("combat.action.unlock", "conditional-surrender", 1)]),
        P("network-credit", "Network Credit", "Convert trusted trade relationships into bounded deferred payment.", "social-commerce", PerkBalanceClass.OptionUnlock, [R(SkillId.Commerce, 45)], [E("economy.option.unlock", "network-credit", 1)]),
        P("mediated-exchange", "Mediated Exchange", "Arrange a three-party exchange when neither side accepts the other's goods.", "social-commerce", PerkBalanceClass.OptionUnlock, [R(SkillId.Influence, 45), R(SkillId.Commerce, 45)], [E("economy.option.unlock", "mediated-exchange", 1)])
    ];

    private static BackgroundDefinition Background(string id, string name, string description, AttributeId plus, AttributeId minus, SkillId skillA, SkillId skillB, string package, string[] items, string benefit) =>
        new(id, name, description, new Dictionary<AttributeId, int> { [plus] = 1, [minus] = -1 }, new Dictionary<SkillId, int> { [skillA] = 5, [skillB] = 5 }, [$"knowledge.{id}"], [$"dialogue.{id}"], new(package, items), benefit);
    private static ProgressionModifier M(string id, SkillId skill, int value) => new(id, id, null, skill, null, value, 1000, true);
    private static ProgressionModifier D(string id, DerivedStatId stat, int value) => new(id, id, null, null, stat, value, 1000, true);
    private static ProgressionModifier Tag(string id, string target, int value) => new(id, target, null, null, null, value, 1000, true);
    private static TraitDefinition Trait(string id, string name, string description, ProgressionModifier[] benefits, ProgressionModifier[] drawbacks, string[] incompatible) => new(id, name, description, benefits, drawbacks, incompatible, [$"trait.{id}"]);
    private static PerkRequirement R(SkillId skill, int minimum) => new(PerkRequirementKind.Skill, skill.ToString(), minimum);
    private static PerkRequirement R(AttributeId attribute, int minimum) => new(PerkRequirementKind.Attribute, attribute.ToString(), minimum);
    private static PerkEffectDefinition E(string type, string target, int value) => new(type, target, value, new Dictionary<string, string>());
    private static PerkDefinition P(string id, string name, string description, string family, PerkBalanceClass balance, PerkRequirement[] requirements, PerkEffectDefinition[] effects) => new(id, name, description, family, 1, balance, requirements, effects);
    private static PerkDefinition Focus(string id, string name, string description, string effectType, string target, int value) => P(id, name, description, "roentgen-focus", PerkBalanceClass.OptionUnlock, [new(PerkRequirementKind.Level, "level", 2)], [E(effectType, target, value)]);
}
