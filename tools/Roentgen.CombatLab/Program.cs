using System.Globalization;
using Roentgen.AI;
using Roentgen.Animation;
using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Generation;

namespace Roentgen.CombatLab;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string command = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
            return command switch
            {
                "duel" => Duel(args),
                "soak" => Soak(args),
                "armor" => Armor(args),
                "weapons" => Weapons(),
                "snapshot" => Snapshot(args),
                _ => Help()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int Duel(string[] args)
    {
        ulong seed = ReadUlong(args, "--seed", 0x524F454E5447454EUL);
        int ticks = ReadInt(args, "--ticks", 1800);
        (CombatEncounterRuntime runtime, CombatActorState player, CombatActorState enemy) = CreateDuel(seed);
        var ai = new BasicEnemyCombatController(runtime, player.Id);
        for (ulong tick = 1; tick <= (ulong)ticks && !player.IsDead && !enemy.IsDead; tick++)
        {
            runtime.State.Tick = tick;
            ai.Update(tick);
            if (tick % 18 == 1)
            {
                FixedVector3 origin = CombatSpatialModel.PartCenter(player, AnatomicalRegion.Head);
                FixedVector3 target = CombatSpatialModel.PartCenter(enemy, AnatomicalRegion.Torso);
                if (!runtime.QueueAttack(player.Id, origin, target - origin, enemy.Id) && runtime.State.GetWeapon(player.EquippedWeaponId).Ammunition == 0) runtime.QueueReload(player.Id);
            }
            runtime.Update(tick);
        }
        Console.WriteLine($"Tick:       {runtime.State.Tick}");
        Console.WriteLine($"Player HP:  {player.Health}/{player.MaximumHealth}");
        Console.WriteLine($"Enemy HP:   {enemy.Health}/{enemy.MaximumHealth}");
        Console.WriteLine($"Checksum:   {runtime.State.ComputeChecksum()}");
        foreach (CombatFeedbackEntry entry in runtime.State.Feedback.TakeLast(20)) Console.WriteLine($"{entry.Tick,5} {entry.Kind,-18} {entry.Message}");
        return 0;
    }

    private static int Soak(string[] args)
    {
        int seeds = ReadInt(args, "--seeds", 1000);
        int failures = 0;
        StateChecksum aggregate = StateChecksum.Zero;
        for (int index = 0; index < seeds; index++)
        {
            try
            {
                (CombatEncounterRuntime runtime, CombatActorState player, CombatActorState enemy) = CreateDuel(unchecked(0x524F454E5447454EUL + (ulong)index));
                for (ulong tick = 1; tick <= 360; tick++)
                {
                    runtime.State.Tick = tick;
                    if (tick % 20 == 1)
                    {
                        FixedVector3 origin = CombatSpatialModel.PartCenter(player, AnatomicalRegion.Head);
                        FixedVector3 target = CombatSpatialModel.PartCenter(enemy, AnatomicalRegion.Torso);
                        runtime.QueueAttack(player.Id, origin, target - origin, enemy.Id);
                    }
                    runtime.Update(tick);
                }
                StateChecksum checksum = runtime.State.ComputeChecksum();
                aggregate = new StateChecksum(aggregate.A ^ checksum.A, aggregate.B ^ checksum.B, aggregate.C ^ checksum.C, aggregate.D ^ checksum.D);
            }
            catch
            {
                failures++;
            }
        }
        Console.WriteLine($"Seeds:      {seeds}");
        Console.WriteLine($"Failures:   {failures}");
        Console.WriteLine($"Aggregate:  {aggregate}");
        return failures == 0 ? 0 : 2;
    }

    private static int Armor(string[] args)
    {
        int condition = ReadInt(args, "--condition", 1000);
        var (runtime, _, enemy) = CreateDuel(0x524F454E5447454EUL);
        var armor = new ArmorRuntimeState(
            StableIdFactory.CreateChild(enemy.Id, "armor", "combatlab"),
            "combatlab-plate",
            ArmorLayerKind.Primary,
            [AnatomicalRegion.Torso],
            [new KeyValuePair<DamageType, int>(DamageType.Kinetic, 10)],
            [new KeyValuePair<DamageType, int>(DamageType.Kinetic, 400)],
            condition);
        runtime.AddArmor(enemy.Id, armor);
        BodyPartRuntimeState torso = enemy.BodyParts.Single(part => part.Definition.Region == AnatomicalRegion.Torso);
        DamageResolution result = new DamageResolver().Apply(enemy, torso, [new DamageComponent(DamageType.Kinetic, 50)], 1000, false, 1);
        Console.WriteLine($"Raw:        {result.RawDamage}");
        Console.WriteLine($"Stopped:    {result.ArmorStopped}");
        Console.WriteLine($"Final:      {result.FinalDamage}");
        Console.WriteLine($"Condition:  {armor.ConditionPermille}");
        return 0;
    }

    private static int Weapons()
    {
        foreach (WeaponDefinition definition in CombatDefinitionCatalog.CreatePhase10Defaults().Weapons)
        {
            Console.WriteLine($"{definition.Id,-26} {definition.AttackKind,-10} range={definition.MaximumRangeMillimeters,6}mm magazine={definition.MagazineCapacity,2} damage={definition.Damage.Sum(static value => value.Amount),3}");
        }
        return 0;
    }

    private static int Snapshot(string[] args)
    {
        string output = ReadString(args, "--out", "phase10-combat-state.bin");
        (CombatEncounterRuntime runtime, CombatActorState player, CombatActorState enemy) = CreateDuel(0x524F454E5447454EUL);
        FixedVector3 origin = CombatSpatialModel.PartCenter(player, AnatomicalRegion.Head);
        FixedVector3 target = CombatSpatialModel.PartCenter(enemy, AnatomicalRegion.Torso);
        runtime.QueueAttack(player.Id, origin, target - origin, enemy.Id);
        for (ulong tick = 1; tick <= 60; tick++) runtime.Update(tick);
        var writer = new CanonicalWriter();
        runtime.State.WriteCanonical(writer);
        File.WriteAllBytes(output, writer.ToArray());
        Console.WriteLine(Path.GetFullPath(output));
        Console.WriteLine(runtime.State.ComputeChecksum());
        return 0;
    }

    private static (CombatEncounterRuntime Runtime, CombatActorState Player, CombatActorState Enemy) CreateDuel(ulong seedValue)
    {
        CampaignSeed seed = new(seedValue);
        var runtime = new CombatEncounterRuntime(seed, CombatDefinitionCatalog.CreatePhase10Defaults());
        GeneratedCharacterProduct playerProduct = Generate(seed, "player", "combatlab-player");
        GeneratedCharacterProduct enemyProduct = Generate(seed, "enemy", "combatlab-enemy");
        CombatActorState player = runtime.RegisterActor(new CharacterState(playerProduct.CharacterId, playerProduct.Appearance, FixedVector3.Zero), playerProduct.TargetParts, CombatActorRole.Player, "player", 180);
        CombatActorState enemy = runtime.RegisterActor(new CharacterState(enemyProduct.CharacterId, enemyProduct.Appearance, new FixedVector3(0, 0, -12_000)), enemyProduct.TargetParts, CombatActorRole.Hostile, "enemy", 160);
        StableId playerWeapon = StableIdFactory.CreateChild(player.Id, "weapon", "rifle");
        StableId enemyWeapon = StableIdFactory.CreateChild(enemy.Id, "weapon", "rifle");
        runtime.RegisterWeapon(playerWeapon, "phase10.rifle", player.Id, 20, 160);
        runtime.RegisterWeapon(enemyWeapon, "phase10.rifle", enemy.Id, 20, 160);
        runtime.Equip(player.Id, playerWeapon);
        runtime.Equip(enemy.Id, enemyWeapon);
        return (runtime, player, enemy);
    }

    private static GeneratedCharacterProduct Generate(CampaignSeed seed, string key, string archetype)
    {
        StableId id = StableIdFactory.Create("combatlab", $"{seed.Value}-{key}");
        return HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(seed, id, archetype));
    }

    private static string ReadString(string[] args, string name, string fallback)
    {
        int index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private static int ReadInt(string[] args, string name, int fallback) => int.TryParse(ReadString(args, name, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    private static ulong ReadUlong(string[] args, string name, ulong fallback) => ulong.TryParse(ReadString(args, name, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value) ? value : fallback;

    private static int Help()
    {
        Console.WriteLine("Roentgen CombatLab — Phase 10");
        Console.WriteLine("Commands: duel, soak, armor, weapons, snapshot");
        Console.WriteLine("Options: --seed N --ticks N --seeds N --condition N --out FILE");
        return 0;
    }
}
