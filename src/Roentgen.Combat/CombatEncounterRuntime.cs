using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;

namespace Roentgen.Combat;

public sealed record CombatPresentationSnapshot(
    ulong Tick,
    StableId PlayerActorId,
    int Health,
    int MaximumHealth,
    string WeaponName,
    int Ammunition,
    int ReserveAmmunition,
    int ConditionPermille,
    int RecoilPermille,
    IReadOnlyList<CombatFeedbackEntry> RecentFeedback,
    StateChecksum Checksum);

public sealed class CombatEncounterRuntime
{
    private const string StateStoreKey = "combat.world-state";
    private readonly DamageResolver _damageResolver = new();
    private readonly AttackEvaluator _attackEvaluator;
    private readonly ICombatQueryWorld _queryWorld;
    private ICombatOcclusionProvider _occlusion;

    public CombatEncounterRuntime(CampaignSeed campaignSeed, CombatDefinitionCatalog catalog, ICombatQueryWorld? queryWorld = null, ICombatOcclusionProvider? occlusion = null, ICombatCapabilityProvider? capabilities = null)
    {
        CampaignSeed = campaignSeed;
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _queryWorld = queryWorld ?? new DynamicCombatQueryWorld();
        _occlusion = occlusion ?? NullCombatOcclusionProvider.Instance;
        _attackEvaluator = new AttackEvaluator(capabilities);
        State = new CombatWorldState();
    }

    public CampaignSeed CampaignSeed { get; }
    public CombatDefinitionCatalog Catalog { get; }
    public CombatWorldState State { get; private set; }
    public string PersistenceKey => StateStoreKey;

    public void SetOcclusionProvider(ICombatOcclusionProvider provider) => _occlusion = provider ?? throw new ArgumentNullException(nameof(provider));

    public CombatActorState RegisterActor(CharacterState presentation, IReadOnlyList<CharacterTargetPart> targetParts, CombatActorRole role, StableId factionId, int maximumHealth)
    {
        var actor = new CombatActorState(presentation, role, factionId, maximumHealth);
        IReadOnlyList<CharacterTargetPart> parts = targetParts.Count == 0 ? CreateFallbackTargetParts(presentation) : targetParts;
        foreach (CharacterTargetPart part in parts)
        {
            int integrity = part.Region switch
            {
                AnatomicalRegion.Head or AnatomicalRegion.SensoryCluster => 55,
                AnatomicalRegion.Torso => 130,
                AnatomicalRegion.LeftArm or AnatomicalRegion.RightArm => 75,
                AnatomicalRegion.LeftLeg or AnatomicalRegion.RightLeg => 90,
                AnatomicalRegion.FrontLeftLimb or AnatomicalRegion.FrontRightLimb or AnatomicalRegion.RearLeftLimb or AnatomicalRegion.RearRightLimb => 85,
                _ => 70
            };
            actor.AddBodyPart(new BodyPartRuntimeState(part, integrity));
        }
        State.AddActor(actor);
        return actor;
    }

    public CombatActorState RegisterActor(CharacterState presentation, IReadOnlyList<CharacterTargetPart> targetParts, CombatActorRole role, string factionId, int maximumHealth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factionId);
        return RegisterActor(presentation, targetParts, role, StableIdFactory.Create("faction.legacy", factionId), maximumHealth);
    }

    public WeaponRuntimeState RegisterWeapon(StableId id, string definitionId, StableId ownerId, int ammunition, int reserveAmmunition, int conditionPermille = 1000)
    {
        WeaponDefinition definition = Catalog.GetWeapon(definitionId);
        var weapon = new WeaponRuntimeState(id, definitionId, ownerId, definition.MagazineCapacity, ammunition)
        {
            ReserveAmmunition = Math.Max(0, reserveAmmunition),
            ConditionPermille = Math.Clamp(conditionPermille, 0, 1000)
        };
        State.AddWeapon(weapon);
        return weapon;
    }

    public void Equip(StableId actorId, StableId weaponId)
    {
        CombatActorState actor = State.GetActor(actorId);
        WeaponRuntimeState weapon = State.GetWeapon(weaponId);
        if (weapon.OwnerId != actorId) throw new InvalidOperationException($"Weapon '{weaponId}' is not owned by actor '{actorId}'.");
        actor.EquippedWeaponId = weaponId;
        actor.Presentation.EquippedObjectId = weaponId;
        WeaponDefinition definition = Catalog.GetWeapon(weapon.DefinitionId);
        actor.Presentation.EquipmentPoseProfileId = definition.Family switch
        {
            WeaponFamilyKind.Rifle => "rifle",
            WeaponFamilyKind.ImprovisedMelee => "melee-one-handed",
            WeaponFamilyKind.CreatureNatural or WeaponFamilyKind.Unarmed => "unarmed",
            _ => "rifle"
        };
        actor.Revision++;
    }

    public void AddArmor(StableId actorId, ArmorRuntimeState armor)
    {
        State.GetActor(actorId).AddArmor(armor);
        State.Revision++;
    }

    public bool QueueAttack(StableId actorId, FixedVector3 origin, FixedVector3 direction, StableId intendedTargetId = default, StableId intendedBodyPartId = default)
    {
        if (!State.TryGetActor(actorId, out CombatActorState? actor) || actor is null || actor.IsDead) return Reject(actorId, "actor unavailable");
        if (actor.EquippedWeaponId.IsEmpty || !State.TryGetWeapon(actor.EquippedWeaponId, out WeaponRuntimeState? weapon) || weapon is null) return Reject(actorId, "no equipped weapon");
        if (actor.Statuses.Any(status => (status.Kind is CombatStatusKind.Stunned or CombatStatusKind.Shocked) && status.ExpiresTick > State.Tick)) return Reject(actorId, "actor incapacitated");
        WeaponDefinition definition = Catalog.GetWeapon(weapon.DefinitionId);
        CombatActionKind actionKind = definition.AttackKind switch
        {
            CombatAttackKind.Melee => CombatActionKind.Melee,
            CombatAttackKind.Explosive or CombatAttackKind.Projectile => CombatActionKind.Throw,
            _ => CombatActionKind.Fire
        };
        if (State.TryGetPendingAction(actorId, out _) || State.Tick < weapon.NextReadyTick) return Reject(actorId, "weapon not ready");
        if (weapon.IsJammed) return Reject(actorId, "weapon jammed");
        if (definition.MagazineCapacity > 0 && weapon.Ammunition <= 0) return Reject(actorId, "empty magazine");
        ulong sequence = weapon.AttackSequence++;
        ulong impact = State.Tick + (ulong)definition.WindupTicks;
        ulong end = impact + (ulong)definition.RecoveryTicks;
        var action = new PendingCombatAction(actorId, weapon.Id, actionKind, origin, CombatMath.Normalize(direction), intendedTargetId, intendedBodyPartId, State.Tick, impact, end, sequence);
        State.SetPendingAction(action);
        actor.Action = actionKind;
        actor.ActionStartedTick = State.Tick;
        actor.ActionImpactTick = impact;
        actor.ActionEndsTick = end;
        actor.CurrentTargetId = intendedTargetId;
        actor.Presentation.Action = actionKind switch
        {
            CombatActionKind.Melee => CharacterActionKind.Melee,
            CombatActionKind.Throw => CharacterActionKind.Throw,
            _ => CharacterActionKind.Aim
        };
        actor.Presentation.ActionProgressPermille = 0;
        actor.Revision++;
        return true;
    }

    public bool QueueReload(StableId actorId)
    {
        if (!State.TryGetActor(actorId, out CombatActorState? actor) || actor is null || actor.IsDead) return Reject(actorId, "actor unavailable");
        if (actor.EquippedWeaponId.IsEmpty || !State.TryGetWeapon(actor.EquippedWeaponId, out WeaponRuntimeState? weapon) || weapon is null) return Reject(actorId, "no equipped weapon");
        WeaponDefinition definition = Catalog.GetWeapon(weapon.DefinitionId);
        if (definition.MagazineCapacity <= 0 || weapon.Ammunition >= weapon.MagazineCapacity || weapon.ReserveAmmunition <= 0) return Reject(actorId, "reload unnecessary");
        if (State.TryGetPendingAction(actorId, out _)) return Reject(actorId, "actor busy");
        ulong end = State.Tick + (ulong)Math.Max(1, definition.ReloadTicks);
        State.SetPendingAction(new PendingCombatAction(actorId, weapon.Id, CombatActionKind.Reload, actor.Presentation.FeetPosition, FixedVector3.Zero, StableId.Empty, StableId.Empty, State.Tick, end, end, weapon.AttackSequence));
        actor.Action = CombatActionKind.Reload;
        actor.ActionStartedTick = State.Tick;
        actor.ActionImpactTick = end;
        actor.ActionEndsTick = end;
        actor.Presentation.Action = CharacterActionKind.Reload;
        actor.Presentation.ActionProgressPermille = 0;
        State.AddFeedback(State.Tick, CombatFeedbackKind.ReloadStarted, actorId, StableId.Empty, StableId.Empty, actor.Presentation.FeetPosition, actor.Presentation.FeetPosition, 0, $"{definition.DisplayName} reload started");
        return true;
    }

    public void SetBlocking(StableId actorId, bool blocking)
    {
        CombatActorState actor = State.GetActor(actorId);
        if (actor.IsDead) return;
        actor.IsBlocking = blocking;
        if (blocking && !State.TryGetPendingAction(actorId, out _))
        {
            actor.Action = CombatActionKind.Block;
            actor.Presentation.Action = CharacterActionKind.Block;
        }
        else if (!blocking && actor.Action == CombatActionKind.Block)
        {
            actor.Action = CombatActionKind.None;
            actor.Presentation.Action = CharacterActionKind.Idle;
        }
        actor.Revision++;
    }

    public void Update(ulong tick)
    {
        if (tick < State.Tick) throw new ArgumentOutOfRangeException(nameof(tick), "Combat time cannot move backwards.");
        State.Tick = tick;
        UpdateActors();
        ProcessActions();
        UpdateProjectiles();
        State.Revision++;
    }

    public void SynchronizeActorTransform(StableId actorId, FixedVector3 feetPosition, FixedVector3 velocity, int facingYawUnits)
    {
        CombatActorState actor = State.GetActor(actorId);
        actor.Presentation.FeetPosition = feetPosition;
        actor.Presentation.Velocity = velocity;
        actor.Presentation.FacingYawUnits = facingYawUnits;
        actor.Presentation.Revision++;
        actor.Revision++;
    }

    public CombatPresentationSnapshot CreatePresentation(StableId playerActorId)
    {
        CombatActorState player = State.GetActor(playerActorId);
        WeaponRuntimeState? weapon = player.EquippedWeaponId.IsEmpty ? null : State.GetWeapon(player.EquippedWeaponId);
        WeaponDefinition? definition = weapon is null ? null : Catalog.GetWeapon(weapon.DefinitionId);
        return new CombatPresentationSnapshot(
            State.Tick,
            playerActorId,
            player.Health,
            player.MaximumHealth,
            definition?.DisplayName ?? "Unarmed",
            weapon?.Ammunition ?? 0,
            weapon?.ReserveAmmunition ?? 0,
            weapon?.ConditionPermille ?? 1000,
            weapon?.RecoilPermille ?? 0,
            State.Feedback.TakeLast(6).ToArray(),
            State.ComputeChecksum());
    }

    public void PersistTo(Roentgen.Simulation.SimulationStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var writer = new CanonicalWriter();
        State.WriteCanonical(writer);
        store.Set(Roentgen.Simulation.SimulationStateValue.FromPayload(StateStoreKey, CombatWorldState.SchemaVersion, writer.WrittenSpan));
    }

    public bool RestoreFrom(Roentgen.Simulation.SimulationStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!store.TryGet(StateStoreKey, out Roentgen.Simulation.SimulationStateValue? value) || value is null) return false;
        State = CombatWorldState.ReadCanonical(value.Payload);
        return true;
    }

    public void Restore(ReadOnlyMemory<byte> payload) => State = CombatWorldState.ReadCanonical(payload);

    private void UpdateActors()
    {
        foreach (CombatActorState actor in State.Actors.OrderBy(static value => value.Id))
        {
            foreach (CombatStatusState status in actor.Statuses.Where(status => status.ExpiresTick != ulong.MaxValue && status.ExpiresTick <= State.Tick).ToArray()) actor.RemoveStatus(status.Kind);
            ApplyOngoingStatuses(actor);
            int torsoTrauma = actor.Presentation.Injury.TorsoTraumaPermille;
            if (!actor.IsDead && !State.TryGetPendingAction(actor.Id, out _)) actor.StaminaPermille = Math.Min(1000, actor.StaminaPermille + Math.Max(1, 5 - torsoTrauma / 250));
            if (!actor.EquippedWeaponId.IsEmpty && State.TryGetWeapon(actor.EquippedWeaponId, out WeaponRuntimeState? weapon) && weapon is not null)
            {
                weapon.RecoilPermille = Math.Max(0, weapon.RecoilPermille - 16);
                weapon.HeatPermille = Math.Max(0, weapon.HeatPermille - 8);
            }
            if (actor.ActionEndsTick > actor.ActionStartedTick)
            {
                ulong elapsed = State.Tick <= actor.ActionStartedTick ? 0 : State.Tick - actor.ActionStartedTick;
                ulong duration = actor.ActionEndsTick - actor.ActionStartedTick;
                actor.Presentation.ActionProgressPermille = (int)Math.Clamp(elapsed * 1000UL / Math.Max(1UL, duration), 0UL, 1000UL);
            }
            if (actor.IsDead && actor.Action == CombatActionKind.Dying && State.Tick >= actor.ActionEndsTick)
            {
                actor.Action = CombatActionKind.Dead;
                actor.Presentation.Action = CharacterActionKind.Dead;
                actor.Presentation.ActionProgressPermille = 1000;
            }
            else if (!actor.IsDead && actor.Action != CombatActionKind.Block && !State.TryGetPendingAction(actor.Id, out _) && State.Tick >= actor.ActionEndsTick)
            {
                actor.Action = CombatActionKind.None;
                if (actor.Presentation.Velocity == FixedVector3.Zero) actor.Presentation.Action = CharacterActionKind.Idle;
            }
        }
    }


    private void ApplyOngoingStatuses(CombatActorState actor)
    {
        if (actor.IsDead || State.Tick == 0 || State.Tick % 60 != 0) return;
        int damage = 0;
        foreach (CombatStatusState status in actor.Statuses)
        {
            damage += status.Kind switch
            {
                CombatStatusKind.Bleeding => Math.Max(1, status.SeverityPermille / 250),
                CombatStatusKind.Burning => Math.Max(1, status.SeverityPermille / 180),
                CombatStatusKind.Poisoned => Math.Max(1, status.SeverityPermille / 320),
                CombatStatusKind.Irradiated => Math.Max(1, status.SeverityPermille / 450),
                _ => 0
            };
            if (status.Kind is CombatStatusKind.Shocked or CombatStatusKind.Stunned) actor.StaminaPermille = Math.Max(0, actor.StaminaPermille - Math.Max(20, status.SeverityPermille / 5));
        }
        if (damage <= 0) return;
        actor.Health = Math.Max(0, actor.Health - damage);
        actor.MoralePermille = Math.Max(0, actor.MoralePermille - damage * 3);
        actor.Revision++;
        State.AddFeedback(State.Tick, CombatFeedbackKind.StatusDamage, actor.Id, actor.Id, StableId.Empty, actor.Presentation.FeetPosition, actor.Presentation.FeetPosition, damage, $"ongoing effects caused {damage} damage");
        if (actor.Health == 0)
        {
            MarkDead(actor, State.Tick);
            State.AddFeedback(State.Tick, CombatFeedbackKind.ActorDied, actor.Id, actor.Id, StableId.Empty, actor.Presentation.FeetPosition, actor.Presentation.FeetPosition, damage, $"{actor.Presentation.Appearance.ArchetypeId} died from ongoing effects");
            State.RemovePendingAction(actor.Id);
        }
    }

    private static void MarkDead(CombatActorState actor, ulong tick)
    {
        if (actor.IsDead) return;
        actor.Health = 0;
        actor.IsDead = true;
        actor.Action = CombatActionKind.Dying;
        actor.ActionStartedTick = tick;
        actor.ActionImpactTick = tick;
        actor.ActionEndsTick = tick + 60;
        actor.Presentation.Action = CharacterActionKind.Dying;
        actor.Presentation.ActionProgressPermille = 0;
        actor.Presentation.Velocity = FixedVector3.Zero;
        actor.Revision++;
    }

    private void ProcessActions()
    {
        foreach (PendingCombatAction pending in State.PendingActions.OrderBy(static value => value.ActorId).ToArray())
        {
            PendingCombatAction action = pending;
            if (!State.TryGetActor(action.ActorId, out CombatActorState? actor) || actor is null || actor.IsDead)
            {
                State.RemovePendingAction(action.ActorId);
                continue;
            }
            if (!action.ImpactResolved && State.Tick >= action.ImpactTick)
            {
                if (action.Kind == CombatActionKind.Reload) CompleteReload(actor, action);
                else ResolveAttack(actor, action);
                action = action with { ImpactResolved = true };
                State.SetPendingAction(action);
            }
            if (State.Tick >= action.EndTick)
            {
                State.RemovePendingAction(action.ActorId);
                if (!actor.IsDead)
                {
                    actor.Action = actor.IsBlocking ? CombatActionKind.Block : CombatActionKind.None;
                    actor.Presentation.Action = actor.IsBlocking ? CharacterActionKind.Block : CharacterActionKind.Idle;
                    actor.Presentation.ActionProgressPermille = 0;
                }
            }
        }
    }

    private void CompleteReload(CombatActorState actor, PendingCombatAction action)
    {
        WeaponRuntimeState weapon = State.GetWeapon(action.WeaponId);
        WeaponDefinition definition = Catalog.GetWeapon(weapon.DefinitionId);
        int required = weapon.MagazineCapacity - weapon.Ammunition;
        int loaded = Math.Min(required, weapon.ReserveAmmunition);
        weapon.Ammunition += loaded;
        weapon.ReserveAmmunition -= loaded;
        weapon.IsJammed = false;
        weapon.ConditionPermille = Math.Max(0, weapon.ConditionPermille - (weapon.IsJammed ? 2 : 0));
        weapon.Revision++;
        State.AddFeedback(State.Tick, CombatFeedbackKind.ReloadCompleted, actor.Id, StableId.Empty, StableId.Empty, actor.Presentation.FeetPosition, actor.Presentation.FeetPosition, loaded, $"{definition.DisplayName} loaded {loaded}");
    }

    private void ResolveAttack(CombatActorState actor, PendingCombatAction action)
    {
        WeaponRuntimeState weapon = State.GetWeapon(action.WeaponId);
        WeaponDefinition definition = Catalog.GetWeapon(weapon.DefinitionId);
        if (definition.MagazineCapacity > 0)
        {
            if (weapon.Ammunition <= 0) { Reject(actor.Id, "empty magazine"); return; }
            weapon.Ammunition--;
        }
        int jamChance = weapon.ConditionPermille >= 500 ? 0 : Math.Clamp((500 - weapon.ConditionPermille) / 4, 0, 180);
        int jamRoll = CombatMath.DeterministicPermille(CampaignSeed, actor.Id, weapon.Id, State.Tick, action.Sequence, "jam");
        if (jamRoll < jamChance)
        {
            weapon.IsJammed = true;
            weapon.NextReadyTick = State.Tick + 30;
            weapon.Revision++;
            State.AddFeedback(State.Tick, CombatFeedbackKind.WeaponJammed, actor.Id, StableId.Empty, StableId.Empty, action.Origin, action.Origin, 0, $"{definition.DisplayName} jammed");
            return;
        }
        weapon.NextReadyTick = State.Tick + (ulong)definition.TicksBetweenAttacks;
        weapon.RecoilPermille = Math.Clamp(weapon.RecoilPermille + definition.RecoilPerAttackPermille, 0, 1000);
        weapon.HeatPermille = Math.Clamp(weapon.HeatPermille + definition.RecoilPerAttackPermille / 2, 0, 1000);
        weapon.ConditionPermille = Math.Max(0, weapon.ConditionPermille - definition.ConditionLossPerAttack);
        weapon.Revision++;
        actor.Presentation.Action = action.Kind switch
        {
            CombatActionKind.Melee => CharacterActionKind.Melee,
            CombatActionKind.Throw => CharacterActionKind.Throw,
            _ => CharacterActionKind.Fire
        };
        State.AddFeedback(State.Tick, CombatFeedbackKind.ShotFired, actor.Id, StableId.Empty, StableId.Empty, action.Origin, action.Origin, 0, definition.DisplayName);
        switch (definition.AttackKind)
        {
            case CombatAttackKind.Hitscan:
                ResolveHitscan(actor, weapon, definition, action);
                break;
            case CombatAttackKind.Melee:
                ResolveMelee(actor, weapon, definition, action);
                break;
            case CombatAttackKind.Projectile:
            case CombatAttackKind.Explosive:
                SpawnProjectile(actor, weapon, definition, action);
                break;
        }
    }

    private void ResolveHitscan(CombatActorState actor, WeaponRuntimeState weapon, WeaponDefinition definition, PendingCombatAction action)
    {
        CombatActorState? intended = null;
        if (!action.IntendedTargetId.IsEmpty) State.TryGetActor(action.IntendedTargetId, out intended);
        int distance = intended is null
            ? definition.MaximumRangeMillimeters / 2
            : (int)Math.Round(CombatMath.Distance(action.Origin, intended.Presentation.FeetPosition));
        AttackEvaluation evaluation = _attackEvaluator.Evaluate(actor, weapon, definition, distance, intended is not null && intended.Presentation.Velocity != FixedVector3.Zero, false);
        for (int pellet = 0; pellet < definition.PelletCount; pellet++)
        {
            FixedVector3 direction = CombatMath.ApplySpread(action.Direction, evaluation.SpreadPermille, CampaignSeed, actor.Id, weapon.Id, State.Tick, action.Sequence + (ulong)pellet);
            CombatHit hit = _queryWorld.RaycastActors(new CombatRay(action.Origin, direction, definition.MaximumRangeMillimeters, actor.Id), State.Actors, _occlusion);
            ApplyHit(actor, weapon, definition, action.Origin, hit, 1000 / definition.PelletCount);
        }
    }

    private void ResolveMelee(CombatActorState actor, WeaponRuntimeState weapon, WeaponDefinition definition, PendingCombatAction action)
    {
        int reach = Math.Max(500, definition.MeleeReachMillimeters);
        FixedVector3 end = CombatMath.PointAlong(action.Origin, action.Direction, reach);
        CombatHit hit = _queryWorld.SweepMelee(new MeleeSweep(action.Origin, end, Math.Max(80, definition.MeleeSweepRadiusMillimeters), actor.Id), State.Actors, _occlusion);
        if (hit.Outcome != CombatHitOutcome.Actor)
        {
            State.AddFeedback(State.Tick, CombatFeedbackKind.ShotMissed, actor.Id, StableId.Empty, StableId.Empty, action.Origin, hit.Position, 0, "melee missed");
            return;
        }
        ApplyHit(actor, weapon, definition, action.Origin, hit, Math.Clamp(actor.StaminaPermille + 250, 500, 1250));
        actor.StaminaPermille = Math.Max(0, actor.StaminaPermille - 160);
    }

    private void SpawnProjectile(CombatActorState actor, WeaponRuntimeState weapon, WeaponDefinition definition, PendingCombatAction action)
    {
        FixedVector3 direction = CombatMath.ApplySpread(action.Direction, definition.BaseSpreadPermille + weapon.RecoilPermille / 2, CampaignSeed, actor.Id, weapon.Id, State.Tick, action.Sequence);
        FixedVector3 velocity = CombatMath.ScaleDirection(direction, definition.ProjectileSpeedMillimetersPerSecond);
        StableId id = StableIdFactory.CreateChild(weapon.Id, "projectile", checked((long)action.Sequence));
        var projectile = new ProjectileState(id, actor.Id, weapon.Id, definition.Id, action.Origin, velocity)
        {
            RadiusMillimeters = definition.ProjectileRadiusMillimeters,
            RemainingTicks = Math.Max(1, definition.MaximumRangeMillimeters * 60 / Math.Max(1, definition.ProjectileSpeedMillimetersPerSecond)),
            FuseTicks = definition.FuseTicks,
            Explosive = definition.ExplosionRadiusMillimeters > 0
        };
        State.AddProjectile(projectile);
    }

    private void UpdateProjectiles()
    {
        foreach (ProjectileState projectile in State.Projectiles.OrderBy(static value => value.Id).ToArray())
        {
            if (!projectile.Active) { State.RemoveProjectile(projectile.Id); continue; }
            WeaponDefinition definition = Catalog.GetWeapon(projectile.DefinitionId);
            FixedVector3 step = new(projectile.Velocity.XMillimeters / 60, projectile.Velocity.YMillimeters / 60, projectile.Velocity.ZMillimeters / 60);
            FixedVector3 direction = CombatMath.Normalize(step);
            int distance = (int)Math.Max(1, Math.Round(CombatMath.Length(step)));
            CombatHit hit = _queryWorld.RaycastActors(new CombatRay(projectile.Position, direction, distance + projectile.RadiusMillimeters, projectile.OwnerId), State.Actors, _occlusion);
            projectile.Position += step;
            projectile.RemainingTicks--;
            if (projectile.FuseTicks > 0) projectile.FuseTicks--;
            projectile.Revision++;
            if (hit.Outcome == CombatHitOutcome.Actor)
            {
                WeaponRuntimeState weapon = State.GetWeapon(projectile.WeaponId);
                CombatActorState owner = State.GetActor(projectile.OwnerId);
                ApplyHit(owner, weapon, definition, projectile.Position, hit, 1000);
                projectile.Active = false;
            }
            if (projectile.Explosive && (projectile.FuseTicks == 0 || hit.Outcome is CombatHitOutcome.Actor or CombatHitOutcome.Environment))
            {
                Explode(projectile, definition);
                projectile.Active = false;
            }
            if (projectile.RemainingTicks <= 0) projectile.Active = false;
            if (!projectile.Active) State.RemoveProjectile(projectile.Id);
        }
    }

    private void Explode(ProjectileState projectile, WeaponDefinition definition)
    {
        IReadOnlyList<ExplosionExposure> exposures = _queryWorld.QueryExplosion(projectile.Position, definition.ExplosionRadiusMillimeters, State.Actors, StableId.Empty, _occlusion);
        foreach (ExplosionExposure exposure in exposures)
        {
            CombatActorState actor = State.GetActor(exposure.ActorId);
            BodyPartRuntimeState part = actor.GetRequiredBodyPart(exposure.BodyPartId);
            DamageResolution result = _damageResolver.Apply(actor, part, definition.Damage, exposure.ExposurePermille, actor.IsBlocking, State.Tick);
            AddDamageFeedback(projectile.OwnerId, actor, part, projectile.Position, result);
        }
        State.AddFeedback(State.Tick, CombatFeedbackKind.Explosion, projectile.OwnerId, StableId.Empty, StableId.Empty, projectile.Position, projectile.Position, exposures.Count, $"explosion affected {exposures.Count} actors");
    }

    private void ApplyHit(CombatActorState attacker, WeaponRuntimeState weapon, WeaponDefinition definition, FixedVector3 origin, CombatHit hit, int damageScalePermille)
    {
        if (hit.Outcome != CombatHitOutcome.Actor || !State.TryGetActor(hit.ActorId, out CombatActorState? target) || target is null)
        {
            State.AddFeedback(State.Tick, CombatFeedbackKind.ShotMissed, attacker.Id, StableId.Empty, StableId.Empty, origin, hit.Position, 0, hit.Outcome == CombatHitOutcome.Environment ? "impact on environment" : "shot missed");
            return;
        }
        BodyPartRuntimeState part = target.GetRequiredBodyPart(hit.BodyPartId);
        DamageResolution result = _damageResolver.Apply(target, part, definition.Damage, damageScalePermille, target.IsBlocking, State.Tick);
        AddDamageFeedback(attacker.Id, target, part, origin, result, hit.Position);
        if (result.ActorDied || result.FinalDamage >= 25) State.RemovePendingAction(target.Id);
        _ = weapon;
    }

    private void AddDamageFeedback(StableId attackerId, CombatActorState target, BodyPartRuntimeState part, FixedVector3 origin, DamageResolution result, FixedVector3? hitPosition = null)
    {
        FixedVector3 position = hitPosition ?? CombatSpatialModel.PartCenter(target, part.Definition.Region);
        if (result.FinalDamage == 0)
        {
            State.AddFeedback(State.Tick, CombatFeedbackKind.ArmorStopped, attackerId, target.Id, part.Definition.Id, origin, position, result.ArmorStopped, $"armor stopped damage to {part.Definition.Name}");
        }
        else
        {
            State.AddFeedback(State.Tick, CombatFeedbackKind.BodyPartHit, attackerId, target.Id, part.Definition.Id, origin, position, result.FinalDamage, $"{part.Definition.Name} hit for {result.FinalDamage}");
            if (result.NewState != result.PreviousState) State.AddFeedback(State.Tick, CombatFeedbackKind.InjuryApplied, attackerId, target.Id, part.Definition.Id, origin, position, result.NewState == BodyPartFunctionalState.Destroyed ? 1000 : part.TraumaPermille, $"{part.Definition.Name} {result.NewState}");
        }
        if (result.ActorDied) State.AddFeedback(State.Tick, CombatFeedbackKind.ActorDied, attackerId, target.Id, part.Definition.Id, origin, position, result.HealthDamage, $"{target.Presentation.Appearance.ArchetypeId} died");
    }

    private bool Reject(StableId actorId, string reason)
    {
        FixedVector3 position = State.TryGetActor(actorId, out CombatActorState? actor) && actor is not null ? actor.Presentation.FeetPosition : FixedVector3.Zero;
        State.AddFeedback(State.Tick, CombatFeedbackKind.ActionRejected, actorId, StableId.Empty, StableId.Empty, position, position, 0, reason);
        return false;
    }

    private static IReadOnlyList<CharacterTargetPart> CreateFallbackTargetParts(CharacterState presentation)
    {
        StableId id = presentation.Id;
        return
        [
            new CharacterTargetPart(StableIdFactory.CreateChild(id, "target-part", "head"), "Head", AnatomicalRegion.Head, "head", 170, 260, 1200),
            new CharacterTargetPart(StableIdFactory.CreateChild(id, "target-part", "torso"), "Torso", AnatomicalRegion.Torso, "chest", 300, 650, 1000),
            new CharacterTargetPart(StableIdFactory.CreateChild(id, "target-part", "left-arm"), "Left Arm", AnatomicalRegion.LeftArm, "upper_arm_l", 120, 620, 650),
            new CharacterTargetPart(StableIdFactory.CreateChild(id, "target-part", "right-arm"), "Right Arm", AnatomicalRegion.RightArm, "upper_arm_r", 120, 620, 650),
            new CharacterTargetPart(StableIdFactory.CreateChild(id, "target-part", "left-leg"), "Left Leg", AnatomicalRegion.LeftLeg, "thigh_l", 145, 760, 750),
            new CharacterTargetPart(StableIdFactory.CreateChild(id, "target-part", "right-leg"), "Right Leg", AnatomicalRegion.RightLeg, "thigh_r", 145, 760, 750)
        ];
    }
}
