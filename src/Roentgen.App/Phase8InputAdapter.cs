using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roentgen.Contracts;
using Roentgen.UI;

namespace Roentgen.App;

internal sealed class Phase8InputAdapter
{
    private KeyboardState previousKeyboard;
    private GamePadState previousGamePad;
    private MouseState previousMouse;
    private bool mouseInitialized;
    private bool crouchToggle;
    private bool sprintToggle;
    private bool previousCrouchDesired;

    public float MouseSensitivity { get; set; } = 0.12f;
    public float StickSensitivity { get; set; } = 2.2f;
    public bool InvertVerticalLook { get; set; }
    public BindingBehavior CrouchBehavior { get; set; } = BindingBehavior.Toggle;
    public BindingBehavior SprintBehavior { get; set; } = BindingBehavior.Hold;
    public InputDeviceKind ActiveDevice { get; private set; } = InputDeviceKind.KeyboardMouse;

    public PlayerIntentFrame Poll(GameWindow window, bool mouseCaptured, Phase20UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        KeyboardState keyboard = Keyboard.GetState();
        GamePadState gamepad = GamePad.GetState(PlayerIndex.One);
        MouseState mouse = Mouse.GetState();
        bool Down(UiAction action) => BindingDown(settings, action, keyboard, gamepad, mouse);
        bool WasDown(UiAction action) => BindingDown(settings, action, previousKeyboard, previousGamePad, previousMouse);
        bool Pressed(UiAction action) => Down(action) && !WasDown(action);

        float moveX = (Down(UiAction.StrafeRight) ? 1f : 0f) - (Down(UiAction.StrafeLeft) ? 1f : 0f);
        float moveY = (Down(UiAction.MoveForward) ? 1f : 0f) - (Down(UiAction.MoveBackward) ? 1f : 0f);
        Vector2 stick = gamepad.ThumbSticks.Left;
        if (stick.LengthSquared() > 0.03f)
        {
            moveX = stick.X;
            moveY = stick.Y;
            ActiveDevice = InputDeviceKind.Gamepad;
        }
        else if (moveX != 0 || moveY != 0) ActiveDevice = InputDeviceKind.KeyboardMouse;

        float lookX = 0;
        float lookY = 0;
        if (!mouseCaptured)
        {
            // Discard the old center reference while menus own the cursor.  Without this,
            // returning to gameplay can turn the first visible cursor position into a
            // very large camera delta.
            mouseInitialized = false;
        }
        if (mouseCaptured)
        {
            Point center = new(window.ClientBounds.Width / 2, window.ClientBounds.Height / 2);
            if (mouseInitialized)
            {
                lookX = (mouse.X - center.X) * MouseSensitivity;
                // Player pitch is positive when looking up, while screen-space mouse Y
                // grows downward. Negate the mouse delta so the default is conventional
                // (mouse up = look up); InvertVerticalLook flips it below when requested.
                lookY = -(mouse.Y - center.Y) * MouseSensitivity;
            }
            Mouse.SetPosition(center.X, center.Y);
            mouseInitialized = true;
        }
        Vector2 right = gamepad.ThumbSticks.Right;
        if (right.LengthSquared() > 0.02f)
        {
            lookX += right.X * StickSensitivity;
            // MonoGame thumbstick Y is positive upward, matching positive player pitch.
            lookY += right.Y * StickSensitivity;
            ActiveDevice = InputDeviceKind.Gamepad;
        }
        if (InvertVerticalLook) lookY = -lookY;

        if (Pressed(UiAction.Crouch) && CrouchBehavior == BindingBehavior.Toggle) crouchToggle = !crouchToggle;
        if (Pressed(UiAction.Sprint) && SprintBehavior == BindingBehavior.Toggle) sprintToggle = !sprintToggle;
        bool crouchHeld = CrouchBehavior == BindingBehavior.Toggle ? crouchToggle : Down(UiAction.Crouch);
        bool sprintHeld = SprintBehavior == BindingBehavior.Toggle ? sprintToggle : Down(UiAction.Sprint);

        PlayerIntentButtons buttons = PlayerIntentButtons.None;
        if (sprintHeld) buttons |= PlayerIntentButtons.SprintHeld;
        if (crouchHeld) buttons |= PlayerIntentButtons.CrouchHeld;
        if (crouchHeld != previousCrouchDesired) buttons |= PlayerIntentButtons.CrouchPressed;
        previousCrouchDesired=crouchHeld;
        if (Pressed(UiAction.Jump)) buttons |= PlayerIntentButtons.JumpPressed;
        if (Pressed(UiAction.Interact)) buttons |= PlayerIntentButtons.InteractPressed;
        if (Pressed(UiAction.Cancel)) buttons |= PlayerIntentButtons.CancelPressed | PlayerIntentButtons.PausePressed;

        previousKeyboard = keyboard;
        previousGamePad = gamepad;
        previousMouse = mouse;
        return new PlayerIntentFrame(ToAxis(moveX), ToAxis(moveY), ToLook(lookX), ToLook(lookY), buttons, ActiveDevice);
    }

    private static bool BindingDown(Phase20UserSettings settings, UiAction action, KeyboardState keyboard, GamePadState gamepad, MouseState mouse)
    {
        if (!settings.Bindings.TryGetValue(action, out InputBinding? binding)) return false;
        return (binding.PrimaryKey != Keys.None && keyboard.IsKeyDown(binding.PrimaryKey))
            || (binding.SecondaryKey != Keys.None && keyboard.IsKeyDown(binding.SecondaryKey))
            || (binding.GamepadButton != Buttons.None && gamepad.IsButtonDown(binding.GamepadButton))
            || Phase20InputRouter.IsMouseButtonDown(mouse, binding.MouseButton);
    }

    private static short ToAxis(float value) => (short)Math.Clamp((int)Math.Round(value * PlayerIntentFrame.AxisMaximum), short.MinValue, short.MaxValue);
    private static short ToLook(float degrees) => (short)Math.Clamp((int)Math.Round(degrees * 10000), short.MinValue, short.MaxValue);
}
