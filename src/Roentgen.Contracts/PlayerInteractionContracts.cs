namespace Roentgen.Contracts;

[Flags]
public enum PlayerIntentButtons : ushort
{
    None = 0,
    SprintHeld = 1 << 0,
    WalkHeld = 1 << 1,
    CrouchPressed = 1 << 2,
    CrouchHeld = 1 << 3,
    JumpPressed = 1 << 4,
    InteractPressed = 1 << 5,
    CancelPressed = 1 << 6,
    PausePressed = 1 << 7,
    TransferAllPressed = 1 << 8,
    ToggleDiagnosticsPressed = 1 << 9
}

public enum PlayerActionId
{
    MoveHorizontal,
    MoveVertical,
    LookHorizontal,
    LookVertical,
    Walk,
    Sprint,
    Crouch,
    JumpOrMantle,
    Interact,
    Cancel,
    Pause,
    TransferAll,
    ToggleDiagnostics
}

public enum PlayerControlMode { Exploration = 0, Container = 1, Paused = 2, Traversal = 3 }
public enum PlayerStance { Standing = 0, Crouched = 1 }
public enum LocomotionState { Idle = 0, Walking = 1, Running = 2, Sprinting = 3, Crouching = 4, Airborne = 5, Sliding = 6, Mantling = 7, Ladder = 8 }
public enum TraversalState { None = 0, Mantle = 1, Ladder = 2 }
public enum InteractionVerb { Open = 0, Close = 1, Take = 2, Search = 3, Transfer = 4, Climb = 5, Inspect = 6, Activate = 7 }
public enum InteractionFailureReason { None = 0, OutOfRange = 1, Occluded = 2, Locked = 3, Jammed = 4, Barricaded = 5, Blocked = 6, Unloaded = 7, Empty = 8, InvalidState = 9 }
public enum InputDeviceKind { KeyboardMouse = 0, Gamepad = 1 }
public enum BindingBehavior { Hold = 0, Toggle = 1 }

public readonly record struct PlayerIntentFrame(
    short MoveX,
    short MoveY,
    short LookYawDelta,
    short LookPitchDelta,
    PlayerIntentButtons Buttons,
    InputDeviceKind Device)
{
    public const short AxisMaximum = 32767;
    public double NormalizedMoveX => MoveX / (double)AxisMaximum;
    public double NormalizedMoveY => MoveY / (double)AxisMaximum;
}

public sealed record InteractionOption(
    InteractionVerb Verb,
    string PromptKey,
    bool Enabled,
    InteractionFailureReason FailureReason,
    int HoldDurationTicks,
    int Priority);

public sealed record InteractionTargetDescriptor(
    StableId TargetId,
    string DisplayName,
    IReadOnlyList<InteractionOption> Options,
    int DistanceMillimeters,
    int Score,
    bool Occluded);
