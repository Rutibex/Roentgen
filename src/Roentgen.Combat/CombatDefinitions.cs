using Roentgen.Contracts;
using Roentgen.Entities;

namespace Roentgen.Combat;

public sealed record WeaponDefinition(
    string Id,
    string DisplayName,
    WeaponFamilyKind Family,
    CombatAttackKind AttackKind,
    IReadOnlyList<DamageComponent> Damage,
    int MagazineCapacity,
    int TicksBetweenAttacks,
    int WindupTicks,
    int RecoveryTicks,
    int ReloadTicks,
    int MaximumRangeMillimeters,
    int BaseSpreadPermille,
    int RecoilPerAttackPermille,
    int ConditionLossPerAttack,
    int ProjectileSpeedMillimetersPerSecond = 0,
    int ProjectileRadiusMillimeters = 20,
    int ExplosionRadiusMillimeters = 0,
    int FuseTicks = 0,
    int MeleeReachMillimeters = 0,
    int PelletCount = 1,
    int MeleeSweepRadiusMillimeters = 160)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        if (Damage.Count == 0) throw new InvalidOperationException($"Weapon '{Id}' has no damage profile.");
        if (MagazineCapacity < 0 || TicksBetweenAttacks < 0 || WindupTicks < 0 || RecoveryTicks < 0 || ReloadTicks < 0) throw new InvalidOperationException($"Weapon '{Id}' has invalid timing or capacity values.");
        if (MaximumRangeMillimeters <= 0 || BaseSpreadPermille < 0 || RecoilPerAttackPermille < 0 || ConditionLossPerAttack < 0) throw new InvalidOperationException($"Weapon '{Id}' has invalid accuracy or range values.");
        if (PelletCount <= 0) throw new InvalidOperationException($"Weapon '{Id}' pellet count must be positive.");
        if (MeleeSweepRadiusMillimeters < 0) throw new InvalidOperationException($"Weapon '{Id}' melee sweep radius cannot be negative.");
    }
}

public sealed class CombatDefinitionCatalog
{
    private readonly SortedDictionary<string, WeaponDefinition> _weapons = new(StringComparer.Ordinal);

    public IReadOnlyCollection<WeaponDefinition> Weapons => _weapons.Values;

    public void Register(WeaponDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (!_weapons.TryAdd(definition.Id, definition)) throw new InvalidOperationException($"Weapon definition '{definition.Id}' is already registered.");
    }

    public WeaponDefinition GetWeapon(string id) => _weapons.TryGetValue(id, out WeaponDefinition? definition) ? definition : throw new KeyNotFoundException($"Weapon definition '{id}' is not registered.");

    public static CombatDefinitionCatalog CreatePhase10Defaults()
    {
        var catalog = new CombatDefinitionCatalog();
        catalog.Register(new WeaponDefinition(
            "phase10.sidearm", "Service Sidearm", WeaponFamilyKind.SelfLoadingPistol, CombatAttackKind.Hitscan,
            [new DamageComponent(DamageType.Kinetic, 28), new DamageComponent(DamageType.Blunt, 4)],
            10, 12, 2, 8, 72, 45_000, 42, 125, 1));
        catalog.Register(new WeaponDefinition(
            "phase10.rifle", "Industrial Rifle", WeaponFamilyKind.Rifle, CombatAttackKind.Hitscan,
            [new DamageComponent(DamageType.Kinetic, 42), new DamageComponent(DamageType.Blunt, 8)],
            20, 8, 2, 6, 96, 90_000, 25, 85, 1));
        catalog.Register(new WeaponDefinition(
            "phase10.scrap-club", "Scrap Impact Tool", WeaponFamilyKind.ImprovisedMelee, CombatAttackKind.Melee,
            [new DamageComponent(DamageType.Blunt, 36), new DamageComponent(DamageType.Cutting, 8)],
            0, 42, 18, 24, 0, 2_200, 0, 0, 1, MeleeReachMillimeters: 2_200));
        catalog.Register(new WeaponDefinition(
            "phase10.grenade", "Fragmentation Charge", WeaponFamilyKind.Grenade, CombatAttackKind.Explosive,
            [new DamageComponent(DamageType.Kinetic, 42), new DamageComponent(DamageType.Blunt, 30), new DamageComponent(DamageType.Thermal, 12)],
            1, 90, 12, 30, 0, 28_000, 12, 0, 1, ProjectileSpeedMillimetersPerSecond: 18_000, ProjectileRadiusMillimeters: 45, ExplosionRadiusMillimeters: 6_000, FuseTicks: 120));
        catalog.Register(new WeaponDefinition(
            "phase10.creature-bite", "Industrial Maw", WeaponFamilyKind.CreatureNatural, CombatAttackKind.Melee,
            [new DamageComponent(DamageType.Cutting, 30), new DamageComponent(DamageType.Blunt, 16), new DamageComponent(DamageType.Biological, 8)],
            0, 48, 16, 28, 0, 1_900, 0, 0, 0, MeleeReachMillimeters: 1_900));
        return catalog;
    }
}
