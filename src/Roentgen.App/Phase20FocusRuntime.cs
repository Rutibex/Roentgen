using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Focus;
using Roentgen.Generation;
using Roentgen.UI;

namespace Roentgen.App;

internal sealed class Phase20FocusRuntime
{
    private readonly Phase10CombatController combat;
    private readonly StableId playerId;
    private readonly ulong rootSeed;
    private readonly FocusModeCoordinator coordinator = new();
    private readonly FocusResourceState resource = new();
    private readonly FocusTargetGraphBuilder graphBuilder = new();
    private readonly FocusForecastService forecasts;
    private readonly DraftFocusPlan draft = new();
    private readonly FocusTacticalCamera camera = new();
    private IReadOnlyList<CombatActorState> targets = [];
    private FocusTargetGraph? graph;
    private FocusActionForecast? forecast;
    private FocusExecutionController? execution;
    private int targetIndex;
    private int partIndex;
    private ulong actionSequence;
    private string status = "Focus ready.";

    public Phase20FocusRuntime(Phase10CombatController combat, Phase12ProgressionRuntime progression, StableId playerId, CampaignSeed campaignSeed, Phase20AccessibilitySettings accessibility)
    {
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        ArgumentNullException.ThrowIfNull(progression);
        this.playerId = playerId;
        rootSeed = GenerationDomain.Derive(campaignSeed, "phase20.focus").Value;
        forecasts = new FocusForecastService(progression.FocusCapabilities);
        resource.ApplyDerivedStatistics(progression.Capabilities.Get(playerId).DerivedStatistics);
        Accessibility = accessibility.ToFocusSettings();
    }

    public FocusTemporalMode Mode => coordinator.Mode;
    public bool Active => Mode != FocusTemporalMode.Inactive;
    public bool IsPlanning => Mode == FocusTemporalMode.Planning;
    public bool IsExecuting => Mode == FocusTemporalMode.Executing;
    public string Status => status;
    public FocusAccessibilitySettings Accessibility { get; private set; }

    public void ApplyAccessibility(Phase20AccessibilitySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Accessibility = settings.ToFocusSettings();
    }

    public bool Enter(ulong simulationTick)
    {
        if (Active) return false;
        targets = combat.Runtime.State.Actors
            .Where(actor => actor.Id != playerId && !actor.IsDead && actor.Role is CombatActorRole.Hostile or CombatActorRole.Creature)
            .OrderBy(actor => DistanceSquared(combat.Runtime.State.GetActor(playerId).Presentation.FeetPosition, actor.Presentation.FeetPosition))
            .ThenBy(static actor => actor.Id)
            .ToArray();
        if (targets.Count == 0)
        {
            status = "No valid hostile target is available.";
            return false;
        }
        coordinator.EnterPlanning(simulationTick);
        targetIndex = 0;
        partIndex = 0;
        draft.Clear();
        execution = null;
        RebuildSelection();
        status = "Simulation paused. Select a consequential target part.";
        return true;
    }

    public void Cancel()
    {
        if (!Active) return;
        if (IsExecuting)
        {
            coordinator.Interrupt(FocusInterruptionReason.UserCancelled);
            status = "Focus sequence interrupted by the player.";
        }
        if (resource.ReservedMilliunits > 0) resource.Release(resource.ReservedMilliunits);
        draft.Clear();
        execution = null;
        coordinator.Exit();
    }

    public void SelectNextTarget(int direction)
    {
        if (!IsPlanning || targets.Count == 0) return;
        targetIndex = Wrap(targetIndex + Math.Sign(direction), targets.Count);
        partIndex = 0;
        RebuildSelection();
    }

    public void SelectNextPart(int direction)
    {
        if (!IsPlanning || graph is null) return;
        IReadOnlyList<FocusTargetNode> selectable = SelectableNodes();
        if (selectable.Count == 0) return;
        partIndex = Wrap(partIndex + Math.Sign(direction), selectable.Count);
        RefreshForecast();
    }

    public bool QueueSelectedAction()
    {
        if (!IsPlanning || graph is null || forecast is null) return false;
        if (forecast.FocusCostMilliunits > resource.AvailableMilliunits - draft.TotalCostMilliunits)
        {
            status = "The selected action exceeds available Focus.";
            return false;
        }
        CombatActorState player = combat.Runtime.State.GetActor(playerId);
        StableId actionId = StableIdFactory.CreateChild(playerId, "focus-action", $"{coordinator.FrozenSimulationTick}:{actionSequence++}");
        FocusTargetNode node = SelectedNode();
        WeaponRuntimeState weapon = combat.Runtime.State.GetWeapon(player.EquippedWeaponId);
        WeaponDefinition definition = combat.Runtime.Catalog.GetWeapon(weapon.DefinitionId);
        FocusActionKind kind = definition.AttackKind switch
        {
            CombatAttackKind.Melee => FocusActionKind.Melee,
            CombatAttackKind.Explosive or CombatAttackKind.Projectile => FocusActionKind.Throw,
            _ => FocusActionKind.Fire
        };
        draft.Add(new DraftFocusAction(actionId, kind, playerId, weapon.Id, node.ActorId, node.NodeId, FocusTrackingPolicy.TrackSamePart, FocusTargetLossPolicy.FallbackToParent, forecast.FocusCostMilliunits));
        status = $"Queued {definition.DisplayName} against {node.DisplayName}.";
        return true;
    }

    public void RemoveLastAction()
    {
        if (!IsPlanning || draft.Actions.Count == 0) return;
        _ = draft.Remove(draft.Actions[^1].Id);
        status = "Removed the last queued action.";
    }

    public bool Commit(ulong tick)
    {
        if (!IsPlanning) return false;
        FocusPlanValidation validation = new FocusPlanValidator().Validate(draft, resource);
        if (!validation.IsValid)
        {
            status = string.Join(" ", validation.Errors);
            return false;
        }
        IReadOnlyList<CommittedFocusAction> actions = new FocusPlanCommitter().Commit(draft, resource, rootSeed, tick);
        execution = new FocusExecutionController(combat.Runtime, resource, actions);
        coordinator.BeginExecution();
        status = "Focus sequence executing.";
        return true;
    }

    public void UpdateExecution()
    {
        if (!IsExecuting || execution is null) return;
        FocusExecutionResult result = execution.Step();
        if (result.Interrupted)
        {
            coordinator.Interrupt(result.Reason);
            status = $"Focus interrupted: {result.Reason}.";
            return;
        }
        if (result.Complete)
        {
            coordinator.Complete();
            status = "Focus sequence completed.";
        }
    }

    public void FinishCompleted()
    {
        if (Mode is FocusTemporalMode.Completed or FocusTemporalMode.Interrupted)
        {
            draft.Clear();
            execution = null;
            coordinator.Exit();
        }
    }

    public void Recover(bool allowed) => resource.Recover(allowed && !Active);

    public bool ShouldAdvanceSimulation(int interfaceFrame) => coordinator.ShouldAdvanceSimulation(interfaceFrame, 4);

    public Phase20FocusModel BuildModel()
    {
        CombatActorState? target = targets.Count == 0 ? null : targets[Math.Clamp(targetIndex, 0, targets.Count - 1)];
        FocusTargetNode? selected = graph is null || SelectableNodes().Count == 0 ? null : SelectedNode();
        var snapshot = new FocusPresentationSnapshot(
            coordinator.Mode,
            coordinator.FrozenSimulationTick,
            camera.State,
            resource,
            graph,
            forecast,
            draft.Actions.ToArray(),
            Accessibility);
        Phase20MenuEntry[] parts = graph is null ? [] : SelectableNodes().Select(node => new Phase20MenuEntry(
            node.NodeId.ToString(),
            node.DisplayName,
            node.FunctionalState.ToString(),
            node.Consequences.FirstOrDefault(),
            node.IsSelectable,
            false,
            node.Knowledge.ToString())).ToArray();
        string[] forecastLines = forecast is null
            ? ["NO FORECAST AVAILABLE"]
            :
            [
                $"HIT CHANCE {forecast.HitChancePermille / 10.0:F1}%",
                $"EXPECTED DAMAGE {forecast.ExpectedDamageMilliunits / 1000.0:F1}",
                $"ARMOR ABSORPTION {forecast.ArmorAbsorptionPermille / 10.0:F1}%",
                $"INJURY CHANCE {forecast.InjuryChancePermille / 10.0:F1}%",
                $"INCAPACITATION {forecast.IncapacitationChancePermille / 10.0:F1}%",
                $"KILL CHANCE {forecast.KillChancePermille / 10.0:F1}%",
                $"FOCUS COST {forecast.FocusCostMilliunits / 1000.0:F1}",
                $"CONFIDENCE {forecast.Confidence}",
                .. forecast.Factors.OrderByDescending(static value => Math.Abs(value.ModifierPermille)).Take(5).Select(value => $"{value.Explanation}: {value.ModifierPermille:+#;-#;0}")
            ];
        string[] queueLines = draft.Actions.Select((action, index) => $"{index + 1}. {action.Kind} — {TargetName(action.TargetId)} / {PartName(action.TargetNodeId)} — {action.CostMilliunits / 1000.0:F1}").ToArray();
        return new Phase20FocusModel(
            snapshot,
            target?.Presentation.Appearance.ArchetypeId ?? "No target",
            selected?.DisplayName ?? "No component",
            parts,
            parts.Length == 0 ? -1 : Math.Clamp(partIndex, 0, parts.Length - 1),
            forecastLines,
            queueLines,
            "Q/E TARGET  •  UP/DOWN PART  •  ENTER QUEUE  •  BACKSPACE REMOVE  •  R COMMIT  •  ESC CANCEL",
            status);
    }

    private void RebuildSelection()
    {
        CombatActorState target = targets[Math.Clamp(targetIndex, 0, targets.Count - 1)];
        graph = graphBuilder.Build(target, DiagnosticKnowledgeLevel.FullyDiagnosed);
        IReadOnlyList<FocusTargetNode> selectable = SelectableNodes();
        partIndex = selectable.Count == 0 ? -1 : Math.Clamp(partIndex, 0, selectable.Count - 1);
        StableId nodeId = partIndex < 0 ? StableId.Empty : selectable[partIndex].NodeId;
        camera.Select(target.Id, nodeId, target.Presentation.FeetPosition + new FixedVector3(0, 1000, 0));
        RefreshForecast();
    }

    private void RefreshForecast()
    {
        if (graph is null || partIndex < 0 || SelectableNodes().Count == 0)
        {
            forecast = null;
            return;
        }
        FocusTargetNode node = SelectedNode();
        StableId actionId = StableIdFactory.CreateChild(playerId, "focus-forecast", $"{coordinator.FrozenSimulationTick}:{targetIndex}:{partIndex}:{actionSequence}");
        forecast = forecasts.Forecast(combat.Runtime.State, combat.Runtime.Catalog, actionId, playerId, node, targetMoving: false, targetInCover: false);
        camera.Select(node.ActorId, node.NodeId, combat.Runtime.State.GetActor(node.ActorId).Presentation.FeetPosition + new FixedVector3(0, 1000, 0));
    }

    private IReadOnlyList<FocusTargetNode> SelectableNodes() => graph?.Nodes.Where(static node => node.IsSelectable).OrderBy(static node => node.DisplayName, StringComparer.Ordinal).ToArray() ?? [];
    private FocusTargetNode SelectedNode() => SelectableNodes()[Math.Clamp(partIndex, 0, SelectableNodes().Count - 1)];
    private string TargetName(StableId actorId) => combat.Runtime.State.TryGetActor(actorId, out CombatActorState? actor) && actor is not null ? actor.Presentation.Appearance.ArchetypeId : actorId.ToString();
    private string PartName(StableId nodeId) => graph?.Nodes.FirstOrDefault(value => value.NodeId == nodeId)?.DisplayName ?? nodeId.ToString();
    private static int Wrap(int value, int count) => count <= 0 ? 0 : (value % count + count) % count;
    private static long DistanceSquared(FixedVector3 left, FixedVector3 right)
    {
        long x = left.XMillimeters - right.XMillimeters;
        long y = left.YMillimeters - right.YMillimeters;
        long z = left.ZMillimeters - right.ZMillimeters;
        return checked(x * x + y * y + z * z);
    }
}
