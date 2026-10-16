using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Roentgen.UI;

namespace Roentgen.App;

[Flags]
internal enum CombatInputButtons
{
    None = 0,
    FireHeld = 1 << 0,
    ReloadPressed = 1 << 1,
    BlockHeld = 1 << 2,
    EquipSidearmPressed = 1 << 3,
    EquipRiflePressed = 1 << 4,
    EquipMeleePressed = 1 << 5,
    ThrowGrenadePressed = 1 << 6
}

internal readonly record struct CombatInputFrame(CombatInputButtons Buttons);

internal sealed class Phase10InputAdapter
{
    private KeyboardState previousKeyboard;
    private GamePadState previousGamePad;
    private MouseState previousMouse;
    private bool aimToggle;

    public CombatInputFrame Poll(Phase20UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        GamePadState gamepad = GamePad.GetState(PlayerIndex.One);
        bool Down(UiAction action) => BindingDown(settings, action, keyboard, gamepad, mouse);
        bool WasDown(UiAction action) => BindingDown(settings, action, previousKeyboard, previousGamePad, previousMouse);
        bool Pressed(UiAction action) => Down(action) && !WasDown(action);

        bool fireHeld = Down(UiAction.PrimaryAttack);
        bool aimPhysical = Down(UiAction.SecondaryAttack);
        bool aimPressed = Pressed(UiAction.SecondaryAttack);
        if (settings.Accessibility.AimBehavior == ToggleBehavior.Toggle && aimPressed) aimToggle = !aimToggle;
        bool aimHeld = settings.Accessibility.AimBehavior == ToggleBehavior.Toggle ? aimToggle : aimPhysical;

        CombatInputButtons buttons = CombatInputButtons.None;
        if (fireHeld) buttons |= CombatInputButtons.FireHeld;
        if (Pressed(UiAction.Reload)) buttons |= CombatInputButtons.ReloadPressed;
        if (aimHeld) buttons |= CombatInputButtons.BlockHeld;
        if (Pressed(UiAction.EquipSidearm)) buttons |= CombatInputButtons.EquipSidearmPressed;
        if (Pressed(UiAction.EquipRifle)) buttons |= CombatInputButtons.EquipRiflePressed;
        if (Pressed(UiAction.EquipMelee)) buttons |= CombatInputButtons.EquipMeleePressed;
        if (Pressed(UiAction.ThrowGrenade)) buttons |= CombatInputButtons.ThrowGrenadePressed;
        previousKeyboard = keyboard;
        previousGamePad = gamepad;
        previousMouse = mouse;
        return new CombatInputFrame(buttons);
    }

    private static bool BindingDown(Phase20UserSettings settings, UiAction action, KeyboardState keyboard, GamePadState gamepad, MouseState mouse)
    {
        if (!settings.Bindings.TryGetValue(action, out InputBinding? binding)) return false;
        return (binding.PrimaryKey != Keys.None && keyboard.IsKeyDown(binding.PrimaryKey))
            || (binding.SecondaryKey != Keys.None && keyboard.IsKeyDown(binding.SecondaryKey))
            || (binding.GamepadButton != Buttons.None && gamepad.IsButtonDown(binding.GamepadButton))
            || Phase20InputRouter.IsMouseButtonDown(mouse, binding.MouseButton);
    }
}
