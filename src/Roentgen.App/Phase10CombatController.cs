using Microsoft.Xna.Framework;
using Roentgen.AI;
using Roentgen.Animation;
using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Factions;
using Roentgen.Generation;
using Roentgen.Items;
using Roentgen.Physics;
using Roentgen.Rendering;
using Roentgen.Simulation;

namespace Roentgen.App;

/// <summary>
/// The real-time combat host retained from Phase 10, now backed by the authoritative
/// Phase 13 item world and driven by Phase 14 perception, navigation and utility AI.
/// </summary>

internal sealed record CompanionInterfaceSnapshot(
    StableId CompanionId,
    string DisplayName,
    int Health,
    int MaximumHealth,
    int StaminaPermille,
    int MoralePermille,
    string WeaponName,
    int WeaponAmmunition,
    int WeaponReserve,
    string Disposition,
    string CurrentAction,
    string CurrentOrder,
    int FearPermille,
    int DutyPermille,
    double DistanceMeters,
    IReadOnlyList<string> Armor,
    IReadOnlyList<string> RecentOrders);

internal sealed class Phase10CombatController
{
    private readonly CombatEncounterRuntime _runtime;
    private readonly FullAiController _ai;
    private readonly Phase13ItemRuntime _items;
    private readonly StableId _playerId;
    private readonly StableId _sidearmId;
    private readonly StableId _rifleId;
    private readonly StableId _meleeId;
    private readonly StableId _grenadeId;
    private readonly StableId[] _authoritativeWeaponIds;
    private readonly HashSet<StableId> _controlledActors = [];
    private StableId _companionId;
    private string _companionDisplayName = "Companion";
    private bool _blocking;
    private ulong _lastFootstepTick;

    public Phase10CombatController(
        Phase9WorldScene scene,
        PlayerSimulationRuntime playerRuntime,
        IPlayerCollisionWorld collisionWorld,
        Phase13ItemRuntime items,
        Phase14WorldAgentRuntime agents,
        SocietyWorldState? society = null,
        ICombatCapabilityProvider? capabilities = null,
        int playerMaximumHealth = 180)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(playerRuntime);
        _items = items ?? throw new ArgumentNullException(nameof(items));
        ArgumentNullException.ThrowIfNull(agents);
        CampaignSeed seed = new(0x524F454E5447454EUL);
        CombatDefinitionCatalog catalog = CombatDefinitionCatalog.CreatePhase10Defaults();
        _sidearmId = items.SidearmId;
        _rifleId = items.RifleId;
        _meleeId = items.MeleeId;
        _authoritativeWeaponIds = [_sidearmId, _rifleId, _meleeId];
        foreach (StableId itemId in _authoritativeWeaponIds) catalog.Register(items.CompileCombatDefinition(itemId));

        _runtime = new CombatEncounterRuntime(seed, catalog, occlusion: new PlayerCollisionCombatOcclusion(collisionWorld), capabilities: capabilities);
        _playerId = playerRuntime.Player.Id;
        StableId playerProductId = StableIdFactory.Create("phase14-player-product", "regression-player");
        GeneratedCharacterProduct playerProduct = HumanoidCharacterGenerator.Generate(new CharacterGenerationRequest(seed, playerProductId, "diagnostic-field-operator"));
        var playerPresentation = new CharacterState(_playerId, playerProduct.Appearance, playerRuntime.Player.FeetPosition)
        {
            EquipmentPoseProfileId = "rifle"
        };
        CombatActorState playerActor = _runtime.RegisterActor(playerPresentation, playerProduct.TargetParts, CombatActorRole.Player, FactionIds.Player, Math.Max(1, playerMaximumHealth));
        RegisterAuthoritativeWeapon(_sidearmId);
        RegisterAuthoritativeWeapon(_rifleId);
        RegisterAuthoritativeWeapon(_meleeId);
        _grenadeId = StableIdFactory.CreateChild(_playerId, "combat-weapon", "phase14-explosive-charge");
        _runtime.RegisterWeapon(_grenadeId, "phase10.grenade", _playerId, 1, 0);
        _runtime.Equip(_playerId, _sidearmId);
        foreach (ArmorRuntimeState armor in items.CompileCombatArmor(_playerId)) _runtime.AddArmor(_playerId, armor);

        bool companionAssigned = false;
        foreach (Phase9CharacterHandle handle in scene.CharacterHandles)
        {
            CombatActorRole role = ResolveRole(handle.Product.Appearance);
            StableId faction = role switch
            {
                CombatActorRole.Hostile or CombatActorRole.Creature => FactionIds.BlackwaterRecoveryAuthority,
                CombatActorRole.Ally => FactionIds.ProspectClinicCompact,
                _ => FactionIds.Civilians
            };
            int health = role == CombatActorRole.Creature ? 220 : role == CombatActorRole.Hostile ? 145 : 120;
            CombatActorState actor = _runtime.RegisterActor(handle.State, handle.Product.TargetParts, role, faction, health);
            bool useFullAi = role is CombatActorRole.Hostile or CombatActorRole.Creature || (role == CombatActorRole.Ally && !companionAssigned);
            if (useFullAi)
            {
                if (role == CombatActorRole.Ally) { companionAssigned = true; _companionId = actor.Id; _companionDisplayName = string.IsNullOrWhiteSpace(handle.DisplayName) ? "Companion" : handle.DisplayName; }
                scene.TakeCombatControl(actor.Id);
                _controlledActors.Add(actor.Id);
                string definition = role == CombatActorRole.Creature ? "phase10.creature-bite" : "phase10.rifle";
                int ammunition = role == CombatActorRole.Creature ? 0 : 20;
                StableId weaponId = StableIdFactory.CreateChild(actor.Id, "combat-weapon", definition);
                _runtime.RegisterWeapon(weaponId, definition, actor.Id, ammunition, role == CombatActorRole.Creature ? 0 : 120);
                _runtime.Equip(actor.Id, weaponId);
                if (role == CombatActorRole.Hostile) _runtime.AddArmor(actor.Id, CreateFieldArmor(actor.Id, "hostile-scrap-armor", 720));
            }
        }

        _ai = new FullAiController(
            _runtime,
            _playerId,
            agents.Navigation,
            new CollisionAiVisibilityQuery(collisionWorld),
            agents.Movement,
            agents.Interactions,
            factionRelations: society is null ? null : new SocietyFactionRelationResolver(society));
        foreach (CombatActorState actor in _runtime.State.Actors.Where(actor => _controlledActors.Contains(actor.Id)))
            _ai.Register(actor, actor.Role.ToString(), actor.Role == CombatActorRole.Creature ? Roentgen.Navigation.NavigationAgentProfile.Quadruped : Roentgen.Navigation.NavigationAgentProfile.OrdinaryHumanoid);
        agents.AttachCombatAi(_ai, _controlledActors);
        _items.EquipActiveWeapon(_sidearmId);
        _items.SynchronizeCombatArmor(playerActor);
    }

    public CombatEncounterRuntime Runtime => _runtime;
    public FullAiController Ai => _ai;
    public CombatPresentationSnapshot Presentation => _runtime.CreatePresentation(_playerId);
    public int LivingHostiles => _runtime.State.Actors.Count(actor => (actor.Role is CombatActorRole.Hostile or CombatActorRole.Creature) && !actor.IsDead);
    public int SurrenderedHostiles => _ai.Brains.Agents.Count(brain => brain.IsSurrendered);
    public StableId CompanionId => _companionId;
    public string LastCompanionCommandStatus { get; private set; } = "companion ready";

    public CompanionInterfaceSnapshot? BuildCompanionInterfaceSnapshot()
    {
        if (_companionId.IsEmpty || !_runtime.State.TryGetActor(_companionId, out CombatActorState? actor) || actor is null) return null;
        _ai.Brains.TryGet(_companionId, out AgentBrainState? brain);
        string weaponName = "Unarmed";
        int ammunition = 0;
        int reserve = 0;
        if (!actor.EquippedWeaponId.IsEmpty && _runtime.State.TryGetWeapon(actor.EquippedWeaponId, out WeaponRuntimeState? weapon) && weapon is not null)
        {
            weaponName = FriendlyIdentifier(weapon.DefinitionId);
            ammunition = weapon.Ammunition;
            reserve = weapon.ReserveAmmunition;
        }
        string currentOrder = _ai.CompanionCommands.TryGet(_companionId, out CompanionCommand? command) && command is not null
            ? FriendlyIdentifier(command.Kind.ToString())
            : "No standing order";
        CombatActorState playerActor = _runtime.State.GetActor(_playerId);
        FixedVector3 delta = actor.Presentation.FeetPosition - playerActor.Presentation.FeetPosition;
        double distance = Math.Sqrt((double)delta.XMillimeters * delta.XMillimeters + (double)delta.YMillimeters * delta.YMillimeters + (double)delta.ZMillimeters * delta.ZMillimeters) / 1000.0;
        string[] armor = actor.Armor.OrderBy(static value => value.Layer).ThenBy(static value => value.Id).Select(value => $"{FriendlyIdentifier(value.ProfileId)} — condition {value.ConditionPermille / 10.0:F0}%").ToArray();
        string[] recent = _ai.CompanionCommands.History.TakeLast(6).Reverse().Select(value => $"{FriendlyIdentifier(value.Status.ToString())}: {FriendlyIdentifier(value.Message)}").ToArray();
        return new CompanionInterfaceSnapshot(
            actor.Id,
            _companionDisplayName,
            actor.Health,
            actor.MaximumHealth,
            actor.StaminaPermille,
            actor.MoralePermille,
            weaponName,
            ammunition,
            reserve,
            FriendlyIdentifier(brain?.Disposition.ToString() ?? "Calm"),
            FriendlyIdentifier(brain?.CurrentActionId ?? actor.Action.ToString()),
            currentOrder,
            brain?.FearPermille ?? 0,
            brain?.DutyPermille ?? 0,
            distance,
            armor,
            recent);
    }

    private static string FriendlyIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        string tail = value.Contains('.') ? value[(value.LastIndexOf('.') + 1)..] : value;
        tail = tail.Replace('-', ' ').Replace('_', ' ');
        var chars = new List<char>(tail.Length + 8);
        for (int index = 0; index < tail.Length; index++)
        {
            char c = tail[index];
            if (index > 0 && char.IsUpper(c) && char.IsLower(tail[index - 1])) chars.Add(' ');
            chars.Add(c);
        }
        string text = string.Join(' ', new string(chars.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(text)) return "Unknown";
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    public void Update(ulong tick, PlayerState player, CombatInputFrame input)
    {
        _runtime.State.Tick = tick;
        int facingYawUnits = (int)Math.Round(player.BodyYawUnits / 10000.0 * Math.PI / 180.0 * 10000.0, MidpointRounding.AwayFromZero);
        _runtime.SynchronizeActorTransform(_playerId, player.FeetPosition, player.Velocity, facingYawUnits);
        EmitPlayerMovementStimulus(tick, player);
        RefreshAuthoritativeAmmunition();
        if (input.Buttons.HasFlag(CombatInputButtons.EquipSidearmPressed)) EquipPlayer(_sidearmId);
        if (input.Buttons.HasFlag(CombatInputButtons.EquipRiflePressed)) EquipPlayer(_rifleId);
        if (input.Buttons.HasFlag(CombatInputButtons.EquipMeleePressed)) EquipPlayer(_meleeId);
        if (input.Buttons.HasFlag(CombatInputButtons.ThrowGrenadePressed) && _items.TryConsumeThrowable())
        {
            StableId previous = _runtime.State.GetActor(_playerId).EquippedWeaponId;
            WeaponRuntimeState grenade = _runtime.State.GetWeapon(_grenadeId);
            grenade.Ammunition = 1;
            _runtime.Equip(_playerId, _grenadeId);
            QueuePlayerAttack(player, emitStimulus: true);
            _runtime.Equip(_playerId, previous);
        }
        if (input.Buttons.HasFlag(CombatInputButtons.ReloadPressed)) _runtime.QueueReload(_playerId);
        bool block = input.Buttons.HasFlag(CombatInputButtons.BlockHeld);
        if (block != _blocking)
        {
            _blocking = block;
            _runtime.SetBlocking(_playerId, block);
        }
        if (input.Buttons.HasFlag(CombatInputButtons.FireHeld)) QueuePlayerAttack(player, emitStimulus: true);
        _ai.Update(tick);
        _runtime.Update(tick);
        foreach (StableId itemId in _authoritativeWeaponIds) _items.SynchronizeCombatWeapon(_runtime.State.GetWeapon(itemId));
        _items.SynchronizeCombatArmor(_runtime.State.GetActor(_playerId));
    }


    public void RefreshFromItemWorld()
    {
        foreach (StableId itemId in _authoritativeWeaponIds)
        {
            ItemInstanceState item = _items.World.GetItem(itemId);
            WeaponRuntimeState weapon = _runtime.State.GetWeapon(itemId);
            weapon.Ammunition = Math.Clamp(item.AmmunitionLoad?.LoadedCount ?? 0, 0, weapon.MagazineCapacity);
            weapon.ReserveAmmunition = _items.CountCompatibleAmmunition(_playerId, itemId);
            weapon.ConditionPermille = item.Condition.OverallPermille;
            weapon.IsJammed = false;
            weapon.HeatPermille = 0;
            weapon.RecoilPermille = 0;
            weapon.Revision++;
        }
        CombatActorState player = _runtime.State.GetActor(_playerId);
        foreach (ArmorRuntimeState armor in player.Armor)
        {
            if (_items.World.TryGetItem(armor.Id, out ItemInstanceState? item) && item is not null)
            {
                armor.ConditionPermille = item.Condition.OverallPermille;
                armor.Revision++;
            }
        }
        StableId active = _items.ActiveWeaponId;
        if (_authoritativeWeaponIds.Contains(active)) _runtime.Equip(_playerId, active);
    }

    public CompanionCommandResult CommandCompanion(StableId companionId, CompanionCommandKind kind, StableId targetId, FixedVector3 targetPosition, ulong tick)
    {
        if (companionId.IsEmpty)
        {
            var unavailable = new CompanionCommandResult(StableId.Empty, CompanionCommandStatus.Rejected, "no companion is available", tick);
            LastCompanionCommandStatus = unavailable.Message;
            return unavailable;
        }
        StableId commandId = StableIdFactory.CreateChild(companionId, "companion-command", $"{kind}:{tick}:{targetId}");
        CompanionCommandResult result = _ai.CompanionCommands.Issue(new CompanionCommand(commandId, companionId, _playerId, kind, targetId, targetPosition, tick, tick + 3600, "player order"), _ai.Brains, tick);
        LastCompanionCommandStatus = $"{kind}: {result.Message}";
        return result;
    }

    public CompanionCommandResult CommandCompanion(CompanionCommandKind kind, ulong tick)
    {
        FixedVector3 playerPosition = _runtime.State.GetActor(_playerId).Presentation.FeetPosition;
        StableId targetId = StableId.Empty;
        FixedVector3 targetPosition = playerPosition;
        if (kind == CompanionCommandKind.AttackTarget)
        {
            CombatActorState? target = _runtime.State.Actors
                .Where(value => !value.IsDead && (value.Role is CombatActorRole.Hostile or CombatActorRole.Creature))
                .OrderBy(value => CombatMath.Length(value.Presentation.FeetPosition - playerPosition))
                .ThenBy(static value => value.Id)
                .FirstOrDefault();
            if (target is not null) { targetId = target.Id; targetPosition = target.Presentation.FeetPosition; }
            else
            {
                var unavailable = new CompanionCommandResult(StableId.Empty, CompanionCommandStatus.Rejected, "no hostile target is available", tick);
                LastCompanionCommandStatus = unavailable.Message;
                return unavailable;
            }
        }
        return CommandCompanion(_companionId, kind, targetId, targetPosition, tick);
    }

    public IReadOnlyList<DebugLine> BuildDebugLines()
    {
        var lines = new List<DebugLine>();
        foreach (CombatFeedbackEntry entry in _runtime.State.Feedback.Where(entry => _runtime.State.Tick - Math.Min(_runtime.State.Tick, entry.Tick) <= 18).TakeLast(24))
        {
            if (entry.Origin == entry.Position) continue;
            Color color = entry.Kind switch
            {
                CombatFeedbackKind.ArmorStopped => Color.CornflowerBlue,
                CombatFeedbackKind.BodyPartHit or CombatFeedbackKind.InjuryApplied => Color.OrangeRed,
                CombatFeedbackKind.Explosion => Color.Yellow,
                _ => Color.LightGoldenrodYellow
            };
            lines.Add(new DebugLine(ToVector(entry.Origin), ToVector(entry.Position), color));
        }
        foreach (ProjectileState projectile in _runtime.State.Projectiles)
        {
            Vector3 center = ToVector(projectile.Position);
            lines.Add(new DebugLine(center - Vector3.UnitX * 0.12f, center + Vector3.UnitX * 0.12f, Color.Yellow));
            lines.Add(new DebugLine(center - Vector3.UnitY * 0.12f, center + Vector3.UnitY * 0.12f, Color.Yellow));
        }
        foreach ((StableId actorId, UtilityDecision decision) in _ai.Decisions)
        {
            if (!_runtime.State.TryGetActor(actorId, out CombatActorState? actor) || actor is null) continue;
            Vector3 origin = ToVector(actor.Presentation.FeetPosition) + Vector3.Up * 2f;
            Vector3 direction = decision.Kind switch
            {
                UtilityActionKind.Retreat or UtilityActionKind.Flee => Vector3.Left,
                UtilityActionKind.Surrender => Vector3.Down,
                UtilityActionKind.Attack => Vector3.Forward,
                _ => Vector3.Up
            };
            lines.Add(new DebugLine(origin, origin + direction, Color.Purple));
        }
        return lines;
    }


    private void EmitPlayerMovementStimulus(ulong tick, PlayerState player)
    {
        long horizontalSquared = checked(player.Velocity.XMillimeters * player.Velocity.XMillimeters + player.Velocity.ZMillimeters * player.Velocity.ZMillimeters);
        if (horizontalSquared < 250L * 250L || tick < _lastFootstepTick + 15) return;
        _lastFootstepTick = tick;
        int radius = player.Stance == PlayerStance.Standing ? 16_000 : 8_000;
        if (horizontalSquared > 3_000L * 3_000L) radius = 28_000;
        StableId stimulusId = StableIdFactory.CreateChild(_playerId, "player-footstep-stimulus", tick.ToString());
        _ai.EmitStimulus(new AiStimulus(stimulusId, AiStimulusKind.Footstep, _playerId, player.FeetPosition, 650, radius, FactionIds.Player, tick, tick + 45));
    }

    private void RegisterAuthoritativeWeapon(StableId itemId)
    {
        ItemInstanceState item = _items.World.GetItem(itemId);
        _runtime.RegisterWeapon(itemId, itemId.ToString(), _playerId, item.AmmunitionLoad?.LoadedCount ?? 0, _items.CountCompatibleAmmunition(_playerId, itemId), item.Condition.OverallPermille);
    }

    private void RefreshAuthoritativeAmmunition()
    {
        foreach (StableId itemId in _authoritativeWeaponIds)
            _runtime.State.GetWeapon(itemId).ReserveAmmunition = _items.CountCompatibleAmmunition(_playerId, itemId);
    }

    private void EquipPlayer(StableId itemId)
    {
        _runtime.Equip(_playerId, itemId);
        _items.EquipActiveWeapon(itemId);
    }

    private void QueuePlayerAttack(PlayerState player, bool emitStimulus)
    {
        FixedVector3 origin = EyePosition(player);
        FixedVector3 direction = ForwardVector(player, CombatMath.DirectionScale);
        if (_runtime.QueueAttack(_playerId, origin, direction) && emitStimulus)
        {
            StableId stimulusId = StableIdFactory.CreateChild(_playerId, "player-attack-stimulus", $"{_runtime.State.Tick}:{_runtime.State.GetActor(_playerId).EquippedWeaponId}");
            _ai.EmitStimulus(new AiStimulus(stimulusId, AiStimulusKind.Gunshot, _playerId, origin, 1000, 90_000, FactionIds.Player, _runtime.State.Tick, _runtime.State.Tick + 120));
        }
    }

    private static CombatActorRole ResolveRole(CharacterAppearanceDescriptor appearance)
    {
        if (appearance.Species == CharacterSpecies.IndustrialQuadruped) return CombatActorRole.Creature;
        if (appearance.ArchetypeId.Contains("hostile", StringComparison.Ordinal)) return CombatActorRole.Hostile;
        if (appearance.ArchetypeId.Contains("guard", StringComparison.Ordinal) || appearance.ArchetypeId.Contains("caretaker", StringComparison.Ordinal)) return CombatActorRole.Ally;
        return CombatActorRole.Neutral;
    }

    private static ArmorRuntimeState CreateFieldArmor(StableId actorId, string key, int condition)
    {
        return new ArmorRuntimeState(
            StableIdFactory.CreateChild(actorId, "armor", key),
            key,
            ArmorLayerKind.Primary,
            [AnatomicalRegion.Torso, AnatomicalRegion.LeftArm, AnatomicalRegion.RightArm],
            [new KeyValuePair<DamageType, int>(DamageType.Kinetic, 8), new KeyValuePair<DamageType, int>(DamageType.Cutting, 10), new KeyValuePair<DamageType, int>(DamageType.Blunt, 5), new KeyValuePair<DamageType, int>(DamageType.Thermal, 3)],
            [new KeyValuePair<DamageType, int>(DamageType.Kinetic, 260), new KeyValuePair<DamageType, int>(DamageType.Cutting, 340), new KeyValuePair<DamageType, int>(DamageType.Blunt, 180), new KeyValuePair<DamageType, int>(DamageType.Thermal, 120)],
            condition);
    }

    private static FixedVector3 EyePosition(PlayerState player)
    {
        int height = player.Stance == PlayerStance.Standing ? 1_650 : 1_150;
        return player.FeetPosition + new FixedVector3(0, height, 0);
    }

    private static FixedVector3 ForwardVector(PlayerState player, long length)
    {
        double yaw = player.BodyYawUnits / 10000.0 * Math.PI / 180.0;
        double pitch = player.ViewPitchUnits / 10000.0 * Math.PI / 180.0;
        double cp = Math.Cos(pitch);
        return new FixedVector3((long)Math.Round(Math.Sin(yaw) * cp * length), (long)Math.Round(Math.Sin(pitch) * length), (long)Math.Round(-Math.Cos(yaw) * cp * length));
    }

    private static Vector3 ToVector(FixedVector3 value) => new(value.XMillimeters / 1000f, value.YMillimeters / 1000f, value.ZMillimeters / 1000f);
}
