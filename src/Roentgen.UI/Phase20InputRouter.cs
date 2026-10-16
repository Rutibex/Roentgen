using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Roentgen.UI;

/// <summary>One unified menu input frame. Semantic actions and raw newly-pressed inputs coexist so remapping can capture either.</summary>
public sealed record Phase20InputFrame(
    IReadOnlySet<UiAction> Pressed,
    IReadOnlySet<UiAction> Repeated,
    Point Pointer,
    int ScrollDelta,
    bool PointerPressed,
    bool PointerReleased,
    bool PointerDoubleClicked,
    bool PointerMoved,
    UiInputDevice ActiveDevice,
    IReadOnlyList<Keys> NewlyPressedKeys,
    IReadOnlyList<Buttons> NewlyPressedGamepadButtons,
    UiMouseButton NewlyPressedMouseButton)
{
    public bool IsPressed(UiAction action) => Pressed.Contains(action);
    public bool IsRepeated(UiAction action) => Pressed.Contains(action) || Repeated.Contains(action);
    public bool UsingGamepad => ActiveDevice == UiInputDevice.Gamepad;

    public UiPhysicalInput? FirstPhysicalInput()
    {
        if (NewlyPressedKeys.Count > 0) return UiPhysicalInput.Keyboard(NewlyPressedKeys[0]);
        if (NewlyPressedMouseButton != UiMouseButton.None) return UiPhysicalInput.Mouse(NewlyPressedMouseButton);
        if (NewlyPressedGamepadButtons.Count > 0) return UiPhysicalInput.Gamepad(NewlyPressedGamepadButtons[0]);
        return null;
    }
}

public sealed class Phase20InputRouter
{
    private KeyboardState previousKeyboard;
    private GamePadState previousGamepad;
    private MouseState previousMouse;
    private readonly Dictionary<UiAction, int> heldFrames = [];
    private UiInputDevice activeDevice = UiInputDevice.Keyboard;
    private long lastLeftClickMilliseconds = long.MinValue;
    private Point lastLeftClickPoint;

    private static readonly Buttons[] CapturableButtons =
    [
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        Buttons.Start, Buttons.Back,
        Buttons.LeftTrigger, Buttons.RightTrigger
    ];

    public Phase20InputFrame Poll(Phase20UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        KeyboardState keyboard = Keyboard.GetState();
        GamePadState gamepad = GamePad.GetState(PlayerIndex.One);
        MouseState mouse = Mouse.GetState();
        var pressed = new HashSet<UiAction>();
        var repeated = new HashSet<UiAction>();

        Keys[] newlyPressedKeys = keyboard.GetPressedKeys().Where(key => !previousKeyboard.IsKeyDown(key)).ToArray();
        Buttons[] newlyPressedButtons = CapturableButtons.Where(button => gamepad.IsButtonDown(button) && !previousGamepad.IsButtonDown(button)).ToArray();
        UiMouseButton newlyPressedMouse = FirstNewMouseButton(mouse, previousMouse);
        bool pointerMoved = mouse.X != previousMouse.X || mouse.Y != previousMouse.Y;
        int scroll = Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue);

        bool keyboardUsed = newlyPressedKeys.Length > 0;
        bool mouseUsed = pointerMoved || newlyPressedMouse != UiMouseButton.None || scroll != 0;
        bool gamepadUsed = newlyPressedButtons.Length > 0 || ThumbstickMoved(gamepad, previousGamepad);
        if (gamepadUsed) activeDevice = UiInputDevice.Gamepad;
        else if (mouseUsed) activeDevice = UiInputDevice.Mouse;
        else if (keyboardUsed) activeDevice = UiInputDevice.Keyboard;

        foreach ((UiAction action, InputBinding binding) in settings.Bindings)
        {
            bool down = BindingDown(binding, keyboard, gamepad, mouse);
            bool wasDown = BindingDown(binding, previousKeyboard, previousGamepad, previousMouse);
            if (down && !wasDown) pressed.Add(action);

            if (down)
            {
                int frames = heldFrames.TryGetValue(action, out int value) ? value + 1 : 1;
                heldFrames[action] = frames;
                if (frames >= 18 && (frames - 18) % 5 == 0) repeated.Add(action);
            }
            else heldFrames.Remove(action);
        }

        bool pointerPressed = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        bool pointerReleased = mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed;
        bool doubleClicked = false;
        if (pointerPressed)
        {
            long now = Environment.TickCount64;
            long elapsed = lastLeftClickMilliseconds == long.MinValue ? long.MaxValue : now - lastLeftClickMilliseconds;
            int dx = mouse.X - lastLeftClickPoint.X;
            int dy = mouse.Y - lastLeftClickPoint.Y;
            doubleClicked = elapsed >= 0 && elapsed <= 420 && dx * dx + dy * dy <= 64;
            lastLeftClickMilliseconds = now;
            lastLeftClickPoint = new Point(mouse.X, mouse.Y);
        }

        previousKeyboard = keyboard;
        previousGamepad = gamepad;
        previousMouse = mouse;
        return new Phase20InputFrame(
            pressed,
            repeated,
            new Point(mouse.X, mouse.Y),
            scroll,
            pointerPressed,
            pointerReleased,
            doubleClicked,
            pointerMoved,
            activeDevice,
            newlyPressedKeys,
            newlyPressedButtons,
            newlyPressedMouse);
    }

    public static string BindingLabel(Phase20UserSettings settings, UiAction action, UiInputDevice device)
        => UiBindingFormatter.BindingLabel(settings, action, device);

    private static bool BindingDown(InputBinding binding, KeyboardState keyboard, GamePadState gamepad, MouseState mouse)
        => (binding.PrimaryKey != Keys.None && keyboard.IsKeyDown(binding.PrimaryKey))
           || (binding.SecondaryKey != Keys.None && keyboard.IsKeyDown(binding.SecondaryKey))
           || (binding.GamepadButton != Buttons.None && gamepad.IsButtonDown(binding.GamepadButton))
           || IsMouseButtonDown(mouse, binding.MouseButton);

    public static bool IsMouseButtonDown(MouseState mouse, UiMouseButton button) => button switch
    {
        UiMouseButton.Left => mouse.LeftButton == ButtonState.Pressed,
        UiMouseButton.Right => mouse.RightButton == ButtonState.Pressed,
        UiMouseButton.Middle => mouse.MiddleButton == ButtonState.Pressed,
        UiMouseButton.XButton1 => mouse.XButton1 == ButtonState.Pressed,
        UiMouseButton.XButton2 => mouse.XButton2 == ButtonState.Pressed,
        _ => false
    };

    private static UiMouseButton FirstNewMouseButton(MouseState current, MouseState previous)
    {
        if (current.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released) return UiMouseButton.Left;
        if (current.RightButton == ButtonState.Pressed && previous.RightButton == ButtonState.Released) return UiMouseButton.Right;
        if (current.MiddleButton == ButtonState.Pressed && previous.MiddleButton == ButtonState.Released) return UiMouseButton.Middle;
        if (current.XButton1 == ButtonState.Pressed && previous.XButton1 == ButtonState.Released) return UiMouseButton.XButton1;
        if (current.XButton2 == ButtonState.Pressed && previous.XButton2 == ButtonState.Released) return UiMouseButton.XButton2;
        return UiMouseButton.None;
    }

    private static bool ThumbstickMoved(GamePadState current, GamePadState previous)
    {
        Vector2 c = current.ThumbSticks.Left;
        Vector2 p = previous.ThumbSticks.Left;
        Vector2 cr = current.ThumbSticks.Right;
        Vector2 pr = previous.ThumbSticks.Right;
        return (c.LengthSquared() > 0.20f && p.LengthSquared() <= 0.20f)
               || (cr.LengthSquared() > 0.20f && pr.LengthSquared() <= 0.20f);
    }
}
