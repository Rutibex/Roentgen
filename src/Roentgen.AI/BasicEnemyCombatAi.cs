using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.AI;

public sealed record BasicCombatAiSettings(
    int DetectionRangeMillimeters = 55_000,
    int RangedPreferredDistanceMillimeters = 18_000,
    int RetreatHealthPermille = 220,
    int WalkSpeedMillimetersPerSecond = 2_400,
    int CreatureSpeedMillimetersPerSecond = 3_800);

public sealed class BasicEnemyCombatController
{
    private readonly CombatEncounterRuntime _runtime;
    private readonly StableId _playerActorId;
    private readonly BasicCombatAiSettings _settings;

    public BasicEnemyCombatController(CombatEncounterRuntime runtime, StableId playerActorId, BasicCombatAiSettings? settings = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        if (playerActorId.IsEmpty) throw new ArgumentException("Player actor ID cannot be empty.", nameof(playerActorId));
        _playerActorId = playerActorId;
        _settings = settings ?? new BasicCombatAiSettings();
    }

    public void Update(ulong tick)
    {
        if (!_runtime.State.TryGetActor(_playerActorId, out CombatActorState? player) || player is null || player.IsDead) return;
        foreach (CombatActorState actor in _runtime.State.Actors.Where(static value => value.Role is CombatActorRole.Hostile or CombatActorRole.Creature).OrderBy(static value => value.Id))
        {
            if (actor.IsDead) continue;
            actor.CurrentTargetId = player.Id;
            actor.Presentation.Attention = CharacterAttentionKind.TrackActor;
            actor.Presentation.AttentionTargetId = player.Id;
            actor.Presentation.AttentionTargetPosition = player.Presentation.FeetPosition;
            FixedVector3 delta = player.Presentation.FeetPosition - actor.Presentation.FeetPosition;
            double distance = CombatMath.Length(delta);
            int perceptionPermille = Math.Clamp(1000 - actor.Presentation.Injury.HeadTraumaPermille * 3 / 4, 250, 1000);
            int detectionRange = _settings.DetectionRangeMillimeters * perceptionPermille / 1000;
            if (distance > detectionRange)
            {
                Idle(actor);
                continue;
            }
            int healthPermille = actor.Health * 1000 / Math.Max(1, actor.MaximumHealth);
            if (healthPermille <= _settings.RetreatHealthPermille && actor.MoralePermille < 500)
            {
                if (!actor.IsRetreating)
                {
                    _runtime.State.AddFeedback(tick, CombatFeedbackKind.ActorRetreated, actor.Id, player.Id, StableId.Empty, actor.Presentation.FeetPosition, actor.Presentation.FeetPosition, healthPermille, "actor began retreating");
                }
                actor.IsRetreating = true;
                Move(actor, new FixedVector3(-delta.XMillimeters, 0, -delta.ZMillimeters), _settings.WalkSpeedMillimetersPerSecond, CharacterActionKind.Run);
                continue;
            }
            actor.IsRetreating = false;
            if (actor.EquippedWeaponId.IsEmpty || !_runtime.State.TryGetWeapon(actor.EquippedWeaponId, out WeaponRuntimeState? weapon) || weapon is null)
            {
                Approach(actor, delta, distance, _settings.WalkSpeedMillimetersPerSecond);
                continue;
            }
            WeaponDefinition definition = _runtime.Catalog.GetWeapon(weapon.DefinitionId);
            if (_runtime.State.TryGetPendingAction(actor.Id, out _)) continue;
            if (definition.MagazineCapacity > 0 && weapon.Ammunition <= 0)
            {
                _runtime.QueueReload(actor.Id);
                continue;
            }
            int attackDistance = definition.AttackKind == CombatAttackKind.Melee ? Math.Max(700, definition.MeleeReachMillimeters - 100) : _settings.RangedPreferredDistanceMillimeters;
            if (distance > attackDistance)
            {
                int speed = actor.Role == CombatActorRole.Creature ? _settings.CreatureSpeedMillimetersPerSecond : _settings.WalkSpeedMillimetersPerSecond;
                Approach(actor, delta, distance, speed);
                continue;
            }
            if (definition.AttackKind != CombatAttackKind.Melee && distance < _settings.RangedPreferredDistanceMillimeters / 3)
            {
                Move(actor, new FixedVector3(-delta.XMillimeters, 0, -delta.ZMillimeters), _settings.WalkSpeedMillimetersPerSecond, CharacterActionKind.Run);
                continue;
            }
            Face(actor, delta);
            FixedVector3 origin = CombatSpatialModel.PartCenter(actor, actor.Role == CombatActorRole.Creature ? AnatomicalRegion.Jaw : AnatomicalRegion.Head);
            FixedVector3 target = CombatSpatialModel.PartCenter(player, AnatomicalRegion.Torso);
            _runtime.QueueAttack(actor.Id, origin, target - origin, player.Id, StableId.Empty);
            _ = tick;
        }
    }

    private static void Approach(CombatActorState actor, FixedVector3 delta, double distance, int speed)
    {
        if (distance < 1) { Idle(actor); return; }
        Move(actor, delta, speed, speed > 3_000 ? CharacterActionKind.Run : CharacterActionKind.Walk);
    }

    private static void Move(CombatActorState actor, FixedVector3 direction, int speedMillimetersPerSecond, CharacterActionKind action)
    {
        FixedVector3 normalized = CombatMath.Normalize(new FixedVector3(direction.XMillimeters, 0, direction.ZMillimeters));
        int legImpairment = Math.Max(actor.Presentation.Injury.LeftLegImpairmentPermille, actor.Presentation.Injury.RightLegImpairmentPermille);
        int movementPermille = Math.Clamp(1000 - legImpairment * 3 / 4, 180, 1000);
        int effectiveSpeed = speedMillimetersPerSecond * movementPermille / 1000;
        FixedVector3 velocity = CombatMath.ScaleDirection(normalized, effectiveSpeed);
        FixedVector3 step = new(velocity.XMillimeters / 60, 0, velocity.ZMillimeters / 60);
        actor.Presentation.FeetPosition += step;
        actor.Presentation.Velocity = velocity;
        actor.Presentation.Action = action;
        actor.Presentation.LocomotionPhasePermille = (actor.Presentation.LocomotionPhasePermille + Math.Max(1, effectiveSpeed / 150)) % 1000;
        Face(actor, direction);
        actor.Presentation.Revision++;
        actor.Revision++;
    }

    private static void Face(CombatActorState actor, FixedVector3 direction)
    {
        if (direction.XMillimeters == 0 && direction.ZMillimeters == 0) return;
        actor.Presentation.FacingYawUnits = (int)Math.Round(Math.Atan2(direction.XMillimeters, direction.ZMillimeters) * 10000.0, MidpointRounding.AwayFromZero);
    }

    private static void Idle(CombatActorState actor)
    {
        actor.Presentation.Velocity = FixedVector3.Zero;
        if (actor.Action == CombatActionKind.None) actor.Presentation.Action = CharacterActionKind.Idle;
    }
}

public sealed class BasicEnemyCombatAiSystem(BasicEnemyCombatController controller) : ISimulationSystem, IPhasedSimulationSystem
{
    public string SystemId => "ai.system.basic-combat";
    public int Order => 100;
    public SimulationSystemPhase Phase => SimulationSystemPhase.ArtificialIntelligence;
    public void Update(SimulationTickContext context) => controller.Update(context.Tick);
}
