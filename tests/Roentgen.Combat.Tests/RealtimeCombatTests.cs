using Roentgen.AI;
using Roentgen.Animation;
using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Generation;
using Roentgen.Simulation;

namespace Roentgen.Combat.Tests;

public sealed class RealtimeCombatTests
{
    [Fact]
    public void HitscanAttackDamagesAConsequentialBodyPart()
    {
        Scenario scenario = CreateScenario();
        FixedVector3 origin = CombatSpatialModel.PartCenter(scenario.Player, AnatomicalRegion.Head);
        FixedVector3 target = CombatSpatialModel.PartCenter(scenario.Enemy, AnatomicalRegion.Torso);
        Assert.True(scenario.Runtime.QueueAttack(scenario.Player.Id, origin, target - origin, scenario.Enemy.Id));
        RunUntil(scenario.Runtime, 20);
        Assert.True(scenario.Enemy.Health < scenario.Enemy.MaximumHealth);
        Assert.Contains(scenario.Runtime.State.Feedback, entry => entry.Kind == CombatFeedbackKind.BodyPartHit && entry.TargetId == scenario.Enemy.Id);
    }

    [Fact]
    public void ArmorThresholdAndResistanceReduceDamageAndDegrade()
    {
        Scenario scenario = CreateScenario();
        ArmorRuntimeState armor = CreateArmor(scenario.Enemy.Id, 1000);
        scenario.Runtime.AddArmor(scenario.Enemy.Id, armor);
        BodyPartRuntimeState torso = scenario.Enemy.BodyParts.Single(part => part.Definition.Region == AnatomicalRegion.Torso);
        int before = armor.ConditionPermille;
        DamageResolution result = new DamageResolver().Apply(
            scenario.Enemy,
            torso,
            [new DamageComponent(DamageType.Kinetic, 40)],
            1000,
            blocked: false,
            tick: 1);
        Assert.True(result.ArmorStopped > 0);
        Assert.True(result.FinalDamage < result.RawDamage);
        Assert.True(armor.ConditionPermille < before);
    }

    [Fact]
    public void ReloadUsesReserveAndCompletesOnTimeline()
    {
        Scenario scenario = CreateScenario();
        WeaponRuntimeState weapon = scenario.Runtime.State.GetWeapon(scenario.Player.EquippedWeaponId);
        weapon.Ammunition = 0;
        weapon.ReserveAmmunition = 7;
        Assert.True(scenario.Runtime.QueueReload(scenario.Player.Id));
        RunUntil(scenario.Runtime, 100);
        Assert.Equal(7, weapon.Ammunition);
        Assert.Equal(0, weapon.ReserveAmmunition);
        Assert.Contains(scenario.Runtime.State.Feedback, entry => entry.Kind == CombatFeedbackKind.ReloadCompleted);
    }

    [Fact]
    public void ExplosiveProjectileProducesExposureAndDamage()
    {
        Scenario scenario = CreateScenario("phase10.grenade", playerAmmo: 1, playerReserve: 0, enemyPosition: new FixedVector3(0, 0, -4_000));
        FixedVector3 origin = CombatSpatialModel.PartCenter(scenario.Player, AnatomicalRegion.Head);
        FixedVector3 target = CombatSpatialModel.PartCenter(scenario.Enemy, AnatomicalRegion.Torso);
        Assert.True(scenario.Runtime.QueueAttack(scenario.Player.Id, origin, target - origin, scenario.Enemy.Id));
        RunUntil(scenario.Runtime, 180);
        Assert.Contains(scenario.Runtime.State.Feedback, entry => entry.Kind == CombatFeedbackKind.Explosion);
        Assert.True(scenario.Enemy.Health < scenario.Enemy.MaximumHealth);
    }

    [Fact]
    public void CanonicalCombatStateRestoresByteIdentically()
    {
        Scenario scenario = CreateScenario();
        FixedVector3 origin = CombatSpatialModel.PartCenter(scenario.Player, AnatomicalRegion.Head);
        FixedVector3 target = CombatSpatialModel.PartCenter(scenario.Enemy, AnatomicalRegion.Torso);
        scenario.Runtime.QueueAttack(scenario.Player.Id, origin, target - origin, scenario.Enemy.Id);
        RunUntil(scenario.Runtime, 30);
        var writer = new CanonicalWriter();
        scenario.Runtime.State.WriteCanonical(writer);
        StateChecksum before = writer.ComputeChecksum();
        CombatWorldState restored = CombatWorldState.ReadCanonical(writer.ToArray());
        Assert.Equal(before, restored.ComputeChecksum());
        Assert.Equal(scenario.Runtime.State.Actors.Count, restored.Actors.Count);
        Assert.Equal(scenario.Runtime.State.Weapons.Count, restored.Weapons.Count);
    }

    [Fact]
    public void SimulationInstallerPersistsAndRestoresCombatState()
    {
        Scenario scenario = CreateScenario();
        using SimulationHost host = CreateHost(scenario.Runtime);
        host.RunTicks(5);
        SimulationSnapshot snapshot = host.CaptureSnapshot();
        int expectedHealth = scenario.Enemy.Health;
        scenario.Enemy.Health = 1;
        scenario.Runtime.PersistTo(host.StateStore);
        host.Restore(snapshot);
        Assert.Equal(expectedHealth, scenario.Runtime.State.GetActor(scenario.Enemy.Id).Health);
    }

    [Fact]
    public void BasicEnemyAiCanCompleteAHeadlessEncounter()
    {
        Scenario scenario = CreateScenario(enemyPosition: new FixedVector3(0, 0, -12_000));
        var ai = new BasicEnemyCombatController(scenario.Runtime, scenario.Player.Id);
        for (ulong tick = 1; tick <= 1800 && !scenario.Player.IsDead && !scenario.Enemy.IsDead; tick++)
        {
            scenario.Runtime.State.Tick = tick;
            ai.Update(tick);
            if (tick % 18 == 0 && !scenario.Player.IsDead)
            {
                FixedVector3 origin = CombatSpatialModel.PartCenter(scenario.Player, AnatomicalRegion.Head);
                FixedVector3 target = CombatSpatialModel.PartCenter(scenario.Enemy, AnatomicalRegion.Torso);
                scenario.Runtime.QueueAttack(scenario.Player.Id, origin, target - origin, scenario.Enemy.Id);
            }
            scenario.Runtime.Update(tick);
        }
        Assert.True(scenario.Player.IsDead || scenario.Enemy.IsDead || scenario.Player.Health < scenario.Player.MaximumHealth || scenario.Enemy.Health < scenario.Enemy.MaximumHealth);
    }

    [Fact]
    public void IdenticalCommandsProduceIdenticalCombatChecksums()
    {
        StateChecksum first = RunDeterministicDuel();
        StateChecksum second = RunDeterministicDuel();
        Assert.Equal(first, second);
    }

    private static StateChecksum RunDeterministicDuel()
    {
        Scenario scenario = CreateScenario(enemyPosition: new FixedVector3(0, 0, -10_000));
        for (ulong tick = 1; tick <= 240; tick++)
        {
            scenario.Runtime.State.Tick = tick;
            if (tick % 20 == 1)
            {
                FixedVector3 origin = CombatSpatialModel.PartCenter(scenario.Player, AnatomicalRegion.Head);
                FixedVector3 target = CombatSpatialModel.PartCenter(scenario.Enemy, AnatomicalRegion.Torso);
                scenario.Runtime.QueueAttack(scenario.Player.Id, origin, target - origin, scenario.Enemy.Id);
            }
            scenario.Runtime.Update(tick);
        }
        return scenario.Runtime.State.ComputeChecksum();
    }

    private static Scenario CreateScenario(string playerWeapon = "phase10.rifle", int playerAmmo = 20, int playerReserve = 80, FixedVector3? enemyPosition = null)
    {
        CampaignSeed seed = new(0x524F454E5447454EUL);
        var runtime = new CombatEncounterRuntime(seed, CombatDefinitionCatalog.CreatePhase10Defaults());
        GeneratedCharacterProduct playerProduct = Generate(seed, "player", "test-player");
        GeneratedCharacterProduct enemyProduct = Generate(seed, "enemy", "test-enemy");
        var playerState = new CharacterState(playerProduct.CharacterId, playerProduct.Appearance, FixedVector3.Zero);
        var enemyState = new CharacterState(enemyProduct.CharacterId, enemyProduct.Appearance, enemyPosition ?? new FixedVector3(0, 0, -8_000));
        CombatActorState player = runtime.RegisterActor(playerState, playerProduct.TargetParts, CombatActorRole.Player, "player", 180);
        CombatActorState enemy = runtime.RegisterActor(enemyState, enemyProduct.TargetParts, CombatActorRole.Hostile, "enemy", 160);
        StableId playerWeaponId = StableIdFactory.CreateChild(player.Id, "weapon", playerWeapon);
        runtime.RegisterWeapon(playerWeaponId, playerWeapon, player.Id, playerAmmo, playerReserve);
        runtime.Equip(player.Id, playerWeaponId);
        StableId enemyWeaponId = StableIdFactory.CreateChild(enemy.Id, "weapon", "phase10.rifle");
        runtime.RegisterWeapon(enemyWeaponId, "phase10.rifle", enemy.Id, 20, 100);
        runtime.Equip(enemy.Id, enemyWeaponId);
        return new Scenario(runtime, player, enemy);
    }

    private static GeneratedCharacterProduct Generate(CampaignSeed seed, string key, string archetype)
    {
        StableId id = StableIdFactory.Create("phase10-combat-test", key);
        return HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(seed, id, archetype));
    }

    private static void RunUntil(CombatEncounterRuntime runtime, ulong endingTick)
    {
        for (ulong tick = runtime.State.Tick + 1; tick <= endingTick; tick++) runtime.Update(tick);
    }

    private static ArmorRuntimeState CreateArmor(StableId actorId, int condition) => new(
        StableIdFactory.CreateChild(actorId, "armor", "test"),
        "test-armor",
        ArmorLayerKind.Primary,
        [AnatomicalRegion.Torso],
        [new KeyValuePair<DamageType, int>(DamageType.Kinetic, 10)],
        [new KeyValuePair<DamageType, int>(DamageType.Kinetic, 400)],
        condition);

    private static SimulationHost CreateHost(CombatEncounterRuntime runtime)
    {
        var entities = new EntityRegistry();
        var store = new SimulationStateStore();
        var commands = new DeterministicCommandQueue();
        var sequences = new CommandSequenceAllocator();
        var router = new CommandRouter();
        var events = new DeterministicEventBus();
        var checksums = new SimulationChecksumService();
        var services = new SimulationServiceRegistry();
        services.Register(entities);
        services.Register(store);
        services.Register(commands);
        services.Register(router);
        services.Register(events);
        checksums.Register(new EntityRegistryChecksumContributor(entities));
        checksums.Register(store);
        var host = new SimulationHost(runtime.CampaignSeed, new SimulationHostOptions(), entities, store, commands, sequences, router, events, checksums, services);
        CombatSimulationInstaller.Install(host, runtime);
        return host;
    }

    private sealed record Scenario(CombatEncounterRuntime Runtime, CombatActorState Player, CombatActorState Enemy);
}
