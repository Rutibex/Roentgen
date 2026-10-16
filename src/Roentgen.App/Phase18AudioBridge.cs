
using Microsoft.Xna.Framework;
using Roentgen.Audio;
using Roentgen.Combat;
using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Rendering;
using Roentgen.Simulation;
using Roentgen.World.Structures;

namespace Roentgen.App;

/// <summary>
/// Presentation-side projection from authoritative simulation snapshots into semantic audio events.
/// It never mutates simulation state and its event IDs are derived from authoritative ticks and identities.
/// </summary>
internal sealed class Phase18AudioBridge
{
    private readonly StableId _playerId;
    private CombatPresentationSnapshot? _previousCombat;
    private PlayerControlMode _previousControlMode;
    private ulong _nextFootstepTick;
    private long _eventSequence;
    private readonly Dictionary<StableId, DoorLifecycleState> _doorStates = [];
    private readonly HashSet<StableId> _carryItems = [];
    private StableId _openContainerId;

    public Phase18AudioBridge(StableId playerId) => _playerId = playerId;

    public void Initialize(IAudioRuntime runtime, PlayerPresentationSnapshot player, CombatPresentationSnapshot combat, CameraState camera, PlayerSimulationRuntime? simulation = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _previousCombat = combat;
        _previousControlMode = player.Player.ControlMode;
        CaptureInteractionState(player, simulation);
        runtime.UpdateListener(ToListener(camera, player.Player.Velocity));
        // Submit the sanity cue before optional regional ambience.  If a generated region
        // needs to fall back, the player still immediately hears that Playroma SFX is alive.
        runtime.Submit(Create(player.Tick, AudioEventKind.InterfaceConfirm, Emitter(player.Player.FeetPosition), parameters: Text("variant", "startup")));
        runtime.SetRegion(AudioRegion.BrokenMeridian, 0x42524F4B454E4D45UL);
    }

    public void Update(IAudioRuntime runtime, PlayerPresentationSnapshot player, CombatPresentationSnapshot combat, CameraState camera, bool interfaceOpen, PlayerSimulationRuntime? simulation = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        runtime.UpdateListener(ToListener(camera, player.Player.Velocity));
        ProjectLocomotion(runtime, player);
        ProjectCombat(runtime, player, combat);
        ProjectInteractions(runtime, player, simulation);
        ProjectMode(runtime, player, interfaceOpen);
        _previousCombat = combat;
        _previousControlMode = player.Player.ControlMode;
    }

    public void EmitInterface(IAudioRuntime runtime, ulong tick, AudioEventKind kind) =>
        runtime.Submit(Create(tick, kind, AudioEmitterState.At(AudioVector3.Zero)));

    private void ProjectLocomotion(IAudioRuntime runtime, PlayerPresentationSnapshot snapshot)
    {
        PlayerState player = snapshot.Player;
        if (!player.Grounded || player.Locomotion is LocomotionState.Idle or LocomotionState.Airborne or LocomotionState.Mantling or LocomotionState.Ladder) return;
        ulong cadence = player.Locomotion switch
        {
            LocomotionState.Sprinting => 17,
            LocomotionState.Running => 23,
            LocomotionState.Crouching => 42,
            _ => 32
        };
        if (snapshot.Tick < _nextFootstepTick) return;
        _nextFootstepTick = snapshot.Tick + cadence;
        AudioGait gait = player.Locomotion switch
        {
            LocomotionState.Sprinting => AudioGait.Sprint,
            LocomotionState.Running => AudioGait.Run,
            LocomotionState.Crouching => AudioGait.Sneak,
            _ => AudioGait.Walk
        };
        runtime.Submit(Create(snapshot.Tick, AudioEventKind.FootContact, Emitter(player.FeetPosition, 0.5f, 35f),
            parameters: Values(("surface", AudioValue.FromInt64((long)AudioSurface.Asphalt)), ("gait", AudioValue.FromInt64((long)gait)))));
    }

    private void ProjectCombat(IAudioRuntime runtime, PlayerPresentationSnapshot player, CombatPresentationSnapshot combat)
    {
        CombatPresentationSnapshot? previous = _previousCombat;
        if (previous is null) return;
        AudioEmitterState emitter = Emitter(player.Player.FeetPosition, 1f, 160f);
        string family = WeaponFamily(combat.WeaponName);
        IReadOnlyDictionary<string, AudioValue> parameters = Text("variant", family);
        if (combat.Ammunition < previous.Ammunition)
        {
            AudioEventKind kind = family == "scrap-impact-tool" ? AudioEventKind.MeleeSwing : AudioEventKind.WeaponFire;
            runtime.Submit(Create(player.Tick, kind, emitter, itemId: StableIdFactory.Create("audio.weapon", family), parameters: parameters));
        }
        else if (combat.Ammunition > previous.Ammunition)
        {
            runtime.Submit(Create(player.Tick, AudioEventKind.ReloadComplete, emitter, itemId: StableIdFactory.Create("audio.weapon", family), parameters: parameters));
        }
        if (combat.Health < previous.Health)
            runtime.Submit(Create(player.Tick, combat.Health <= 0 ? AudioEventKind.Death : AudioEventKind.Injury, emitter, targetId: _playerId));
        if (combat.RecentFeedback.Count > previous.RecentFeedback.Count)
        {
            CombatFeedbackEntry feedback = combat.RecentFeedback[^1];
            string message = feedback.Message ?? string.Empty;
            if (message.Contains("jam", StringComparison.OrdinalIgnoreCase)) runtime.Submit(Create(player.Tick, AudioEventKind.WeaponJam, emitter, parameters: parameters));
            else if (message.Contains("empty", StringComparison.OrdinalIgnoreCase)) runtime.Submit(Create(player.Tick, AudioEventKind.WeaponDryFire, emitter, parameters: parameters));
            else if (message.Contains("explos", StringComparison.OrdinalIgnoreCase)) runtime.Submit(Create(player.Tick, AudioEventKind.Explosion, emitter));
            else if (message.Contains("armor", StringComparison.OrdinalIgnoreCase)) runtime.Submit(Create(player.Tick, AudioEventKind.ArmorImpact, emitter));
            else if (message.Contains("hit", StringComparison.OrdinalIgnoreCase) || message.Contains("damage", StringComparison.OrdinalIgnoreCase)) runtime.Submit(Create(player.Tick, AudioEventKind.Impact, emitter, parameters: Values(("surface", AudioValue.FromInt64((long)AudioSurface.Concrete)))));
        }
    }

    private void CaptureInteractionState(PlayerPresentationSnapshot snapshot, PlayerSimulationRuntime? simulation)
    {
        _doorStates.Clear();
        if (simulation is not null)
            foreach (DoorRuntimeState door in simulation.Doors) _doorStates[door.Definition.Id] = door.State;
        _carryItems.Clear();
        foreach (PortableObjectState item in snapshot.CarryItems) _carryItems.Add(item.Id);
        _openContainerId = snapshot.Player.OpenContainerId;
    }

    private void ProjectInteractions(IAudioRuntime runtime, PlayerPresentationSnapshot snapshot, PlayerSimulationRuntime? simulation)
    {
        if (simulation is null) return;

        foreach (DoorRuntimeState door in simulation.Doors)
        {
            StableId id = door.Definition.Id;
            if (!_doorStates.TryGetValue(id, out DoorLifecycleState previous))
            {
                _doorStates[id] = door.State;
                continue;
            }
            if (previous == door.State) continue;
            if (door.State == DoorLifecycleState.Open)
                runtime.Submit(Create(snapshot.Tick, AudioEventKind.DoorOpen, Emitter(door), targetId: id));
            else if (previous == DoorLifecycleState.Open && door.State is not (DoorLifecycleState.Missing or DoorLifecycleState.Destroyed))
                runtime.Submit(Create(snapshot.Tick, AudioEventKind.DoorClose, Emitter(door), targetId: id));
            _doorStates[id] = door.State;
        }

        StableId currentContainer = snapshot.Player.OpenContainerId;
        if (currentContainer != _openContainerId)
        {
            AudioEmitterState nearby = Emitter(snapshot.Player.FeetPosition, 0.5f, 22f);
            if (!_openContainerId.IsEmpty) runtime.Submit(Create(snapshot.Tick, AudioEventKind.ContainerClose, nearby, targetId: _openContainerId));
            if (!currentContainer.IsEmpty) runtime.Submit(Create(snapshot.Tick, AudioEventKind.ContainerOpen, nearby, targetId: currentContainer));
            _openContainerId = currentContainer;
        }

        bool acquired = false;
        foreach (PortableObjectState item in snapshot.CarryItems)
        {
            if (_carryItems.Add(item.Id)) acquired = true;
        }
        _carryItems.RemoveWhere(id => !snapshot.CarryItems.Any(item => item.Id == id));
        if (acquired) runtime.Submit(Create(snapshot.Tick, AudioEventKind.Pickup, Emitter(snapshot.Player.FeetPosition, 0.35f, 14f)));

        StableId activated = simulation.LastActivatedTargetId;
        if (!activated.IsEmpty && !simulation.ActorTargets.Any(actor => actor.Id == activated))
            runtime.Submit(Create(snapshot.Tick, AudioEventKind.TerminalUse, Emitter(snapshot.Player.FeetPosition, 0.5f, 18f), targetId: activated));
    }

    private void ProjectMode(IAudioRuntime runtime, PlayerPresentationSnapshot snapshot, bool interfaceOpen)
    {
        PlayerControlMode mode = snapshot.Player.ControlMode;
        AudioMixSnapshotKind mix = interfaceOpen ? AudioMixSnapshotKind.Dialogue : mode == PlayerControlMode.Paused ? AudioMixSnapshotKind.PauseMenu : AudioMixSnapshotKind.Gameplay;
        runtime.ApplyMixSnapshot(mix);
        if (mode != _previousControlMode && (mode == PlayerControlMode.Paused || _previousControlMode == PlayerControlMode.Paused))
        {
            AudioEventKind kind = mode == PlayerControlMode.Paused ? AudioEventKind.InterfaceCancel : AudioEventKind.InterfaceConfirm;
            runtime.Submit(Create(snapshot.Tick, kind, AudioEmitterState.At(AudioVector3.Zero)));
        }
    }

    private SemanticAudioEvent Create(ulong tick, AudioEventKind kind, AudioEmitterState emitter, StableId sourceId = default, StableId targetId = default, StableId itemId = default, IReadOnlyDictionary<string, AudioValue>? parameters = null)
    {
        long sequence = Interlocked.Increment(ref _eventSequence);
        StableId id = StableIdFactory.CreateChild(_playerId, "audio-event", $"{tick}:{kind}:{sequence}");
        return SemanticAudioEvent.Create(id, tick, kind, emitter, sourceId.IsEmpty ? _playerId : sourceId, targetId, itemId, parameters: parameters);
    }

    private static AudioListenerState ToListener(CameraState camera, FixedVector3 velocity) => new(
        new AudioVector3(camera.Position.X, camera.Position.Y, camera.Position.Z),
        new AudioVector3(camera.Forward.X, camera.Forward.Y, camera.Forward.Z),
        new AudioVector3(camera.Up.X, camera.Up.Y, camera.Up.Z),
        new AudioVector3(velocity.XMillimeters / 1000f, velocity.YMillimeters / 1000f, velocity.ZMillimeters / 1000f),
        "outdoor");

    private static AudioEmitterState Emitter(FixedVector3 position, float minDistance = 1f, float maxDistance = 100f) => new(
        new AudioVector3(position.XMillimeters / 1000f, position.YMillimeters / 1000f, position.ZMillimeters / 1000f),
        AudioVector3.Zero, minDistance, maxDistance, 0f, 0f, "outdoor");

    private static AudioEmitterState Emitter(DoorRuntimeState door) => new(
        new AudioVector3(door.Definition.WorldCenter.XCentimeters / 100f, door.Definition.WorldCenter.YCentimeters / 100f, door.Definition.WorldCenter.ZCentimeters / 100f),
        AudioVector3.Zero, 0.75f, 35f, 0f, 0f, "outdoor");

    private static IReadOnlyDictionary<string, AudioValue> Text(string key, string value) => Values((key, AudioValue.FromText(value)));
    private static IReadOnlyDictionary<string, AudioValue> Values(params (string Key, AudioValue Value)[] values) => values.ToDictionary(static value => value.Key, static value => value.Value, StringComparer.Ordinal);
    private static string WeaponFamily(string name) => name.Contains("rifle", StringComparison.OrdinalIgnoreCase) ? "industrial-rifle" : name.Contains("impact", StringComparison.OrdinalIgnoreCase) || name.Contains("melee", StringComparison.OrdinalIgnoreCase) ? "scrap-impact-tool" : "service-sidearm";
}
