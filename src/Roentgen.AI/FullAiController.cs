using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Entities;
using Roentgen.Factions;
using Roentgen.Core;
using Roentgen.Navigation;
using Roentgen.Simulation;
using Roentgen.World;

namespace Roentgen.AI;

public sealed record FullAiSettings(
    PerceptionProfile Perception,
    int PreferredRangedDistanceMillimeters = 18_000,
    int SurrenderMoralePermille = 220,
    int RetreatHealthPermille = 350,
    int AttackCommitTicks = 12,
    int ReplanTicks = 30)
{
    public static FullAiSettings Default { get; } = new(new PerceptionProfile());
}

public sealed class FullAiController
{
    private readonly CombatEncounterRuntime runtime;
    private readonly StableId playerId;
    private readonly HierarchicalNavigationService navigation;
    private readonly PerceptionService perception;
    private readonly UtilityAiEvaluator utility;
    private readonly FullAiSettings settings;
    private readonly INavigationMovementQuery movement;
    private readonly INavigationInteractionHandler interactions;
    private readonly IFactionRelationResolver factionRelations;
    private readonly SortedDictionary<StableId, NavigationMotor> motors = [];
    private readonly SortedDictionary<StableId, NavigationAgentProfile> profiles = [];
    private readonly SortedDictionary<StableId, PerceptionProfile> perceptionProfiles = [];
    private readonly SortedDictionary<StableId, UtilityDecision> decisions = [];
    public AgentBrainRegistry Brains { get; } = new();
    public AiStimulusRegistry Stimuli { get; } = new();
    public CompanionCommandService CompanionCommands { get; } = new();

    public FullAiController(CombatEncounterRuntime runtime, StableId playerId, HierarchicalNavigationService navigation, IAiVisibilityQuery? visibility = null, INavigationMovementQuery? movement = null, INavigationInteractionHandler? interactions = null, FullAiSettings? settings = null, IFactionRelationResolver? factionRelations = null)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.playerId = playerId;
        this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        this.settings = settings ?? FullAiSettings.Default;
        this.movement = movement ?? UnconstrainedNavigationMovementQuery.Instance;
        this.interactions = interactions ?? AssumedNavigationInteractionHandler.Instance;
        perception = new PerceptionService(visibility);
        utility = new UtilityAiEvaluator();
        this.factionRelations = factionRelations ?? DistinctFactionHostilityResolver.Instance;
    }

    public IReadOnlyDictionary<StableId, UtilityDecision> Decisions => decisions;

    public void Register(CombatActorState actor, string roleId, NavigationAgentProfile? profile = null, PerceptionProfile? perceptionProfile = null)
    {
        if (!Brains.TryGet(actor.Id, out _)) Brains.Add(new AgentBrainState(actor.Id, roleId, actor.FactionId) { Disposition = actor.Role is CombatActorRole.Hostile or CombatActorRole.Creature ? AgentDisposition.Aggressive : AgentDisposition.Guarded });
        profiles[actor.Id] = profile ?? (actor.Role == CombatActorRole.Creature ? NavigationAgentProfile.Quadruped : NavigationAgentProfile.OrdinaryHumanoid);
        perceptionProfiles[actor.Id] = perceptionProfile ?? settings.Perception;
        motors[actor.Id] = new NavigationMotor(navigation, movement, interactions);
    }

    public void EmitStimulus(AiStimulus stimulus) => Stimuli.Emit(stimulus);

    public void Update(ulong tick)
    {
        Stimuli.Expire(tick); CompanionCommands.Expire(tick);
        if (!runtime.State.TryGetActor(playerId, out CombatActorState? player) || player is null || player.IsDead) return;
        var characters = runtime.State.Actors.ToDictionary(static value => value.Id, static value => value.Presentation);
        foreach (CombatActorState actor in runtime.State.Actors.Where(value => value.Id != playerId).OrderBy(static value => value.Id))
        {
            if (!Brains.TryGet(actor.Id, out AgentBrainState? brain) || brain is null || actor.IsDead || brain.Tier != AgentSimulationTier.Full) continue;
            PerceptionProfile profile = perceptionProfiles[actor.Id];
            brain.Awareness.Decay(tick, profile.MemoryDecayPerSecondPermille);
            bool hostileToPlayer = factionRelations.IsHostile(actor.FactionId, player.FactionId);
            if (hostileToPlayer)
            {
                _ = perception.Observe(actor.Presentation, player.Presentation, brain, profile, tick);
                _ = perception.Hear(actor.Presentation, brain, Stimuli.Stimuli, profile, tick);
            }
            UtilityContext context = BuildContext(actor, brain, player, tick);
            UtilityDecision decision = utility.Select(brain, context);
            decisions[actor.Id] = decision;
            Execute(actor, brain, player, decision, tick);
        }
        perception.ShareAlerts(Brains, characters, settings.Perception, tick);
    }

    private UtilityContext BuildContext(CombatActorState actor, AgentBrainState brain, CombatActorState player, ulong tick)
    {
        int health = actor.Health * 1000 / Math.Max(1, actor.MaximumHealth);
        int distance = checked((int)Math.Min(int.MaxValue, CombatMath.Length(player.Presentation.FeetPosition - actor.Presentation.FeetPosition)));
        bool hasAmmo = true; bool ready = true;
        if (!actor.EquippedWeaponId.IsEmpty && runtime.State.TryGetWeapon(actor.EquippedWeaponId, out WeaponRuntimeState? weapon) && weapon is not null)
        {
            hasAmmo = weapon.MagazineCapacity == 0 || weapon.Ammunition > 0;
            ready = !weapon.IsJammed && tick >= weapon.NextReadyTick;
        }
        int hostileAllies = runtime.State.Actors.Count(value => value.Id != actor.Id && factionRelations.IsFriendly(actor.FactionId, value.FactionId) && !value.IsDead && CombatMath.Length(value.Presentation.FeetPosition - actor.Presentation.FeetPosition) < 25_000);
        int threats = runtime.State.Actors.Count(value => value.Id != actor.Id && factionRelations.IsHostile(actor.FactionId, value.FactionId) && !value.IsDead && CombatMath.Length(value.Presentation.FeetPosition - actor.Presentation.FeetPosition) < 25_000);
        bool hasCommand = CompanionCommands.TryGet(actor.Id, out CompanionCommand? command);
        bool orderedHold = command?.Kind == CompanionCommandKind.Stay;
        WorldPoint actorPoint = ToWorldPoint(actor.Presentation.FeetPosition);
        WorldPoint threatPoint = ToWorldPoint(player.Presentation.FeetPosition);
        NavigationLocation? nearbyCover = navigation.FindNearestCover(actorPoint, threatPoint, 1_200);
        int coverQuality = nearbyCover.HasValue && DistanceSquared(actorPoint, nearbyCover.Value.WorldPosition) <= 150L * 150L ? 800 : 0;
        return new UtilityContext(tick, actor.Id, brain.Awareness.Level, health, actor.MoralePermille, brain.FearPermille, brain.HungerPermille, brain.FatiguePermille, brain.DutyPermille, distance, coverQuality, hasAmmo, ready, CanEscape(actor), threats > hostileAllies + 1, orderedHold, false, actor.Role == CombatActorRole.Ally, hasCommand);
    }

    private void Execute(CombatActorState actor, AgentBrainState brain, CombatActorState player, UtilityDecision decision, ulong tick)
    {
        if (brain.IsSurrendered) { Idle(actor); return; }
        switch (decision.Kind)
        {
            case UtilityActionKind.Attack: Attack(actor, player, tick); break;
            case UtilityActionKind.Reload: runtime.QueueReload(actor.Id); break;
            case UtilityActionKind.Advance: MoveToward(actor, brain.Awareness.ConfirmedTargetId.IsEmpty ? brain.Awareness.LastHeardPosition : player.Presentation.FeetPosition, tick, run: true); break;
            case UtilityActionKind.Investigate: MoveToward(actor, brain.Awareness.LastHeardPosition, tick, run: false); break;
            case UtilityActionKind.Retreat:
            case UtilityActionKind.Flee: Retreat(actor, player, tick); break;
            case UtilityActionKind.Surrender: Surrender(actor, brain); break;
            case UtilityActionKind.RaiseAlarm: RaiseAlarm(actor, brain, tick); break;
            case UtilityActionKind.Follow: ExecuteCompanionCommand(actor, brain, tick); break;
            case UtilityActionKind.HoldPosition: motors[actor.Id].Stop(actor.Presentation); break;
            case UtilityActionKind.TakeCover: TakeCover(actor, player, tick); break;
            default: Idle(actor); break;
        }
    }

    private void Attack(CombatActorState actor, CombatActorState player, ulong tick)
    {
        if (runtime.State.TryGetPendingAction(actor.Id, out _)) return;
        if (actor.EquippedWeaponId.IsEmpty || !runtime.State.TryGetWeapon(actor.EquippedWeaponId, out WeaponRuntimeState? weapon) || weapon is null) { MoveToward(actor, player.Presentation.FeetPosition, tick, true); return; }
        WeaponDefinition definition = runtime.Catalog.GetWeapon(weapon.DefinitionId);
        if (definition.MagazineCapacity > 0 && weapon.Ammunition <= 0) { runtime.QueueReload(actor.Id); return; }
        double distance = CombatMath.Length(player.Presentation.FeetPosition - actor.Presentation.FeetPosition);
        int desired = definition.AttackKind == CombatAttackKind.Melee ? Math.Max(700, definition.MeleeReachMillimeters - 100) : settings.PreferredRangedDistanceMillimeters;
        if (distance > desired) { MoveToward(actor, player.Presentation.FeetPosition, tick, true); return; }
        Face(actor.Presentation, player.Presentation.FeetPosition - actor.Presentation.FeetPosition);
        FixedVector3 origin = CombatSpatialModel.PartCenter(actor, actor.Role == CombatActorRole.Creature ? AnatomicalRegion.Jaw : AnatomicalRegion.Head);
        FixedVector3 target = CombatSpatialModel.PartCenter(player, AnatomicalRegion.Torso);
        bool accepted = runtime.QueueAttack(actor.Id, origin, target - origin, player.Id, StableId.Empty);
        if (accepted)
        {
            StableId stimulusId = StableIdFactory.CreateChild(actor.Id, "attack-stimulus", $"{tick}:{weapon.AttackSequence}");
            Stimuli.Emit(new AiStimulus(stimulusId, definition.AttackKind == CombatAttackKind.Explosive ? AiStimulusKind.Explosion : AiStimulusKind.Gunshot, actor.Id, origin, 1000, definition.AttackKind == CombatAttackKind.Melee ? 8_000 : 90_000, actor.FactionId, tick, tick + 90));
        }
    }

    private void MoveToward(CombatActorState actor, FixedVector3 target, ulong tick, bool run)
    {
        var destination = NavigationLocation.Outdoor(ToWorldPoint(target), checked((int)(target.YMillimeters / 10)));
        MoveToward(actor, destination, tick, run);
    }

    private void MoveToward(CombatActorState actor, NavigationLocation destination, ulong tick, bool run)
    {
        NavigationMotor motor = motors[actor.Id]; NavigationAgentProfile profile = profiles[actor.Id];
        _ = motor.SetDestination(actor.Presentation, destination, profile, tick);
        _ = motor.Tick(actor.Presentation, profile, tick, run);
        actor.Revision++;
    }

    private void Retreat(CombatActorState actor, CombatActorState player, ulong tick)
    {
        FixedVector3 delta = actor.Presentation.FeetPosition - player.Presentation.FeetPosition;
        FixedVector3 direction = CombatMath.Normalize(new FixedVector3(delta.XMillimeters, 0, delta.ZMillimeters));
        FixedVector3 destination = actor.Presentation.FeetPosition + CombatMath.ScaleDirection(direction, 35_000);
        actor.IsRetreating = true; actor.MoralePermille = Math.Max(0, actor.MoralePermille - 1);
        MoveToward(actor, destination, tick, true);
    }

    private static bool CanEscape(CombatActorState actor) => !actor.IsDead && actor.Presentation.Injury.LeftLegImpairmentPermille < 850 && actor.Presentation.Injury.RightLegImpairmentPermille < 850;

    private void Surrender(CombatActorState actor, AgentBrainState brain)
    {
        brain.IsSurrendered = true; brain.Disposition = AgentDisposition.Surrendered; brain.Revision++;
        actor.IsRetreating = false; actor.IsBlocking = false; actor.CurrentTargetId = StableId.Empty; actor.MoralePermille = 0;
        actor.Presentation.Velocity = FixedVector3.Zero; actor.Presentation.Action = CharacterActionKind.Idle; actor.Presentation.Attention = CharacterAttentionKind.Neutral;
        actor.Presentation.EquipmentPoseProfileId = "unarmed"; actor.Presentation.Revision++; actor.Revision++;
        motors[actor.Id].Stop(actor.Presentation);
    }

    private void RaiseAlarm(CombatActorState actor, AgentBrainState brain, ulong tick)
    {
        if (brain.HasRaisedAlarm) { AttackFallback(actor, tick); return; }
        brain.HasRaisedAlarm = true; brain.Revision++;
        StableId id = StableIdFactory.CreateChild(actor.Id, "alarm", tick.ToString());
        Stimuli.Emit(new AiStimulus(id, AiStimulusKind.Alarm, actor.Id, actor.Presentation.FeetPosition, 1000, 120_000, actor.FactionId, tick, tick + 300));
    }

    private void AttackFallback(CombatActorState actor, ulong tick)
    {
        if (!runtime.State.TryGetActor(playerId, out CombatActorState? player) || player is null) return;
        Attack(actor, player, tick);
    }

    private void ExecuteCompanionCommand(CombatActorState actor, AgentBrainState brain, ulong tick)
    {
        if (!CompanionCommands.TryGet(actor.Id, out CompanionCommand? command) || command is null) { Idle(actor); return; }
        switch (command.Kind)
        {
            case CompanionCommandKind.Follow:
                if (runtime.State.TryGetActor(command.IssuerId, out CombatActorState? followIssuer) && followIssuer is not null)
                    MoveToward(actor, followIssuer.Presentation.FeetPosition, tick, true);
                else _ = CompanionCommands.Fail(actor.Id, tick, "command issuer is unavailable");
                break;
            case CompanionCommandKind.Regroup:
                if (runtime.State.TryGetActor(command.IssuerId, out CombatActorState? regroupIssuer) && regroupIssuer is not null)
                {
                    if (CombatMath.Length(regroupIssuer.Presentation.FeetPosition - actor.Presentation.FeetPosition) <= 1_800)
                        _ = CompanionCommands.Complete(actor.Id, tick, "companion regrouped");
                    else MoveToward(actor, regroupIssuer.Presentation.FeetPosition, tick, true);
                }
                else _ = CompanionCommands.Fail(actor.Id, tick, "command issuer is unavailable");
                break;
            case CompanionCommandKind.MoveTo:
            case CompanionCommandKind.Interact:
                if (CombatMath.Length(command.TargetPosition - actor.Presentation.FeetPosition) <= 900)
                {
                    motors[actor.Id].Stop(actor.Presentation);
                    _ = CompanionCommands.Complete(actor.Id, tick, command.Kind == CompanionCommandKind.Interact ? "interaction position reached" : "destination reached");
                }
                else MoveToward(actor, command.TargetPosition, tick, true);
                break;
            case CompanionCommandKind.AttackTarget:
                if (runtime.State.TryGetActor(command.TargetId, out CombatActorState? target) && target is not null && !target.IsDead) Attack(actor, target, tick);
                else _ = CompanionCommands.Complete(actor.Id, tick, "target neutralized or unavailable");
                break;
            case CompanionCommandKind.Stay:
            case CompanionCommandKind.WaitUntil:
                motors[actor.Id].Stop(actor.Presentation);
                break;
            case CompanionCommandKind.AvoidCombat:
                CombatActorState? avoidThreat = FindNearestThreat(actor);
                if (avoidThreat is not null) Retreat(actor, avoidThreat, tick);
                else if (runtime.State.TryGetActor(command.IssuerId, out CombatActorState? safeIssuer) && safeIssuer is not null) MoveToward(actor, safeIssuer.Presentation.FeetPosition, tick, true);
                else Idle(actor);
                break;
            case CompanionCommandKind.TakeCover:
                CombatActorState? coverThreat = FindNearestThreat(actor);
                if (coverThreat is not null) TakeCover(actor, coverThreat, tick); else Idle(actor);
                break;
            case CompanionCommandKind.PreferRanged:
                SelectCompanionWeapon(actor, preferMelee: false, tick);
                break;
            case CompanionCommandKind.PreferMelee:
                SelectCompanionWeapon(actor, preferMelee: true, tick);
                break;
            case CompanionCommandKind.HealSelf:
            case CompanionCommandKind.HealPlayer:
                _ = CompanionCommands.Fail(actor.Id, tick, "no authoritative medical-item provider is attached to companion AI");
                break;
            default:
                _ = CompanionCommands.Fail(actor.Id, tick, "unsupported companion command");
                break;
        }
        brain.CurrentDestination = command.TargetPosition; brain.CurrentDestinationId = command.TargetId; brain.Revision++;
    }

    private void SelectCompanionWeapon(CombatActorState actor, bool preferMelee, ulong tick)
    {
        WeaponRuntimeState? selected = runtime.State.Weapons
            .Where(value => value.OwnerId == actor.Id)
            .Select(value => (Weapon: value, Definition: runtime.Catalog.GetWeapon(value.DefinitionId)))
            .Where(value => preferMelee ? value.Definition.AttackKind == CombatAttackKind.Melee : value.Definition.AttackKind != CombatAttackKind.Melee)
            .OrderBy(value => value.Weapon.Id)
            .Select(value => value.Weapon)
            .FirstOrDefault();
        if (selected is null)
        {
            _ = CompanionCommands.Fail(actor.Id, tick, preferMelee ? "no melee weapon is available" : "no ranged weapon is available");
            return;
        }
        runtime.Equip(actor.Id, selected.Id);
        actor.Presentation.EquipmentPoseProfileId = preferMelee ? "melee" : "rifle";
        actor.Presentation.Revision++;
        _ = CompanionCommands.Complete(actor.Id, tick, preferMelee ? "melee weapon preferred" : "ranged weapon preferred");
    }

    private CombatActorState? FindNearestThreat(CombatActorState actor) => runtime.State.Actors
        .Where(value => value.Id != actor.Id && !value.IsDead && (value.Role is CombatActorRole.Hostile or CombatActorRole.Creature))
        .OrderBy(value => CombatMath.Length(value.Presentation.FeetPosition - actor.Presentation.FeetPosition))
        .ThenBy(static value => value.Id)
        .FirstOrDefault();

    private void TakeCover(CombatActorState actor, CombatActorState player, ulong tick)
    {
        NavigationLocation? cover = navigation.FindNearestCover(ToWorldPoint(actor.Presentation.FeetPosition), ToWorldPoint(player.Presentation.FeetPosition), 1_200);
        if (cover.HasValue)
        {
            MoveToward(actor, cover.Value, tick, true);
            return;
        }
        FixedVector3 delta = player.Presentation.FeetPosition - actor.Presentation.FeetPosition;
        var lateral = new FixedVector3(-delta.ZMillimeters, 0, delta.XMillimeters);
        FixedVector3 direction = CombatMath.Normalize(lateral);
        MoveToward(actor, actor.Presentation.FeetPosition + CombatMath.ScaleDirection(direction, 8_000), tick, true);
    }

    private static WorldPoint ToWorldPoint(FixedVector3 value) => new(checked((int)(value.XMillimeters / 10)), checked((int)(value.ZMillimeters / 10)));

    private static long DistanceSquared(WorldPoint left, WorldPoint right)
    {
        long dx = left.XCentimeters - right.XCentimeters;
        long dz = left.ZCentimeters - right.ZCentimeters;
        return checked(dx * dx + dz * dz);
    }

    private static void Face(CharacterState character, FixedVector3 direction)
    {
        if (direction.XMillimeters == 0 && direction.ZMillimeters == 0) return;
        character.FacingYawUnits = (int)Math.Round(Math.Atan2(direction.XMillimeters, direction.ZMillimeters) * 10000.0, MidpointRounding.AwayFromZero);
    }

    private static void Idle(CombatActorState actor)
    {
        actor.Presentation.Velocity = FixedVector3.Zero;
        if (actor.Action == CombatActionKind.None) actor.Presentation.Action = CharacterActionKind.Idle;
        actor.Presentation.Revision++; actor.Revision++;
    }
}

public sealed class FullAiSimulationSystem(FullAiController controller) : ISimulationSystem, IPhasedSimulationSystem
{
    public string SystemId => "ai.system.full";
    public int Order => 100;
    public SimulationSystemPhase Phase => SimulationSystemPhase.ArtificialIntelligence;
    public void Update(SimulationTickContext context) => controller.Update(context.Tick);
}
